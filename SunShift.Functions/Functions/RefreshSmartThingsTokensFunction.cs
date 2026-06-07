using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using SunShift.Application.Common.Interfaces.Repositories;
using SunShift.Application.Common.Interfaces.Services;
using SunShift.Domain.Entities;

namespace SunShift.Functions.Functions;

public class RefreshSmartThingsTokensFunction(
    ISmartThingsIntegrationRepository integrationRepository,
    ISmartThingsClient smartThingsClient,
    ITokenProtector tokenProtector,
    IUnitOfWork unitOfWork,
    ILogger<RefreshSmartThingsTokensFunction> logger)
{
    // Run every hour. Refreshes tokens expiring within the next 2 hours so the dispatcher
    // always has a valid token ready.
    [Function(nameof(RefreshSmartThingsTokensFunction))]
    public async Task RunAsync([TimerTrigger("0 0 * * * *")] TimerInfo timer, CancellationToken cancellationToken)
    {
        DateTime threshold = DateTime.UtcNow.AddHours(2);

        List<SmartThingsIntegration> expiring =
            await integrationRepository.GetActiveExpiringSoonAsync(threshold, cancellationToken);

        if (expiring.Count == 0)
        {
            logger.LogDebug("RefreshSmartThingsTokensFunction: no tokens expiring within threshold.");
            return;
        }

        logger.LogInformation("RefreshSmartThingsTokensFunction: refreshing {Count} token(s).", expiring.Count);

        int succeeded = 0;
        int failed = 0;

        foreach (SmartThingsIntegration integration in expiring)
        {
            try
            {
                if (string.IsNullOrEmpty(integration.RefreshTokenEncrypted))
                {
                    logger.LogWarning(
                        "RefreshSmartThingsTokensFunction: user {UserId} has no refresh token — marking Inactive.",
                        integration.UserId);
                    integration.Status = SmartThingsIntegrationStatus.Inactive;
                    failed++;
                    continue;
                }

                string refreshToken = tokenProtector.Unprotect(integration.RefreshTokenEncrypted);
                Application.Models.SmartThings.SmartThingsTokenResponse tokens =
                    await smartThingsClient.RefreshAsync(refreshToken, cancellationToken);

                integration.AccessTokenEncrypted = tokenProtector.Protect(tokens.AccessToken);

                // SmartThings rotates refresh tokens — keep the new one if provided.
                if (!string.IsNullOrEmpty(tokens.RefreshToken))
                {
                    integration.RefreshTokenEncrypted = tokenProtector.Protect(tokens.RefreshToken);
                }

                integration.AccessTokenExpiresAtUtc = DateTime.UtcNow.AddSeconds(tokens.ExpiresIn - 60);
                integration.Status = SmartThingsIntegrationStatus.Active;
                succeeded++;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "RefreshSmartThingsTokensFunction: failed to refresh token for user {UserId} — marking Inactive.",
                    integration.UserId);
                integration.Status = SmartThingsIntegrationStatus.Inactive;
                failed++;
            }
        }

        _ = await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "RefreshSmartThingsTokensFunction: completed. Succeeded={Succeeded}, Failed={Failed}.",
            succeeded, failed);
    }
}
