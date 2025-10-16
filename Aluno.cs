using System;
using System.Collections.Generic;

namespace ADOLab.Domain.Entities
{
    public class Aluno
    {
        public int Id { get; set; }                
        public string Nome { get; set; } = null!;
        public int Idade { get; set; }
        public string Email { get; set; } = null!;
        public DateTime DataNascimento { get; set; }

        // Navegação: muitas matrículas
        public ICollection<Matricula> Matriculas { get; set; } = new List<Matricula>();
    }
}
