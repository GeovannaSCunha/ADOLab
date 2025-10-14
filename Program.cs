using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

// ====== MODELOS / DTOS ======
public record Aluno(int Id, string Nome, int Idade, string Email, DateTime DataNascimento);

public record AlunoCreateDto(
    string Nome,
    int Idade,
    string Email,
    DateTime DataNascimento
);

public record AlunoUpdateDto(
    string Nome,
    int Idade,
    string Email,
    DateTime DataNascimento
);

// ====== CONTRATO DO REPOSITÓRIO ======
public interface IRepository<T>
{
    // Métodos que você já tem no seu projeto
    int Inserir(string nome, int idade, string email, DateTime dataNascimento);
    List<Aluno> Listar();
    int Atualizar(int id, string nome, int idade, string email, DateTime dataNascimento);
    int Excluir(int id);
    List<Aluno> Buscar(string propriedade, object valor);
    void GarantirEsquema();
}

// ====== SUA IMPLEMENTAÇÃO EXISTENTE ======
// AlunoRepository: a mesma que você já criou (ADO.NET/SqlClient)
// (coloque a sua classe AlunoRepository aqui ou no arquivo próprio)

var builder = WebApplication.CreateBuilder(args);

// Conexão
var connString = builder.Configuration.GetConnectionString("DefaultConnection")
                  ?? "Server=localhost;Database=ADOLab;Trusted_Connection=True;TrustServerCertificate=True;";

// DI
builder.Services.AddSingleton<IRepository<Aluno>>(sp =>
{
    var repo = new AlunoRepository(connString);
    repo.GarantirEsquema();
    return repo;
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Alunos API", Version = "v1" });
});

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

// ====== GRUPO /api/alunos ======
var api = app.MapGroup("/api/alunos").WithTags("Alunos");

// LISTAR
api.MapGet("/", ([FromServices] IRepository<Aluno> repo) =>
{
    var itens = repo.Listar();
    return Results.Ok(itens);
})
.Produces<List<Aluno>>(StatusCodes.Status200OK);

// OBTER POR ID
api.MapGet("/{id:int}", ([FromServices] IRepository<Aluno> repo, int id) =>
{
    var item = repo.Buscar("Id", id).FirstOrDefault();
    return item is null ? Results.NotFound() : Results.Ok(item);
})
.Produces<Aluno>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound);

// BUSCAR (filtros opcionais)
api.MapGet("/search", (
    [FromServices] IRepository<Aluno> repo,
    string? nome,
    string? email,
    int? idade,
    DateTime? dataNascimento
) =>
{
    var resultados = new List<Aluno>();

    if (!string.IsNullOrWhiteSpace(nome))
        resultados.AddRange(repo.Buscar("Nome", nome));
    if (!string.IsNullOrWhiteSpace(email))
        resultados.AddRange(repo.Buscar("Email", email));
    if (idade.HasValue)
        resultados.AddRange(repo.Buscar("Idade", idade.Value));
    if (dataNascimento.HasValue)
        resultados.AddRange(repo.Buscar("DataNascimento", dataNascimento.Value.Date));

    // Se nenhum filtro: retorna tudo
    if (string.IsNullOrWhiteSpace(nome) && string.IsNullOrWhiteSpace(email) && !idade.HasValue && !dataNascimento.HasValue)
        resultados = repo.Listar();

    // remove duplicados pelo Id
    var unicos = resultados
        .GroupBy(a => a.Id)
        .Select(g => g.First())
        .OrderBy(a => a.Id)
        .ToList();

    return Results.Ok(unicos);
})
.Produces<List<Aluno>>(StatusCodes.Status200OK);

// CRIAR
api.MapPost("/", ([FromServices] IRepository<Aluno> repo, [FromBody] AlunoCreateDto dto) =>
{
    var erro = Validar(dto);
    if (erro is not null) return Results.ValidationProblem(erro);

    var novoId = repo.Inserir(dto.Nome.Trim(), dto.Idade, dto.Email.Trim(), dto.DataNascimento.Date);
    var criado = repo.Buscar("Id", novoId).First(); // deve existir após inserir

    return Results.Created($"/api/alunos/{novoId}", criado);
})
.Produces<Aluno>(StatusCodes.Status201Created)
.ProducesProblem(StatusCodes.Status400BadRequest);

// ATUALIZAR
api.MapPut("/{id:int}", ([FromServices] IRepository<Aluno> repo, int id, [FromBody] AlunoUpdateDto dto) =>
{
    var existente = repo.Buscar("Id", id).FirstOrDefault();
    if (existente is null) return Results.NotFound();

    var erro = Validar(dto);
    if (erro is not null) return Results.ValidationProblem(erro);

    var linhas = repo.Atualizar(id, dto.Nome.Trim(), dto.Idade, dto.Email.Trim(), dto.DataNascimento.Date);
    if (linhas == 0) return Results.NotFound();

    var atualizado = repo.Buscar("Id", id).First();
    return Results.Ok(atualizado);
})
.Produces<Aluno>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status404NotFound)
.ProducesProblem(StatusCodes.Status400BadRequest);

// EXCLUIR
api.MapDelete("/{id:int}", ([FromServices] IRepository<Aluno> repo, int id) =>
{
    var linhas = repo.Excluir(id);
    return linhas == 0 ? Results.NotFound() : Results.NoContent();
})
.Produces(StatusCodes.Status204NoContent)
.Produces(StatusCodes.Status404NotFound);

app.Run();

// ====== VALIDAÇÃO BÁSICA ======
static IDictionary<string, string[]>? Validar(AlunoCreateDto dto)
{
    var erros = new Dictionary<string, string[]>();

    if (string.IsNullOrWhiteSpace(dto.Nome) || dto.Nome.Trim().Length < 3)
        erros["nome"] = new[] { "Nome é obrigatório e deve ter ao menos 3 caracteres." };

    if (dto.Idade < 0 || dto.Idade > 130)
        erros["idade"] = new[] { "Idade deve estar entre 0 e 130." };

    if (string.IsNullOrWhiteSpace(dto.Email) || !dto.Email.Contains("@"))
        erros["email"] = new[] { "Email inválido." };

    if (dto.DataNascimento == default)
        erros["dataNascimento"] = new[] { "Data de nascimento inválida." };

    return erros.Count == 0 ? null : erros;
}

static IDictionary<string, string[]>? Validar(AlunoUpdateDto dto)
{
    // mesma regra do Create
    return Validar(new AlunoCreateDto(dto.Nome, dto.Idade, dto.Email, dto.DataNascimento));
}
