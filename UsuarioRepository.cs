public interface IUsuarioRepository
{
    Usuario? ObterPorEmail(string email);
    Usuario? ObterPorId(int id);
    int Criar(string nome, string email, string senhaHash, string role = "user");
}

public class UsuarioRepository : IUsuarioRepository
{
    private readonly List<Usuario> _db = new();
    private int _seq = 1;

    public Usuario? ObterPorEmail(string email) =>
        _db.FirstOrDefault(u => u.Email.ToLower() == email.ToLower());

    public Usuario? ObterPorId(int id) => _db.FirstOrDefault(u => u.Id == id);

    public int Criar(string nome, string email, string senhaHash, string role = "user")
    {
        if (ObterPorEmail(email) is not null) throw new InvalidOperationException("Email já cadastrado.");
        var u = new Usuario(_seq++, nome, email, senhaHash, role);
        _db.Add(u);
        return u.Id;
    }
}
