using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Application.Models.Notifications;
using SunShift.Domain.Common.Constants;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SunShift.Infrastructure.Services;

/// <summary>
/// Firebase Cloud Messaging HTTP v1 client. Mints a Google OAuth2 access token from the
/// service-account JSON (cached until shortly before expiry) and posts to the v1 send endpoint.
/// One Firebase project serves both Android and iOS (APNs is configured inside Firebase).
/// </summary>
public class FcmClient : IFcmClient
{
    private const string GoogleTokenUrl = "https://oauth2.googleapis.com/token";
    private const string FcmScope = "https://www.googleapis.com/auth/firebase.messaging";

    private readonly HttpClient _http;
    private readonly ILogger<FcmClient> _logger;

    private readonly string _projectId;
    private readonly string _clientEmail;
    private readonly RSA _privateKey;

    // Cached access token shared across requests; refreshed when within 60s of expiry.
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public FcmClient(HttpClient http, IConfiguration configuration, ILogger<FcmClient> logger)
    {
        _http = http;
        _logger = logger;

        string json = configuration[ConfigurationKeys.FcmServiceAccountJson]
            ?? throw new InvalidOperationException(
                $"Configuration key '{ConfigurationKeys.FcmServiceAccountJson}' is missing.");

        using JsonDocument doc = JsonDocument.Parse(json);
        JsonElement root = doc.RootElement;

        _projectId = root.GetProperty("project_id").GetString()
            ?? throw new InvalidOperationException("Service account JSON is missing 'project_id'.");
        _clientEmail = root.GetProperty("client_email").GetString()
            ?? throw new InvalidOperationException("Service account JSON is missing 'client_email'.");
        string privateKeyPem = root.GetProperty("private_key").GetString()
            ?? throw new InvalidOperationException("Service account JSON is missing 'private_key'.");

        _privateKey = RSA.Create();
        _privateKey.ImportFromPem(privateKeyPem);
    }

    public async Task<PushResult> SendAsync(
        string deviceToken,
        PushMessage message,
        CancellationToken cancellationToken = default)
    {
        string accessToken;
        try
        {
            accessToken = await GetAccessTokenAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FcmClient: failed to obtain Google access token.");
            return PushResult.Error;
        }

        string payload = BuildMessagePayload(deviceToken, message);

        string url = $"https://fcm.googleapis.com/v1/projects/{_projectId}/messages:send";
        using HttpRequestMessage req = new(HttpMethod.Post, url)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage resp;
        try
        {
            resp = await _http.SendAsync(req, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "FcmClient: transport error sending push.");
            return PushResult.Error;
        }

        using (resp)
        {
            if (resp.IsSuccessStatusCode)
            {
                return PushResult.Success;
            }

            string body = await resp.Content.ReadAsStringAsync(cancellationToken);

            // FCM returns 404 NOT_FOUND or 400 with UNREGISTERED/INVALID_ARGUMENT for dead tokens.
            if (resp.StatusCode == HttpStatusCode.NotFound || IsUnregistered(body))
            {
                _logger.LogInformation("FcmClient: token reported invalid by FCM — will be pruned.");
                return PushResult.TokenInvalid;
            }

            _logger.LogWarning(
                "FcmClient: send failed. Status={Status}, Body={Body}", (int)resp.StatusCode, body);
            return PushResult.Error;
        }
    }

    private static bool IsUnregistered(string body)
        => body.Contains("UNREGISTERED", StringComparison.Ordinal)
        || body.Contains("\"NOT_FOUND\"", StringComparison.Ordinal);

    private string BuildMessagePayload(string deviceToken, PushMessage message)
    {
        // FCM v1 "data" values must all be strings. Notification gives the OS-rendered banner;
        // data carries routing info the app reads on tap / for the Cancel action.
        var fcm = new
        {
            message = new
            {
                token = deviceToken,
                notification = new
                {
                    title = message.Title,
                    body = message.Body
                },
                data = message.Data,
                android = new
                {
                    priority = "high"
                },
                apns = new
                {
                    headers = new Dictionary<string, string> { ["apns-priority"] = "10" },
                    payload = new { aps = new { sound = "default" } }
                }
            }
        };

        return JsonSerializer.Serialize(fcm);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt.AddSeconds(-60))
        {
            return _accessToken;
        }

        await _tokenLock.WaitAsync(cancellationToken);
        try
        {
            // Re-check after acquiring the lock — another caller may have refreshed it.
            if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt.AddSeconds(-60))
            {
                return _accessToken;
            }

            string assertion = BuildSignedJwt();

            using HttpRequestMessage req = new(HttpMethod.Post, GoogleTokenUrl)
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
                    ["assertion"] = assertion
                })
            };

            using HttpResponseMessage resp = await _http.SendAsync(req, cancellationToken);
            string body = await resp.Content.ReadAsStringAsync(cancellationToken);

            if (!resp.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    $"Google token endpoint returned {(int)resp.StatusCode}: {body}");
            }

            using JsonDocument doc = JsonDocument.Parse(body);
            JsonElement root = doc.RootElement;
            string token = root.GetProperty("access_token").GetString()
                ?? throw new InvalidOperationException("Google token response has no access_token.");
            int expiresIn = root.TryGetProperty("expires_in", out JsonElement exp) ? exp.GetInt32() : 3600;

            _accessToken = token;
            _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
            return token;
        }
        finally
        {
            _ = _tokenLock.Release();
        }
    }

    private string BuildSignedJwt()
    {
        long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        string header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            alg = "RS256",
            typ = "JWT"
        }));

        string claims = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = _clientEmail,
            scope = FcmScope,
            aud = GoogleTokenUrl,
            iat = now,
            exp = now + 3600
        }));

        string signingInput = $"{header}.{claims}";
        byte[] signature = _privateKey.SignData(
            Encoding.ASCII.GetBytes(signingInput),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64Url(signature)}";
    }

    private static string Base64Url(byte[] bytes)
        => Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
