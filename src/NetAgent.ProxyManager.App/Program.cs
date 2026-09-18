using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NetAgent.ProxyManager.App.Controls;
using NetAgent.ProxyManager.App.Forms;
using NetAgent.ProxyManager.App.Theme;
using NetAgent.ProxyManager.Core.Interfaces;
using NetAgent.ProxyManager.Core.Services;
using NetAgent.ProxyManager.Infrastructure.Api;
using NetAgent.ProxyManager.Infrastructure.Logging;
using NetAgent.ProxyManager.Infrastructure.Services;
using NetAgent.ProxyManager.Infrastructure.Storage;

namespace NetAgent.ProxyManager.App;

internal static class Program
{
    private const string EnvironmentVariablePrefix = "PROXYMANAGER_";

    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Any(arg => string.Equals(arg, "--prepare-branding", StringComparison.OrdinalIgnoreCase)))
        {
            AppBranding.PrepareBranding(updateShortcuts: true);
            return;
        }

        AppBranding.PrepareBranding(updateShortcuts: false);
        Application.Idle += (_, _) => AppBranding.ApplyApplicationIconToOpenForms();

        MaterializeBundledProxifierInstaller();

        var environmentName = ResolveConfiguredEnvironmentName();

        using var host = Host.CreateDefaultBuilder()
            .UseEnvironment(environmentName)
            .ConfigureAppConfiguration((_, configuration) =>
            {
                configuration.Sources.Clear();
                AddAppConfiguration(configuration, environmentName);
            })
            .ConfigureServices((context, services) =>
            {
                services.Configure<BackendApiOptions>(options =>
                {
                    context.Configuration.GetSection("BackendApi").Bind(options);
                });
                services.Configure<ProxifierOptions>(context.Configuration.GetSection("Proxifier"));

                services.AddSingleton<AppDataPaths>();
                services.AddSingleton<ICredentialProtector, DpapiCredentialProtector>();
                services.AddSingleton<ILocalSecretStore, SecureLocalSecretStore>();
                services.AddSingleton<IProxyRepository, JsonProxyRepository>();
                services.AddSingleton<IApplicationRuleRepository, JsonApplicationRuleRepository>();
                services.AddSingleton<IAppSettingsRepository, JsonAppSettingsRepository>();
                services.AddSingleton<IAuthService, AuthService>();
                services.AddSingleton<IProxyChecker, ProxyChecker>();
                services.AddSingleton<IBulkProxyParser, BulkProxyParser>();
                services.AddSingleton<RoundRobinProxyAssignmentService>();
                services.AddSingleton<ProxyManagementService>();
                services.AddSingleton<IProxyOrderCacheService, ProxyOrderCacheService>();
                services.AddSingleton<ExpiredBackendProxyAssignmentService>();
                services.AddSingleton<ApplicationRuleService>();
                services.AddSingleton<IApplicationRuntimeController, ApplicationRuntimeController>();
                services.AddSingleton<ManualProxyRotateGuard>();
                services.AddSingleton<IProxifierProfileBuilder, ProxifierProfileBuilder>();
                services.AddSingleton<IProxifierService, ProxifierService>();
                services.AddSingleton<IInstallerProcessRunner, ElevatedInstallerProcessRunner>();
                services.AddSingleton<IProxifierOwnershipStore, RegistryProxifierOwnershipStore>();
                services.AddSingleton<IProxifierInstallerService, ProxifierInstallerService>();
                services.AddSingleton<IProxifierLicenseStore, RegistryProxifierLicenseStore>();
                services.AddSingleton<IProxifierRegistrationService, ProxifierRegistrationService>();
                services.AddSingleton<IProxifierPreferenceStore, RegistryProxifierPreferenceStore>();
                services.AddSingleton<IProxifierPreferenceService, ProxifierPreferenceService>();
                services.AddSingleton<IProxifierSessionService, ProxifierSessionService>();
                services.AddSingleton<IAppProcessScanner, AppProcessScanner>();
                services.AddSingleton<IEmulatorProcessScanner, EmulatorProcessScanner>();
                services.AddSingleton<ILoggerSanitizer, LoggerSanitizer>();
                services.AddSingleton<FileLogger>();

                services.AddTransient<MerchantHeaderHandler>();
                services.AddTransient<BearerTokenHandler>();
                services.AddSingleton<MockProxyApiClient>();
                services.AddHttpClient<IAuthApiClient, HomeProxyAuthApiClient>((provider, client) =>
                {
                    ConfigureBackendClient(provider, client);
                }).AddHttpMessageHandler<MerchantHeaderHandler>();
                services.AddHttpClient<HttpProxyApiClient>((provider, client) =>
                {
                    ConfigureBackendClient(provider, client);
                    // Never disable TLS certificate validation in production. In particular, do not use:
                    // ServerCertificateCustomValidationCallback = (_, _, _, _) => true
                })
                    .AddHttpMessageHandler<MerchantHeaderHandler>()
                    .AddHttpMessageHandler<BearerTokenHandler>();
                services.AddHttpClient<IProxyOrderApiClient, HomeProxyOrderApiClient>((provider, client) =>
                {
                    ConfigureBackendClient(provider, client);
                })
                    .AddHttpMessageHandler<MerchantHeaderHandler>()
                    .AddHttpMessageHandler<BearerTokenHandler>();
                services.AddHttpClient<IDepositApiClient, HomeProxyDepositApiClient>((provider, client) =>
                {
                    ConfigureBackendClient(provider, client);
                })
                    .AddHttpMessageHandler<MerchantHeaderHandler>()
                    .AddHttpMessageHandler<BearerTokenHandler>();
                services.AddSingleton<IProxyApiClient>(provider =>
                {
                    var options = provider.GetRequiredService<IOptions<BackendApiOptions>>().Value;
                    return string.Equals(options.Mode, "Http", StringComparison.OrdinalIgnoreCase)
                        ? provider.GetRequiredService<HttpProxyApiClient>()
                        : provider.GetRequiredService<MockProxyApiClient>();
                });

                services.AddTransient<LoginForm>();
                services.AddTransient<AddApplicationRuleForm>();
                services.AddTransient<AddProxyForm>();
                services.AddTransient<ProxyEditorForm>();
                services.AddTransient<BulkImportProxyForm>();
                services.AddTransient<ApplicationRuleEditorForm>();
                services.AddTransient<ChangeProxyInfoForm>();
                services.AddTransient<ChangeApplicationProxyForm>();
                services.AddTransient<RotateProxyInfoForm>();
                services.AddTransient<FetchProxyFromKeyForm>();
                services.AddTransient<RenewProxyOrdersForm>();
                services.AddTransient<DepositAmountForm>();
                services.AddTransient<ProxyListControl>();
                services.AddTransient<ProxyOrdersControl>();
                services.AddTransient<ProductPurchaseControl>();
                services.AddTransient<ApplicationRulesControl>();
                services.AddTransient<StatusBarControl>();
                services.AddTransient<MainForm>();
            })
            .Build();

        using var mainForm = host.Services.GetRequiredService<MainForm>();
        Application.Run(mainForm);
    }

    private static string ResolveConfiguredEnvironmentName()
    {
        var builder = new ConfigurationBuilder().SetBasePath(AppContext.BaseDirectory);
        AddEmbeddedJson(builder, "appsettings.json");
        builder.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        builder.AddEnvironmentVariables(prefix: EnvironmentVariablePrefix);
        var configuration = builder.Build();

        return BackendApiEnvironmentResolver.ResolveEnvironmentName(
            configuration["Environment"],
            configuration["ENVIRONMENT"]);
    }

    // Embedded JSON is the guaranteed base so the single-file exe runs standalone.
    // On-disk appsettings files (installer/dev) are layered on top as optional overrides.
    private static IConfigurationBuilder AddAppConfiguration(IConfigurationBuilder configuration, string environmentName)
    {
        configuration.SetBasePath(AppContext.BaseDirectory);
        AddEmbeddedJson(configuration, "appsettings.json");
        AddEmbeddedJson(configuration, $"appsettings.{environmentName}.json");
        configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
        configuration.AddJsonFile($"appsettings.{environmentName}.json", optional: true, reloadOnChange: true);
        configuration.AddEnvironmentVariables(prefix: EnvironmentVariablePrefix);
        return configuration;
    }

    private static void AddEmbeddedJson(IConfigurationBuilder configuration, string logicalName)
    {
        var stream = EmbeddedAssets.Open(logicalName);
        if (stream is not null)
        {
            configuration.AddJsonStream(stream);
        }
    }

    // The standalone exe carries ProxifierSetup.exe as an embedded resource. Extract it to
    // %AppData%\ProxyManager\Installers once so ProxifierInstallerService can run it silently,
    // then point Proxifier:BundledInstallerPath at that location via the env-var override.
    private static void MaterializeBundledProxifierInstaller()
    {
        try
        {
            using var resource = EmbeddedAssets.Open("Installers/ProxifierSetup.exe");
            if (resource is null)
            {
                return;
            }

            var installersDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProxyManager",
                "Installers");
            Directory.CreateDirectory(installersDirectory);

            var targetPath = Path.Combine(installersDirectory, "ProxifierSetup.exe");
            if (!File.Exists(targetPath) || new FileInfo(targetPath).Length != resource.Length)
            {
                using var target = File.Create(targetPath);
                resource.CopyTo(target);
            }

            Environment.SetEnvironmentVariable(
                EnvironmentVariablePrefix + "Proxifier__BundledInstallerPath",
                targetPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            // Non-fatal: app still runs; Proxifier can be installed/configured manually later.
        }
    }

    private static void ConfigureBackendClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<BackendApiOptions>>().Value;
        client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        client.DefaultRequestHeaders.TryAddWithoutValidation("accept", "application/json");
    }

}
