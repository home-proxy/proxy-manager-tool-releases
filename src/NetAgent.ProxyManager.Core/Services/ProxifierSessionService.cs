using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Models;

namespace NetAgent.ProxyManager.Core.Services;

public sealed class ProxifierSessionService(
    IProxifierProfileBuilder profileBuilder,
    IProxifierService proxifierService) : IProxifierSessionService
{
    public async Task<ProxifierSessionResult> StartAsync(
        AppSettings settings,
        IReadOnlyList<ProxyServer> proxies,
        IReadOnlyList<ApplicationRule> applications,
        CancellationToken cancellationToken)
    {
        var profile = profileBuilder.Build(new ProxifierProfileModel
        {
            Proxies = proxies,
            Rules = applications,
            DefaultRouteDirect = settings.DefaultRouteDirect
        });

        var profilePath = await WriteProfileAsync(settings.ProfileOutputDirectory, "active.ppx", profile.Xml, cancellationToken);
        var command = await proxifierService.LoadProfileAsync(
            settings.ProxifierExecutablePath,
            profilePath,
            cancellationToken);
        if (command.Success)
        {
            var hideCommand = await proxifierService.HideWindowAsync(settings.ProxifierExecutablePath, cancellationToken);
            return new ProxifierSessionResult(
                hideCommand.Success,
                CombineMessages(command, hideCommand),
                profilePath,
                profile);
        }

        return new ProxifierSessionResult(command.Success, command.Message, profilePath, profile);
    }

    public async Task<ProxifierSessionResult> StopAsync(
        AppSettings settings,
        CancellationToken cancellationToken)
    {
        var profile = profileBuilder.Build(new ProxifierProfileModel
        {
            Proxies = [],
            Rules = [],
            DefaultRouteDirect = true
        });

        var profilePath = await WriteProfileAsync(settings.ProfileOutputDirectory, "inactive.ppx", profile.Xml, cancellationToken);
        var command = await proxifierService.LoadProfileAsync(
            settings.ProxifierExecutablePath,
            profilePath,
            cancellationToken);
        await Task.Delay(500, cancellationToken);
        var stopCommand = await proxifierService.StopProcessesAsync(settings.ProxifierExecutablePath, cancellationToken);

        return new ProxifierSessionResult(
            stopCommand.Success,
            CombineMessages(command, stopCommand),
            profilePath,
            profile);
    }

    private static string CombineMessages(ProxifierCommandResult first, ProxifierCommandResult second) =>
        string.Equals(first.Message, second.Message, StringComparison.Ordinal)
            ? first.Message
            : $"{first.Message} {second.Message}";

    private static async Task<string> WriteProfileAsync(
        string outputDirectory,
        string fileName,
        string xml,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(outputDirectory);
        var profilePath = Path.Combine(outputDirectory, fileName);
        await File.WriteAllTextAsync(profilePath, xml, cancellationToken);
        return profilePath;
    }
}
