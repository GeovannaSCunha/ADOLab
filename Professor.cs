using System.Collections.Generic;

namespace ADOLab.Domain.Entities
{
    public class Professor
    {
        public int Id { get; set; }                 // PK
        public string Nome { get; set; } = null!;
        public string Email { get; set; } = null!;

        // 1:N -> Professor tem várias Disciplinas
        public ICollection<Disciplina> Disciplinas { get; set; } = new List<Disciplina>();
    }
}
