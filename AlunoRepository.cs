using System.Data;
using Microsoft.Data.SqlClient;
using System.Globalization;
using System.Collections.Generic;

/// <summary>
/// Repositório ADO.NET para a entidade Aluno.
/// </summary>
public class AlunoRepository : IRepository<Aluno>
{
    /// <summary>
    /// String de conexão.
    /// </summary>
    public string ConnectionString { get; set; }

    public AlunoRepository(string connectionString)
    {
        ConnectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// Garante a existência da tabela dbo.Alunos.
    /// </summary>
    public void GarantirEsquema()
    {
        const string ddl = @"
IF OBJECT_ID('dbo.Alunos', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.Alunos (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Nome NVARCHAR(100) NOT NULL,
        Idade INT NOT NULL,
        Email NVARCHAR(100) NOT NULL,
        DataNascimento DATE NOT NULL
    );
END";

        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(ddl, conn);
        conn.Open();
        cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Insere um aluno e retorna o Id gerado.
    /// </summary>
    public int Inserir(string nome, int idade, string email, DateTime dataNascimento)
    {
        const string sql = @"
INSERT INTO dbo.Alunos (Nome, Idade, Email, DataNascimento)
VALUES (@Nome, @Idade, @Email, @DataNascimento);
SELECT CAST(SCOPE_IDENTITY() AS INT);";

        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@Nome", SqlDbType.NVarChar, 100) { Value = nome });
        cmd.Parameters.Add(new SqlParameter("@Idade", SqlDbType.Int) { Value = idade });
        cmd.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 100) { Value = email });
        cmd.Parameters.Add(new SqlParameter("@DataNascimento", SqlDbType.Date) { Value = dataNascimento.Date });

        conn.Open();
        var id = (int)cmd.ExecuteScalar();
        return id;
    }

    /// <summary>
    /// Lista todos os alunos.
    /// </summary>
    public List<Aluno> Listar()
    {
        const string sql = @"SELECT Id, Nome, Idade, Email, DataNascimento FROM dbo.Alunos ORDER BY Id;";

        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        conn.Open();

        var lista = new List<Aluno>();
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            lista.Add(MapAluno(rd));
        }
        return lista;
    }

    /// <summary>
    /// Atualiza um aluno. Retorna linhas afetadas (0 ou 1).
    /// </summary>
    public int Atualizar(int id, string nome, int idade, string email, DateTime dataNascimento)
    {
        const string sql = @"
UPDATE dbo.Alunos
   SET Nome = @Nome,
       Idade = @Idade,
       Email = @Email,
       DataNascimento = @DataNascimento
 WHERE Id = @Id;";

        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
        cmd.Parameters.Add(new SqlParameter("@Nome", SqlDbType.NVarChar, 100) { Value = nome });
        cmd.Parameters.Add(new SqlParameter("@Idade", SqlDbType.Int) { Value = idade });
        cmd.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 100) { Value = email });
        cmd.Parameters.Add(new SqlParameter("@DataNascimento", SqlDbType.Date) { Value = dataNascimento.Date });

        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Exclui um aluno. Retorna linhas afetadas (0 ou 1).
    /// </summary>
    public int Excluir(int id)
    {
        const string sql = @"DELETE FROM dbo.Alunos WHERE Id = @Id;";

        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);
        cmd.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });

        conn.Open();
        return cmd.ExecuteNonQuery();
    }

    /// <summary>
    /// Busca por propriedade (Nome, Idade, Email, DataNascimento, Id) com valor.
    /// </summary>
    public List<Aluno> Buscar(string propriedade, object valor)
    {
        if (string.IsNullOrWhiteSpace(propriedade))
            throw new ArgumentException("Propriedade inválida.", nameof(propriedade));

        // Whitelist de colunas válidas para evitar SQL injection por nome de coluna
        string coluna = propriedade.Trim();
        switch (coluna.ToLowerInvariant())
        {
            case "id": coluna = "Id"; break;
            case "nome": coluna = "Nome"; break;
            case "idade": coluna = "Idade"; break;
            case "email": coluna = "Email"; break;
            case "datanascimento": coluna = "DataNascimento"; break;
            default: throw new ArgumentException("Propriedade não suportada.", nameof(propriedade));
        }

        // Para strings usa LIKE; para tipos numéricos/data usa igualdade
        bool usarLike = (coluna == "Nome" || coluna == "Email");

        string sql = usarLike
            ? $@"SELECT Id, Nome, Idade, Email, DataNascimento FROM dbo.Alunos WHERE {coluna} LIKE @Valor ORDER BY Id;"
            : $@"SELECT Id, Nome, Idade, Email, DataNascimento FROM dbo.Alunos WHERE {coluna} = @Valor ORDER BY Id;";

        using var conn = new SqlConnection(ConnectionString);
        using var cmd = new SqlCommand(sql, conn);

        SqlParameter p;
        if (usarLike)
        {
            p = new SqlParameter("@Valor", SqlDbType.NVarChar, 100)
            {
                Value = $"%{valor?.ToString() ?? string.Empty}%"
            };
        }
        else
        {
            // tipagem do parâmetro conforme coluna
            p = coluna switch
            {
                "Id" => new SqlParameter("@Valor", SqlDbType.Int) { Value = Convert.ToInt32(valor) },
                "Idade" => new SqlParameter("@Valor", SqlDbType.Int) { Value = Convert.ToInt32(valor) },
                "DataNascimento" => new SqlParameter("@Valor", SqlDbType.Date)
                { Value = (valor is DateTime dt) ? dt.Date : DateTime.Parse(valor!.ToString()!) },
                _ => new SqlParameter("@Valor", SqlDbType.NVarChar, 100) { Value = valor?.ToString() ?? "" }
            };
        }
        cmd.Parameters.Add(p);

        conn.Open();
        var lista = new List<Aluno>();
        using var rd = cmd.ExecuteReader();
        while (rd.Read())
        {
            lista.Add(MapAluno(rd));
        }
        return lista;
    }

    private static Aluno MapAluno(SqlDataReader rd)
    {
        return new Aluno(
            id: rd.GetInt32(rd.GetOrdinal("Id")),
            nome: rd.GetString(rd.GetOrdinal("Nome")),
            idade: rd.GetInt32(rd.GetOrdinal("Idade")),
            email: rd.GetString(rd.GetOrdinal("Email")),
            dataNascimento: rd.GetDateTime(rd.GetOrdinal("DataNascimento"))
        );
    }
}
