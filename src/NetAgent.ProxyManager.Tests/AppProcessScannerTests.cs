using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Versioning;
using FluentAssertions;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

[SupportedOSPlatform("windows")]
public sealed class AppProcessScannerTests
{
    [Fact]
    public async Task GetRunningProcessesAsync_ShouldReturnCurrentProcessExecutablePath()
    {
        var scanner = new AppProcessScanner();
        using var currentProcess = Process.GetCurrentProcess();

        var processes = await scanner.GetRunningProcessesAsync(CancellationToken.None);
        var current = processes.FirstOrDefault(process => process.ProcessId == currentProcess.Id);

        current.Should().NotBeNull();
        current!.ExecutablePath.Should().NotBeNullOrWhiteSpace();
        current.ExecutablePath.Should().EndWith(".exe");
    }

    [Fact]
    public async Task GetRunningProcessesAsync_ShouldMarkProcessWithNetworkActivity()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var scanner = new AppProcessScanner();
        using var currentProcess = Process.GetCurrentProcess();

        var processes = await scanner.GetRunningProcessesAsync(CancellationToken.None);
        var current = processes.FirstOrDefault(process => process.ProcessId == currentProcess.Id);

        current.Should().NotBeNull();
        current!.HasNetworkActivity.Should().BeTrue();
    }
}
