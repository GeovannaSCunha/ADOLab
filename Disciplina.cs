using System.Collections.Generic;

namespace ADOLab.Domain.Entities
{
    public class Disciplina
    {
        public int Id { get; set; }                 // PK
        public string Nome { get; set; } = null!;

        // FK -> Professor
        public int ProfessorId { get; set; }
        public Professor Professor { get; set; } = null!;

        // N:N via Matricula
        public ICollection<Matricula> Matriculas { get; set; } = new List<Matricula>();
    }
}
