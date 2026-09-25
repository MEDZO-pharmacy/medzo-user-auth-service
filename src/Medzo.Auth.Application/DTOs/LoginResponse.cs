using System.Text.Json.Serialization;

namespace Medzo.Auth.Application.DTOs;

public class LoginResponse
{
    public string Token { get; set; } = string.Empty;

    // Transported only in an HttpOnly cookie by the API. Keeping it on the
    // application response lets the service remain independent of HTTP.
    [JsonIgnore]
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public UserResponse User { get; set; } = null!;
}
