namespace SchoolManagement.Infrastructure.Security;

/// <summary>Bound from the "Jwt" section of appsettings.json.</summary>
public class JwtSettings
{
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string SigningKey { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;
}