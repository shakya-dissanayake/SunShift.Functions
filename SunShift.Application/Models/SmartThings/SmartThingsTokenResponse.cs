namespace SunShift.Application.Models.SmartThings;

public class SmartThingsTokenResponse
{
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public int ExpiresIn { get; set; }
    public string? Scope { get; set; }
}
