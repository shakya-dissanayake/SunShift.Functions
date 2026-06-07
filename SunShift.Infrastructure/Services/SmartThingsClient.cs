using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Application.Models.SmartThings;
using SunShift.Domain.Common.Constants;
using System.Text;

namespace SunShift.Infrastructure.Services;

public class SmartThingsClient : ISmartThingsClient
{
    private const string ApiBase = "https://api.smartthings.com";
    private const string TokenUrl = "https://api.smartthings.com/oauth/token";

    private readonly HttpClient _http;
    private readonly string _clientId;
    private readonly string _clientSecret;

    public SmartThingsClient(HttpClient http, IConfiguration configuration)
    {
        _http = http;
        _clientId = configuration[ConfigurationKeys.SmartThingsClientId]
            ?? throw new InvalidOperationException($"Configuration key '{ConfigurationKeys.SmartThingsClientId}' is not set.");
        _clientSecret = configuration[ConfigurationKeys.SmartThingsClientSecret]
            ?? throw new InvalidOperationException($"Configuration key '{ConfigurationKeys.SmartThingsClientSecret}' is not set.");
    }

    public Task<SmartThingsTokenResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default)
    {
        Dictionary<string, string> form = new()
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = _clientId
        };
        return PostTokenAsync(form, cancellationToken);
    }

    public async Task SendCommandAsync(string accessToken, string deviceId, string command, CancellationToken cancellationToken = default)
    {
        string body = $"{{\"commands\":[{{\"component\":\"main\",\"capability\":\"switch\",\"command\":\"{command}\"}}]}}";
        using HttpRequestMessage req = new(HttpMethod.Post, $"{ApiBase}/v1/devices/{deviceId}/commands")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage resp = await _http.SendAsync(req, cancellationToken);
        resp.EnsureSuccessStatusCode();
    }

    private async Task<SmartThingsTokenResponse> PostTokenAsync(Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        string credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));

        using HttpRequestMessage req = new(HttpMethod.Post, TokenUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };
        req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", credentials);
        req.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        HttpResponseMessage resp = await _http.SendAsync(req, cancellationToken);
        string responseBody = await resp.Content.ReadAsStringAsync(cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"SmartThings token endpoint returned {(int)resp.StatusCode}: {responseBody}");
        }

        JObject parsed = JObject.Parse(responseBody);
        return new SmartThingsTokenResponse
        {
            AccessToken = parsed["access_token"]?.ToString() ?? string.Empty,
            RefreshToken = parsed["refresh_token"]?.ToString(),
            ExpiresIn = parsed["expires_in"]?.Value<int>() ?? 0,
            Scope = parsed["scope"]?.ToString()
        };
    }
}
