using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;

// ===================== MODELOS & DTOS =====================
public record Aluno(int Id, string Nome, int Idade, string Email, DateTime DataNascimento);

public record AlunoCreateDto(string Nome, int Idade, string Email, DateTime DataNascimento);
public record AlunoUpdateDto(string Nome, int Idade, string Email, DateTime DataNascimento);

public record Usuario(int Id, string Nome, string Email, string SenhaHash, string Role = "user");
public record RegisterDto(string Nome, string Email, string Senha);
public record LoginDto(string Email, string Senha);
public record TokenDto(string access_token, DateTime expires_at);

// ===================== CONTRATOS =====================
public interface IRepository<T>
{
    int Inserir(string nome, int idade, string email, DateTime dataNascimento);
    List<Aluno> Listar();
    int Atualizar(int id, string nome, int idade, string email, DateTime dataNascimento);
    int Excluir(int id);
    List<Aluno> Buscar(string propriedade, object valor);
    void GarantirEsquema();
}

public interface IUsuarioRepository
{
    Usuario? ObterPorEmail(string email);
    Usuario? ObterPorId(int id);
    int Criar(string nome, string email, string senhaHash, string role = "user");
}

public interface IJwtTokenService
{
    TokenDto Gerar(Usuario u);
}

// ===================== AUTH (implementação simples) =====================
public class UsuarioRepository : IUsuarioRepository
{
    private readonly List<Usuario> _db = new();
    private int _seq = 1;

    public Usuario? ObterPorEmail(string email) =>
        _db.FirstOrDefault(u => u.Email.Equals(email, StringComparison.OrdinalIgnoreCase));

    public Usuario? ObterPorId(int id) => _db.FirstOrDefault(u => u.Id == id);

    public int Criar(string nome, string email, string senhaHash, string role = "user")
    {
        if (ObterPorEmail(email) is not null)
            throw new InvalidOperationException("Email já cadastrado.");
        var u = new Usuario(_seq++, nome, email, senhaHash, role);
        _db.Add(u);
        return u.Id;
    }
}

public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _cfg;
    public JwtTokenService(IConfiguration cfg) => _cfg = cfg;

    public TokenDto Gerar(Usuario u)
    {
        var issuer = _cfg["Jwt:Issuer"] ?? throw new InvalidOperationException("Jwt:Issuer ausente.");
        var audience = _cfg["Jwt:Audience"] ?? throw new InvalidOperationException("Jwt:Audience ausente.");
        var key = _cfg["Jwt:Key"] ?? throw new InvalidOperationException("Jwt:Key ausente.");
        var mins = int.TryParse(_cfg["Jwt:ExpiresMinutes"], out var m) ? m : 60;
        var expires = DateTime.UtcNow.AddMinutes(mins);

        var claims = new[]
        {
            new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub, u.Id.ToString()),
            new System.Security.Claims.Claim(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Email, u.Email),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, u.Nome),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, u.Role)
        };

        var creds = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            SecurityAlgorithms.HmacSha256);

        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expires,
            signingCredentials: creds);

        var token = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().WriteToken(jwt);
        return new TokenDto(token, expires);
    }
}

// ===================== EF CORE =====================
public class EscolaContext : DbContext
{
    public EscolaContext(DbContextOptions<EscolaContext> options) : base(options) { }

    public DbSet<Aluno> Alunos => Set<Aluno>();
    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Mantém compatibilidade com a tabela já existente "Alunos"
        mb.Entity<Aluno>(e =>
        {
            e.ToTable("Alunos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            e.Property(x => x.Email).HasMaxLength(100).IsRequired();
            e.Property(x => x.Idade).IsRequired();
            e.Property(x => x.DataNascimento).IsRequired();
        });

        // Quando você criar Professor/Disciplina/Matricula, registre aqui também.
        // Exemplo (quando tiver as classes):
        // mb.Entity<Professor>(...);
        // mb.Entity<Disciplina>(...);
        // mb.Entity<Matricula>(...);
    }
}

// ===================== APP =====================
var builder = WebApplication.CreateBuilder(args);

// 1) Connection string
var connString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=ADOLab;Trusted_Connection=True;TrustServerCertificate=True;";

// 2) EF Core
builder.Services.AddDbContext<EscolaContext>(opt => opt.UseSqlServer(connString));

// 3) DI do repositório ADO.NET que você já tem (ajuste o namespace/using da sua classe AlunoRepository)
builder.Services.AddSingleton<IRepository<Aluno>>(_ =>
{
    var repo = new AlunoRepository(connString); // sua implementação ADO.NET
    repo.GarantirEsquema();
    return repo;
});

// 4) Auth/JWT
builder.Services.AddSingleton<IUsuarioRepository, UsuarioRepository>();
builder.Services.AddSingleton<IJwtTokenService, JwtTokenService>();

var key = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("Configure Jwt:Key");
var issuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("Configure Jwt:Issuer");
var audience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("Configure Jwt:Audience");

builder.Services
    .AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", o =>
    {
        o.TokenValidationParameters = new()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromSeconds(15)
        };
    });

builder.Services.AddAuthorization(o =>
{
    // Política opcional para admin (usada no DELETE)
    o.AddPolicy("admin", p => p.RequireRole("admin"));
});

