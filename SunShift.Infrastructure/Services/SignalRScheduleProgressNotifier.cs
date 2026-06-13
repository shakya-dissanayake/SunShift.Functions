using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Domain.Common.Constants;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SunShift.Infrastructure.Services;

public class SignalRScheduleProgressNotifier : IScheduleProgressNotifier
{
    private const string HubName = "scheduleProgress";
    private const string SignalRAudience = "https://signalr.azure.com";

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly TokenCredential _credential;
    private readonly ILogger<SignalRScheduleProgressNotifier> _logger;

    public SignalRScheduleProgressNotifier(
        HttpClient http,
        IConfiguration configuration,
        ILogger<SignalRScheduleProgressNotifier> logger)
    {
        _http = http;
        _logger = logger;

        string connectionString = configuration[ConfigurationKeys.SignalRConnectionString]
            ?? throw new InvalidOperationException($"Configuration key '{ConfigurationKeys.SignalRConnectionString}' is missing.");

        _endpoint = ParseEndpoint(connectionString);

        string? clientId = configuration["ManagedIdentityClientId"];
        _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = clientId
        });
    }

    public async Task NotifyAsync(int userId, string stage, int progressPercent, CancellationToken cancellationToken = default)
    {
        // Azure SignalR REST: POST {endpoint}/api/v1/hubs/{hub}/users/{userId}
        string url = $"{_endpoint}/api/v1/hubs/{HubName}/users/{userId}";

        string payload = JsonSerializer.Serialize(new
        {
            target = "ScheduleProgress",
            arguments = new object[] { new { stage, progressPercent } }
        });

        AccessToken token = await _credential.GetTokenAsync(
            new TokenRequestContext([SignalRAudience + "/.default"]),
            cancellationToken);

        using HttpRequestMessage req = new(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

        using HttpResponseMessage resp = await _http.SendAsync(req, cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            string body = await resp.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "SignalRScheduleProgressNotifier: failed to send progress (userId={UserId}, stage={Stage}). Status={Status}, Body={Body}",
                userId, stage, (int)resp.StatusCode, body);
        }
    }

    private static string ParseEndpoint(string connectionString)
    {
        // Connection string format: Endpoint=https://...;AccessKey=...;Version=1.0;
        foreach (string part in connectionString.Split(';'))
        {
            if (part.StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                return part["Endpoint=".Length..].TrimEnd('/');
            }
        }

        throw new InvalidOperationException("SignalR connection string does not contain an Endpoint.");
    }
}
