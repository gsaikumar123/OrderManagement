using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OrderManagement.Api.Models;

namespace OrderManagement.Api.Services;

public class TokenService(IConfiguration cfg)
{
    public (string Token, DateTime Expires) Create(ApplicationUser user, string role)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(cfg["Jwt:Key"]!));
        var expires = DateTime.UtcNow.AddMinutes(cfg.GetValue("Jwt:ExpiryMinutes", 60));
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim("name", user.FullName),
            new Claim("role", role)
        };
        var jwt = new JwtSecurityToken(cfg["Jwt:Issuer"], cfg["Jwt:Audience"], claims,
            expires: expires, signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}
