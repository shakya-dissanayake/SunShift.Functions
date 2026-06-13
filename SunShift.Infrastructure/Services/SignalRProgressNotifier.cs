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

public class SignalRProgressNotifier : IProgressNotifier
{
    private const string HubName = "ForecastProgressHub";
    private const string SignalRAudience = "https://signalr.azure.com";

    private readonly HttpClient _http;
    private readonly string _endpoint;
    private readonly TokenCredential _credential;
    private readonly ILogger<SignalRProgressNotifier> _logger;

    public SignalRProgressNotifier(
        HttpClient http,
        IConfiguration configuration,
        ILogger<SignalRProgressNotifier> logger)
    {
        _http = http;
        _logger = logger;

        // Data-plane auth is via managed identity (AAD token), so only the SignalR
        // service endpoint URL is needed — no connection string / access key.
        _endpoint = (configuration[ConfigurationKeys.SignalREndpoint]
            ?? throw new InvalidOperationException($"Configuration key '{ConfigurationKeys.SignalREndpoint}' is missing."))
            .TrimEnd('/');

        string? clientId = configuration["ManagedIdentityClientId"];
        _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = clientId
        });
    }

    public async Task NotifyAsync(int userId, string stage, int progressPercent, CancellationToken cancellationToken = default)
    {
        string url = $"{_endpoint}/api/v1/hubs/{HubName}/groups/{userId}";

        string payload = JsonSerializer.Serialize(new
        {
            target = "ForecastProgress",
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
                "SignalRProgressNotifier: failed to send progress (userId={UserId}, stage={Stage}). Status={Status}, Body={Body}",
                userId, stage, (int)resp.StatusCode, body);
        }
    }
}
