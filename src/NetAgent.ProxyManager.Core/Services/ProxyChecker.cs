using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ProxyChecker : IProxyChecker
{
    private static readonly Uri DefaultEgressEndpoint = new("http://api.ipify.org/");
    private readonly Uri _egressEndpoint;
    private readonly TimeSpan _timeout;

    public ProxyChecker(TimeSpan? timeout = null, Uri? egressEndpoint = null)
    {
        _timeout = timeout ?? TimeSpan.FromSeconds(20);
        _egressEndpoint = egressEndpoint ?? DefaultEgressEndpoint;
        if (!string.Equals(_egressEndpoint.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Proxy checker egress endpoint must use HTTP.", nameof(egressEndpoint));
        }
    }

    public async Task<ProxyCheckResult> CheckAsync(ProxyServer proxy, CancellationToken cancellationToken)
    {
        if (!proxy.HasValidEndpoint)
        {
            return new ProxyCheckResult(false, null, "Địa chỉ proxy không hợp lệ.");
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(_timeout);

        var stopwatch = Stopwatch.StartNew();
        try
        {
            var egressIp = proxy.Protocol switch
            {
                ProxyProtocol.Https => await CheckHttpProxyAsync(proxy, timeoutCts.Token),
                ProxyProtocol.Socks5 => await CheckSocks5ProxyAsync(proxy, timeoutCts.Token),
                _ => throw new NotSupportedException($"Protocol {proxy.Protocol} chưa được hỗ trợ.")
            };

            stopwatch.Stop();
            return new ProxyCheckResult(true, (int)stopwatch.ElapsedMilliseconds, EgressIp: egressIp);
        }
        catch (Exception ex) when (ex is SocketException or IOException or OperationCanceledException or InvalidDataException or NotSupportedException)
        {
            stopwatch.Stop();
            var message = ex is OperationCanceledException ? "Kiểm tra proxy quá thời gian chờ." : ex.Message;
            return new ProxyCheckResult(false, null, message);
        }
    }

    private async Task<string> CheckHttpProxyAsync(ProxyServer proxy, CancellationToken cancellationToken)
    {
        using var client = await ConnectToProxyAsync(proxy, cancellationToken);
        await using var stream = client.GetStream();

        var request = BuildHttpProxyRequest(proxy);
        await WriteAsciiAsync(stream, request, cancellationToken);
        var response = await ReadHttpResponseAsync(stream, cancellationToken);

        return ExtractVerifiedEgressIp(response);
    }

    private async Task<string> CheckSocks5ProxyAsync(ProxyServer proxy, CancellationToken cancellationToken)
    {
        using var client = await ConnectToProxyAsync(proxy, cancellationToken);
        await using var stream = client.GetStream();

        await PerformSocks5HandshakeAsync(stream, proxy, cancellationToken);

        var request = BuildOriginHttpRequest();
        await WriteAsciiAsync(stream, request, cancellationToken);
        var response = await ReadHttpResponseAsync(stream, cancellationToken);

        return ExtractVerifiedEgressIp(response);
    }

    private async Task<TcpClient> ConnectToProxyAsync(ProxyServer proxy, CancellationToken cancellationToken)
    {
        var client = new TcpClient();
        try
        {
            await client.ConnectAsync(proxy.Host, proxy.Port, cancellationToken);
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    private string BuildHttpProxyRequest(ProxyServer proxy)
    {
        var builder = new StringBuilder();
        builder.Append(CultureInfo.InvariantCulture, $"GET {_egressEndpoint.AbsoluteUri} HTTP/1.1\r\n");
        builder.Append(CultureInfo.InvariantCulture, $"Host: {GetHostHeaderValue()}\r\n");
        builder.Append("User-Agent: ProxyManager/1.0\r\n");
        builder.Append("Accept: text/plain\r\n");
        builder.Append("Connection: close\r\n");
        if (!string.IsNullOrWhiteSpace(proxy.Username))
        {
            var credential = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{proxy.Username}:{proxy.Password ?? string.Empty}"));
            builder.Append(CultureInfo.InvariantCulture, $"Proxy-Authorization: Basic {credential}\r\n");
        }

        builder.Append("\r\n");
        return builder.ToString();
    }

    private string BuildOriginHttpRequest()
    {
        var builder = new StringBuilder();
        var path = string.IsNullOrWhiteSpace(_egressEndpoint.PathAndQuery) ? "/" : _egressEndpoint.PathAndQuery;
        builder.Append(CultureInfo.InvariantCulture, $"GET {path} HTTP/1.1\r\n");
        builder.Append(CultureInfo.InvariantCulture, $"Host: {GetHostHeaderValue()}\r\n");
        builder.Append("User-Agent: ProxyManager/1.0\r\n");
        builder.Append("Accept: text/plain\r\n");
        builder.Append("Connection: close\r\n");
        builder.Append("\r\n");
        return builder.ToString();
    }

    private string GetHostHeaderValue()
    {
        var port = _egressEndpoint.Port;
        return port is 80 or -1
            ? _egressEndpoint.Host
            : $"{_egressEndpoint.Host}:{port}";
    }

    private async Task PerformSocks5HandshakeAsync(Stream stream, ProxyServer proxy, CancellationToken cancellationToken)
    {
        var hasCredentials = !string.IsNullOrWhiteSpace(proxy.Username);
        var greeting = hasCredentials
            ? new byte[] { 0x05, 0x02, 0x00, 0x02 }
            : [0x05, 0x01, 0x00];
        await stream.WriteAsync(greeting, cancellationToken);

        var methodResponse = await ReadExactAsync(stream, 2, cancellationToken);
        if (methodResponse[0] != 0x05)
        {
            throw new InvalidDataException("SOCKS5 proxy trả về phiên bản không hợp lệ.");
        }

        var method = methodResponse[1];
        if (method == 0xFF)
        {
            throw new InvalidDataException("SOCKS5 proxy không chấp nhận phương thức xác thực được hỗ trợ.");
        }

        if (method == 0x02)
        {
            await AuthenticateSocks5Async(stream, proxy, cancellationToken);
        }
        else if (method != 0x00)
        {
            throw new InvalidDataException($"SOCKS5 proxy yêu cầu phương thức xác thực chưa hỗ trợ: 0x{method:X2}.");
        }

        await SendSocks5ConnectAsync(stream, cancellationToken);
    }

    private static async Task AuthenticateSocks5Async(Stream stream, ProxyServer proxy, CancellationToken cancellationToken)
    {
        var username = Encoding.UTF8.GetBytes(proxy.Username ?? string.Empty);
        var password = Encoding.UTF8.GetBytes(proxy.Password ?? string.Empty);
        if (username.Length > byte.MaxValue || password.Length > byte.MaxValue)
        {
            throw new InvalidDataException("Username/password SOCKS5 quá dài.");
        }

        var request = new byte[3 + username.Length + password.Length];
        request[0] = 0x01;
        request[1] = (byte)username.Length;
        Buffer.BlockCopy(username, 0, request, 2, username.Length);
        request[2 + username.Length] = (byte)password.Length;
        Buffer.BlockCopy(password, 0, request, 3 + username.Length, password.Length);

        await stream.WriteAsync(request, cancellationToken);
        var response = await ReadExactAsync(stream, 2, cancellationToken);
        if (response[1] != 0x00)
        {
            throw new InvalidDataException("SOCKS5 proxy từ chối username/password.");
        }
    }

    private async Task SendSocks5ConnectAsync(Stream stream, CancellationToken cancellationToken)
    {
        var host = Encoding.ASCII.GetBytes(_egressEndpoint.Host);
        if (host.Length > byte.MaxValue)
        {
            throw new InvalidDataException("Host kiểm tra egress quá dài cho SOCKS5.");
        }

        var port = _egressEndpoint.Port == -1 ? 80 : _egressEndpoint.Port;
        var request = new byte[7 + host.Length];
        request[0] = 0x05;
        request[1] = 0x01;
        request[2] = 0x00;
        request[3] = 0x03;
        request[4] = (byte)host.Length;
        Buffer.BlockCopy(host, 0, request, 5, host.Length);
        request[5 + host.Length] = (byte)(port >> 8);
        request[6 + host.Length] = (byte)(port & 0xFF);

        await stream.WriteAsync(request, cancellationToken);
        var header = await ReadExactAsync(stream, 4, cancellationToken);
        if (header[0] != 0x05)
        {
            throw new InvalidDataException("SOCKS5 CONNECT trả về phiên bản không hợp lệ.");
        }

        if (header[1] != 0x00)
        {
            throw new InvalidDataException($"SOCKS5 CONNECT thất bại: {DescribeSocks5Reply(header[1])}.");
        }

        var addressLength = header[3] switch
        {
            0x01 => 4,
            0x03 => (await ReadExactAsync(stream, 1, cancellationToken))[0],
            0x04 => 16,
            _ => throw new InvalidDataException("SOCKS5 CONNECT trả về address type không hợp lệ.")
        };
        _ = await ReadExactAsync(stream, addressLength + 2, cancellationToken);
    }

    private static string DescribeSocks5Reply(byte reply) =>
        reply switch
        {
            0x01 => "general failure",
            0x02 => "connection not allowed",
            0x03 => "network unreachable",
            0x04 => "host unreachable",
            0x05 => "connection refused",
            0x06 => "TTL expired",
            0x07 => "command not supported",
            0x08 => "address type not supported",
            _ => $"unknown reply 0x{reply:X2}"
        };

    private static async Task WriteAsciiAsync(Stream stream, string value, CancellationToken cancellationToken)
    {
        var bytes = Encoding.ASCII.GetBytes(value);
        await stream.WriteAsync(bytes, cancellationToken);
    }

    private static async Task<byte[]> ReadExactAsync(Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var offset = 0;
        while (offset < length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset, length - offset), cancellationToken);
            if (read == 0)
            {
                throw new InvalidDataException("Kết nối proxy đóng sớm.");
            }

            offset += read;
        }

        return buffer;
    }

    private static async Task<HttpProxyResponse> ReadHttpResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[4096];
        while (true)
        {
            var read = await stream.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            memory.Write(buffer, 0, read);
            if (memory.Length > 128 * 1024)
            {
                throw new InvalidDataException("Phản hồi kiểm tra egress quá lớn.");
            }
        }

        var bytes = memory.ToArray();
        var separator = FindHeaderSeparator(bytes);
        if (separator < 0)
        {
            throw new InvalidDataException("Phản hồi HTTP từ proxy không hợp lệ.");
        }

        var headerText = Encoding.ASCII.GetString(bytes, 0, separator);
        var lines = headerText.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0 || !TryParseStatusCode(lines[0], out var statusCode))
        {
            throw new InvalidDataException("Status line HTTP từ proxy không hợp lệ.");
        }

        var headers = ParseHeaders(lines.Skip(1));
        var bodyOffset = separator + 4;
        var bodyBytes = bytes[bodyOffset..];
        var body = DecodeBody(bodyBytes, headers);
        return new HttpProxyResponse(statusCode, body);
    }

    private static int FindHeaderSeparator(byte[] bytes)
    {
        for (var index = 0; index <= bytes.Length - 4; index++)
        {
            if (bytes[index] == '\r' &&
                bytes[index + 1] == '\n' &&
                bytes[index + 2] == '\r' &&
                bytes[index + 3] == '\n')
            {
                return index;
            }
        }

        return -1;
    }

    private static bool TryParseStatusCode(string statusLine, out int statusCode)
    {
        statusCode = default;
        var parts = statusLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 && int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out statusCode);
    }

    private static Dictionary<string, string> ParseHeaders(IEnumerable<string> lines)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex <= 0)
            {
                continue;
            }

            headers[line[..separatorIndex].Trim()] = line[(separatorIndex + 1)..].Trim();
        }

        return headers;
    }

    private static string DecodeBody(byte[] bodyBytes, IReadOnlyDictionary<string, string> headers)
    {
        if (headers.TryGetValue("Transfer-Encoding", out var transferEncoding) &&
            transferEncoding.Contains("chunked", StringComparison.OrdinalIgnoreCase))
        {
            bodyBytes = DecodeChunkedBody(bodyBytes);
        }

        return Encoding.UTF8.GetString(bodyBytes).Trim();
    }

    private static byte[] DecodeChunkedBody(byte[] bodyBytes)
    {
        using var source = new MemoryStream(bodyBytes);
        using var result = new MemoryStream();
        while (true)
        {
            var line = ReadAsciiLine(source);
            var extensionIndex = line.IndexOf(';');
            var sizeText = extensionIndex >= 0 ? line[..extensionIndex] : line;
            if (!int.TryParse(sizeText.Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var size))
            {
                throw new InvalidDataException("Chunked body không hợp lệ.");
            }

            if (size == 0)
            {
                break;
            }

            var chunk = new byte[size];
            var read = source.Read(chunk, 0, size);
            if (read != size)
            {
                throw new InvalidDataException("Chunked body bị thiếu dữ liệu.");
            }

            result.Write(chunk, 0, chunk.Length);
            _ = ReadAsciiLine(source);
        }

        return result.ToArray();
    }

    private static string ReadAsciiLine(Stream stream)
    {
        using var line = new MemoryStream();
        while (true)
        {
            var current = stream.ReadByte();
            if (current < 0)
            {
                throw new InvalidDataException("HTTP chunk line bị kết thúc sớm.");
            }

            if (current == '\r')
            {
                var next = stream.ReadByte();
                if (next != '\n')
                {
                    throw new InvalidDataException("HTTP chunk line không hợp lệ.");
                }

                return Encoding.ASCII.GetString(line.ToArray());
            }

            line.WriteByte((byte)current);
        }
    }

    private static string ExtractVerifiedEgressIp(HttpProxyResponse response)
    {
        if (response.StatusCode == 407)
        {
            throw new InvalidDataException("HTTP proxy yêu cầu hoặc từ chối xác thực.");
        }

        if (response.StatusCode is < 200 or >= 300)
        {
            throw new InvalidDataException($"Endpoint kiểm tra egress trả về HTTP {response.StatusCode}.");
        }

        var firstLine = response.Body
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?
            .Trim();
        if (!IPAddress.TryParse(firstLine, out _))
        {
            throw new InvalidDataException("Không xác minh được IP egress từ phản hồi proxy.");
        }

        return firstLine;
    }

    private sealed record HttpProxyResponse(int StatusCode, string Body);
}
