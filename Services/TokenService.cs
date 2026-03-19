using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Leafy_Library.Models;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Leafy_Library.Services;

public class TokenService
{
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    private SymmetricSecurityKey SigningKey =>
        new(Encoding.UTF8.GetBytes(_jwtSettings.Secret));

    public string CreateToken(User user)
    {
        ArgumentNullException.ThrowIfNull(user);

        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id!),
            new("username", user.Name),
            new("isAdmin", (user.IsAdmin ?? false).ToString().ToLower()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (user.IsAdmin == true)
        {
            claims.Add(new Claim("role", "Admin"));
        }

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(_jwtSettings.ExpiryInDays),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false
        };

        try
        {
            var principal = handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = SigningKey,
                ValidateIssuer = true,
                ValidIssuer = _jwtSettings.Issuer,
                ValidateAudience = true,
                ValidAudience = _jwtSettings.Audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
                NameClaimType = "username",
                RoleClaimType = "role"
            }, out _);

            return principal;
        }
        catch
        {
            return null;
        }
    }
}
