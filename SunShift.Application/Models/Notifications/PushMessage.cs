namespace SunShift.Application.Models.Notifications;

/// <summary>A single push notification to deliver to one device token.</summary>
public class PushMessage
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string> Data { get; set; } = [];
}

/// <summary>Outcome of delivering a <see cref="PushMessage"/> to a single token.</summary>
public enum PushResult
{
    /// <summary>FCM accepted the message.</summary>
    Success,

    /// <summary>FCM reports the token is no longer valid (UNREGISTERED / invalid) — prune it.</summary>
    TokenInvalid,

    /// <summary>A transient or unexpected error; the token is kept and may be retried next scan.</summary>
    Error,
}
