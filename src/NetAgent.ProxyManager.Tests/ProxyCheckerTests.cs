using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxyCheckerTests
{
    [Fact]
    public async Task CheckAsync_ShouldAuthenticateHttpProxyAndVerifyEgressIp()
    {
        using var proxyServer = new FakeHttpProxyServer("203.0.113.10");
        var proxy = new ProxyServer
        {
            Proxy = $"127.0.0.1:{proxyServer.Port}",
            Protocol = ProxyProtocol.Https,
            Username = "user",
            Password = "pass"
        };
        var checker = new ProxyChecker(TimeSpan.FromSeconds(3), new Uri("http://check.example/ip"));

        var result = await checker.CheckAsync(proxy, CancellationToken.None);
        var request = await proxyServer.RequestTask;

        result.IsReachable.Should().BeTrue();
        result.EgressIp.Should().Be("203.0.113.10");
        result.LatencyMs.Should().NotBeNull();
        request.Should().Contain("GET http://check.example/ip HTTP/1.1");
        request.Should().Contain("Proxy-Authorization: Basic dXNlcjpwYXNz");
    }

    [Fact]
    public async Task CheckAsync_ShouldFailHttpProxyWhenAuthenticationIsRequired()
    {
        using var proxyServer = new FakeHttpProxyServer("203.0.113.11", requireAuthentication: true);
        var proxy = new ProxyServer
        {
            Proxy = $"127.0.0.1:{proxyServer.Port}",
            Protocol = ProxyProtocol.Https
        };
        var checker = new ProxyChecker(TimeSpan.FromSeconds(3), new Uri("http://check.example/ip"));

        var result = await checker.CheckAsync(proxy, CancellationToken.None);

        result.IsReachable.Should().BeFalse();
        result.ErrorMessage.Should().Contain("xác thực");
    }

    [Fact]
    public async Task CheckAsync_ShouldAuthenticateSocks5ProxyAndVerifyEgressIp()
    {
        using var proxyServer = new FakeSocks5ProxyServer("198.51.100.9", expectedUsername: "socks-user", expectedPassword: "socks-pass");
        var proxy = new ProxyServer
        {
            Proxy = $"127.0.0.1:{proxyServer.Port}",
            Protocol = ProxyProtocol.Socks5,
            Username = "socks-user",
            Password = "socks-pass"
        };
        var checker = new ProxyChecker(TimeSpan.FromSeconds(3), new Uri("http://check.example/ip"));

        var result = await checker.CheckAsync(proxy, CancellationToken.None);
        var request = await proxyServer.RequestTask;

        result.IsReachable.Should().BeTrue();
        result.EgressIp.Should().Be("198.51.100.9");
        result.LatencyMs.Should().NotBeNull();
        request.Should().Contain("GET /ip HTTP/1.1");
        request.Should().Contain("Host: check.example");
    }

    private sealed class FakeHttpProxyServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _egressIp;
        private readonly bool _requireAuthentication;

        public FakeHttpProxyServer(string egressIp, bool requireAuthentication = false)
        {
            _egressIp = egressIp;
            _requireAuthentication = requireAuthentication;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            RequestTask = RunAsync();
        }

        public int Port { get; }
        public Task<string> RequestTask { get; }

        public void Dispose()
        {
            _listener.Stop();
        }

        private async Task<string> RunAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();
            var request = await ReadHeadersAsync(stream);
            var authenticated = request.Contains("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase);
            var response = _requireAuthentication && !authenticated
                ? "HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"test\"\r\nContent-Length: 0\r\nConnection: close\r\n\r\n"
                : BuildHttpResponse(_egressIp);

            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
            return request;
        }
    }

    private sealed class FakeSocks5ProxyServer : IDisposable
    {
        private readonly TcpListener _listener;
        private readonly string _egressIp;
        private readonly string? _expectedUsername;
        private readonly string? _expectedPassword;

        public FakeSocks5ProxyServer(string egressIp, string? expectedUsername = null, string? expectedPassword = null)
        {
            _egressIp = egressIp;
            _expectedUsername = expectedUsername;
            _expectedPassword = expectedPassword;
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            Port = ((IPEndPoint)_listener.LocalEndpoint).Port;
            RequestTask = RunAsync();
        }

        public int Port { get; }
        public Task<string> RequestTask { get; }

        public void Dispose()
        {
            _listener.Stop();
        }

        private async Task<string> RunAsync()
        {
            using var client = await _listener.AcceptTcpClientAsync();
            await using var stream = client.GetStream();

            var version = await ReadByteAsync(stream);
            version.Should().Be(0x05);
            var methodCount = await ReadByteAsync(stream);
            var methods = await ReadExactAsync(stream, methodCount);
            var requiresAuth = _expectedUsername is not null;
            var selectedMethod = requiresAuth ? (byte)0x02 : (byte)0x00;
            methods.Should().Contain(selectedMethod);
            await stream.WriteAsync(new byte[] { 0x05, selectedMethod });

            if (requiresAuth)
            {
                await ReadAndValidateAuthenticationAsync(stream);
            }

            await ReadAndValidateConnectRequestAsync(stream);
            await stream.WriteAsync(new byte[] { 0x05, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });

            var request = await ReadHeadersAsync(stream);
            var response = BuildHttpResponse(_egressIp);
            await stream.WriteAsync(Encoding.ASCII.GetBytes(response));
            return request;
        }

        private async Task ReadAndValidateAuthenticationAsync(Stream stream)
        {
            var version = await ReadByteAsync(stream);
            version.Should().Be(0x01);
            var usernameLength = await ReadByteAsync(stream);
            var username = Encoding.UTF8.GetString(await ReadExactAsync(stream, usernameLength));
            var passwordLength = await ReadByteAsync(stream);
            var password = Encoding.UTF8.GetString(await ReadExactAsync(stream, passwordLength));

            var success = username == _expectedUsername && password == _expectedPassword;
            await stream.WriteAsync(new byte[] { 0x01, success ? (byte)0x00 : (byte)0x01 });
        }

        private static async Task ReadAndValidateConnectRequestAsync(Stream stream)
        {
            var header = await ReadExactAsync(stream, 5);
            header[0].Should().Be(0x05);
            header[1].Should().Be(0x01);
            header[2].Should().Be(0x00);
            header[3].Should().Be(0x03);

            var hostLength = header[4];
            var host = Encoding.ASCII.GetString(await ReadExactAsync(stream, hostLength));
            var portBytes = await ReadExactAsync(stream, 2);
            var port = portBytes[0] << 8 | portBytes[1];

            host.Should().Be("check.example");
            port.Should().Be(80);
        }
    }

    private static string BuildHttpResponse(string body) =>
        $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {Encoding.UTF8.GetByteCount(body)}\r\nConnection: close\r\n\r\n{body}";

    private static async Task<string> ReadHeadersAsync(Stream stream)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[1];
        while (true)
        {
            var read = await stream.ReadAsync(buffer);
            read.Should().Be(1);
            memory.WriteByte(buffer[0]);
            var bytes = memory.ToArray();
            if (bytes.Length >= 4 &&
                bytes[^4] == '\r' &&
                bytes[^3] == '\n' &&
                bytes[^2] == '\r' &&
                bytes[^1] == '\n')
            {
                return Encoding.ASCII.GetString(bytes);
            }
        }
    }

    private static async Task<byte> ReadByteAsync(Stream stream)
    {
        var buffer = await ReadExactAsync(stream, 1);
        return buffer[0];
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset));
            read.Should().BeGreaterThan(0);
            offset += read;
        }

        return buffer;
    }
}
