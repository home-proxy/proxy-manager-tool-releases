using FluentAssertions;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ApplicationRuntimeControllerTests
{
    [Fact]
    public async Task StartAsync_ShouldRejectEmulatorRules()
    {
        var controller = new ApplicationRuntimeController();

        var result = await controller.StartAsync(
            new ApplicationRule { TargetType = ApplicationTargetType.Emulator },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Chưa hỗ trợ");
    }

    [Fact]
    public async Task StopAsync_ShouldRejectInvalidExecutablePath()
    {
        var controller = new ApplicationRuntimeController();

        var result = await controller.StopAsync(
            new ApplicationRule
            {
                TargetType = ApplicationTargetType.Executable,
                ExecutableName = "not-a-valid-target"
            },
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain(".exe");
    }

    [Fact]
    public async Task RestartIfRunningAsync_ShouldSkipExecutableThatIsNotRunning()
    {
        var controller = new ApplicationRuntimeController();
        var path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.exe");

        try
        {
            await File.WriteAllTextAsync(path, string.Empty);

            var result = await controller.RestartIfRunningAsync(
                new ApplicationRule
                {
                    TargetType = ApplicationTargetType.Executable,
                    ExecutableName = path
                },
                CancellationToken.None);

            result.Success.Should().BeTrue();
            result.Message.Should().Contain("không cần mở lại");
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