// 5) Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v2", new OpenApiInfo { Title = "Alunos API V2 (JWT + EF Core)", Version = "v2" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer. Ex.: Bearer {seu_token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { new OpenApiSecurityScheme{ Reference = new OpenApiReference{ Type = ReferenceType.SecurityScheme, Id = "Bearer"}}, Array.Empty<string>() }
    });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(setup => setup.SwaggerEndpoint("/swagger/v2/swagger.json", "v2"));

app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ===================== AUTH =====================
var auth = app.MapGroup("/api/auth").WithTags("Auth");

// Registro de usuário
auth.MapPost("/register", ([FromBody] RegisterDto dto, IUsuarioRepository users) =>
{
    if (string.IsNullOrWhiteSpace(dto.Nome) || string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Senha))
        return Results.BadRequest("Nome, Email e Senha são obrigatórios.");

    if (users.ObterPorEmail(dto.Email) is not null)
        return Results.Conflict("Email já cadastrado.");

    var hash = BCrypt.Net.BCrypt.HashPassword(dto.Senha);
    var id = users.Criar(dto.Nome.Trim(), dto.Email.Trim(), hash, role: "admin");
    return Results.Created($"/api/users/{id}", new { id, dto.Nome, dto.Email });
});

// Login
auth.MapPost("/login", ([FromBody] LoginDto dto, IUsuarioRepository users, IJwtTokenService tokens) =>
{
    var u = users.ObterPorEmail(dto.Email);
    if (u is null || !BCrypt.Net.BCrypt.Verify(dto.Senha, u.SenhaHash))
        return Results.Unauthorized();

    var tk = tokens.Gerar(u);
    return Results.Ok(tk);
});

// ===================== API V2 /api/v2/alunos (PROTEGIDA) =====================
var v2 = app.MapGroup("/api/v2/alunos")
            .WithTags("Alunos V2")
            .RequireAuthorization(); // toda a V2 exige Bearer

// Listar
v2.MapGet("/", (IRepository<Aluno> repo) => Results.Ok(repo.Listar()))
  .Produces<List<Aluno>>(StatusCodes.Status200OK);

// Detalhe
v2.MapGet("/{id:int}", (IRepository<Aluno> repo, int id) =>
{
    var a = repo.Buscar("Id", id).FirstOrDefault();
    return a is null ? Results.NotFound() : Results.Ok(a);
})
.Produces<Aluno>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// Criar
v2.MapPost("/", (IRepository<Aluno> repo, [FromBody] AlunoCreateDto dto) =>
{
    if (!Valid(dto, out var err)) return Results.ValidationProblem(err);

    var id = repo.Inserir(dto.Nome.Trim(), dto.Idade, dto.Email.Trim(), dto.DataNascimento.Date);
    var created = repo.Buscar("Id", id).First();
    return Results.Created($"/api/v2/alunos/{id}", created);
})
.Produces<Aluno>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest);

// Atualizar
v2.MapPut("/{id:int}", (IRepository<Aluno> repo, int id, [FromBody] AlunoUpdateDto dto) =>
{
    var existe = repo.Buscar("Id", id).FirstOrDefault();
    if (existe is null) return Results.NotFound();

    if (!Valid(new AlunoCreateDto(dto.Nome, dto.Idade, dto.Email, dto.DataNascimento), out var err))
        return Results.ValidationProblem(err);

    var linhas = repo.Atualizar(id, dto.Nome.Trim(), dto.Idade, dto.Email.Trim(), dto.DataNascimento.Date);
    if (linhas == 0) return Results.NotFound();

    var updated = repo.Buscar("Id", id).First();
    return Results.Ok(updated);
})
.Produces<Aluno>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status400BadRequest);

// Excluir (exige role admin)
v2.MapDelete("/{id:int}", (IRepository<Aluno> repo, int id) =>
{
    var linhas = repo.Excluir(id);
    return linhas == 0 ? Results.NotFound() : Results.NoContent();
})
.RequireAuthorization("admin")
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

// =============== ENDPOINT OPCIONAL: seed EF para validar quickly ===============
app.MapPost("/seed-ef", async (EscolaContext db) =>
{
    // Só para validar que o EF está conectado e salvando.
    // Se a tabela Alunos já existir/populada, este seed pode ser adaptado.
    if (!await db.Alunos.AnyAsync())
    {
        db.Alunos.Add(new Aluno(0, "Aluno EF", 20, "alunoef@ex.com", new DateTime(2005, 1, 10)));
        await db.SaveChangesAsync();
    }
    return Results.Ok("EF OK");
}).WithTags("Dev");

// ======================================================================
app.Run();

// ===================== VALIDAÇÃO DTO =====================
static bool Valid(AlunoCreateDto dto, out IDictionary<string, string[]>? errors)
{
    var e = new Dictionary<string, string[]>();
    if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Nome.Trim().Length < 3) e["nome"] = new[] { "Mínimo 3 caracteres." };
    if (dto.Idade < 0 || dto.Idade > 130) e["idade"] = new[] { "0 a 130." };
    if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains("@")) e["email"] = new[] { "Email inválido." };
    if (dto.DataNascimento == default) e["dataNascimento"] = new[] { "Obrigatório." };
    errors = e.Count == 0 ? null : e;
    return errors is null;
}
