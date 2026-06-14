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
    private const string HubName = "ScheduleProgressHub";
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

        _endpoint = (configuration[ConfigurationKeys.SignalREndpoint]
            ?? throw new InvalidOperationException($"Configuration key '{ConfigurationKeys.SignalREndpoint}' is missing."))
            .TrimEnd('/');

        string? clientId = configuration["ManagedIdentityClientId"];
        _credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
        {
            ManagedIdentityClientId = clientId
        });
    }

    public Task NotifyAsync(int userId, string stage, int progressPercent, CancellationToken cancellationToken = default)
    {
        object argument = new { stage, progressPercent };
        return SendToGroupAsync(userId, "ScheduleProgress", argument,
            $"progress (stage={stage})", cancellationToken);
    }

    public Task NotifyDeviceStatusAsync(
        int userId,
        int scheduleId,
        int workloadId,
        string deviceId,
        string status,
        string scheduleStatus,
        CancellationToken cancellationToken = default)
    {
        object argument = new { scheduleId, workloadId, deviceId, status, scheduleStatus };
        return SendToGroupAsync(userId, "DeviceStatus", argument,
            $"device status (scheduleId={scheduleId}, status={status})", cancellationToken);
    }

    private async Task SendToGroupAsync(
        int userId,
        string target,
        object argument,
        string description,
        CancellationToken cancellationToken)
    {
        string url = $"{_endpoint}/api/v1/hubs/{HubName}/groups/{userId}";

        string payload = JsonSerializer.Serialize(new
        {
            target,
            arguments = new[] { argument }
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
                "SignalRScheduleProgressNotifier: failed to send {Description} to userId={UserId}. Status={Status}, Body={Body}",
                description, userId, (int)resp.StatusCode, body);
        }
    }
}
