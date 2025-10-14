using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

public interface IJwtTokenService
{
    TokenDto Gerar(Usuario u);
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _cfg;
    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    public TokenDto Gerar(Usuario u)
    {
        var issuer = _cfg["Jwt:Issuer"]!;
        var audience = _cfg["Jwt:Audience"]!;
        var key = _cfg["Jwt:Key"]!;
        var expires = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Jwt:ExpiresMinutes"] ?? "60"));

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, u.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, u.Email),
            new Claim(ClaimTypes.Name, u.Nome),
            new Claim(ClaimTypes.Role, u.Role)
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new JwtSecurityToken(issuer, audience, claims, expires: expires, signingCredentials: creds);
        var token = new JwtSecurityTokenHandler().WriteToken(jwt);
        return new TokenDto(token, expires);
    }
}
