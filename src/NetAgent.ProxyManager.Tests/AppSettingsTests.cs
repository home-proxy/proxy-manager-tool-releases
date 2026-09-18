using FluentAssertions;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.Core.Models;
using NetAgent.ProxyManager.Infrastructure.Api;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.Tests;

public sealed class AppSettingsTests
{
    [Fact]
    public void AppSettings_ShouldDefaultProxyRestartPreferenceToAsk()
    {
        var settings = new AppSettings();

        settings.ProxyRestartConfirmationPreference.Should().Be(ProxyRestartConfirmationPreference.Ask);
    }

    [Fact]
    public async Task JsonAppSettingsRepository_ShouldHandleConcurrentSaves()
    {
        var root = Path.Combine(Path.GetTempPath(), $"netagent-settings-{Guid.NewGuid():N}");
        try
        {
            var paths = new AppDataPaths(root);
            var repository = new JsonAppSettingsRepository(paths, Options.Create(new ProxifierOptions()));
            var saves = Enumerable.Range(0, 12)
                .Select(index => repository.SaveAsync(new AppSettings
                {
                    ProxifierExecutablePath = $"proxifier-{index}.exe",
                    ProfileOutputDirectory = root,
                    DefaultRouteDirect = true,
                    ProxyRestartConfirmationPreference = index % 2 == 0
                        ? ProxyRestartConfirmationPreference.AutoRestart
                        : ProxyRestartConfirmationPreference.SkipRestart
                }, CancellationToken.None));

            await Task.WhenAll(saves);
            var settings = await repository.GetAsync(CancellationToken.None);

            settings.ProxifierExecutablePath.Should().StartWith("proxifier-");
            Enum.IsDefined(settings.ProxyRestartConfirmationPreference).Should().BeTrue();
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
