public record Usuario(int Id, string Nome, string Email, string SenhaHash, string Role = "user");
public record RegisterDto(string Nome, string Email, string Senha);
public record LoginDto(string Email, string Senha);
public record TokenDto(string access_token, DateTime expires_at);
