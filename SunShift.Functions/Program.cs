using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Functions.Functions.Activities;
using SunShift.Functions.Services;
using SunShift.Infrastructure;
using SunShift.Infrastructure.Repositories;
using SunShift.Infrastructure.Services;

namespace SunShift.Functions
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            IHost host = new HostBuilder()
                .ConfigureAppConfiguration(config =>
                {
                    _ = config.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                        .AddEnvironmentVariables();

                    IConfigurationRoot settings = config.Build();
                    string endpoint = settings["Endpoints:AppConfiguration"]!;
                    string configPrefix = settings["AppConfigPrefix"] ?? string.Empty;
                    string env = settings["Env"] ?? string.Empty;
                    string? clientId = settings["ManagedIdentityClientId"];

                    _ = config.AddAzureAppConfiguration(options =>
                    {
                        _ = options.Connect(new Uri(endpoint), new DefaultAzureCredential(
                                new DefaultAzureCredentialOptions
                                {
                                    ManagedIdentityClientId = clientId
                                }))
                            .Select(configPrefix + "*", LabelFilter.Null)
                            .Select(configPrefix + "*", env)
                            .ConfigureKeyVault(kvOptions =>
                            {
                                _ = kvOptions.SetCredential(new DefaultAzureCredential(
                                    new DefaultAzureCredentialOptions
                                    {
                                        ManagedIdentityClientId = clientId
                                    }));
                            })
                            .ConfigureRefresh(refreshOptions => refreshOptions.RegisterAll());
                    });
                })
                .ConfigureFunctionsWorkerDefaults(worker =>
                {
                    _ = worker.Services.AddAzureAppConfiguration();
                    _ = worker.UseAzureAppConfiguration();
                })
                .ConfigureServices((ctx, services) =>
                {
                    services.AddInfrastructure(ctx.Configuration);
                    _ = services.AddHttpClient<IWeatherService, WeatherService>();
                    _ = services.AddSingleton<ISolarForecastService, SolarForecastService>();
                    _ = services.AddSingleton<IScheduleService, ScheduleService>();

                    _ = services.AddSingleton<ITokenProtector, AesGcmTokenProtector>();
                    _ = services.AddScoped<ISmartThingsIntegrationRepository, SmartThingsIntegrationRepository>();
                    _ = services.AddHttpClient<ISmartThingsClient, SmartThingsClient>();
                    _ = services.AddHttpClient<IProgressNotifier, SignalRProgressNotifier>();
                    _ = services.AddHttpClient<IScheduleProgressNotifier, SignalRScheduleProgressNotifier>();

                    // Push notifications (FCM HTTP v1). FcmClient caches its Google access token in
                    // instance fields, so it must be a singleton; it pulls a pooled HttpClient from
                    // IHttpClientFactory rather than being a transient typed client.
                    _ = services.AddHttpClient(nameof(FcmClient));
                    _ = services.AddSingleton<IFcmClient>(sp => new FcmClient(
                        sp.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(FcmClient)),
                        sp.GetRequiredService<IConfiguration>(),
                        sp.GetRequiredService<ILogger<FcmClient>>()));
                    _ = services.AddScoped<INotificationSender, NotificationSender>();

                    // Register activity classes so they can be resolved for on-demand invocations
                    _ = services.AddScoped<GenerateForecastsActivity>();
                    _ = services.AddScoped<GenerateSchedulesActivity>();
                })
                .Build();

            host.Run();
        }
    }
}
