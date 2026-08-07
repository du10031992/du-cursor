using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using MepPanel.LicenseServer.Models;
using Microsoft.IdentityModel.Tokens;

namespace MepPanel.LicenseServer.Services;

public class JwtTokenService
{
    private readonly IConfiguration _configuration;

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateAccessToken(User user)
    {
        var key = _configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Key.");
        var issuer = _configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Issuer.");
        var audience = _configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("Chưa cấu hình Jwt:Audience.");

        var minutes = int.TryParse(
            _configuration["Jwt:AccessTokenMinutes"],
            out var parsed)
            ? parsed
            : 60;

        var expires = DateTime.UtcNow.AddMinutes(minutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.PhoneNumber),
            new("phoneNumber", user.PhoneNumber),
            new(ClaimTypes.Name, user.DisplayName ?? user.PhoneNumber),
            new(ClaimTypes.Role, user.Role ?? "User")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
