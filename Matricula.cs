using System;

namespace ADOLab.Domain.Entities
{
    // Usaremos chave composta (AlunoId, DisciplinaId)
    public class Matricula
    {
        public int AlunoId { get; set; }
        public Aluno Aluno { get; set; } = null!;

        public int DisciplinaId { get; set; }
        public Disciplina Disciplina { get; set; } = null!;

        public DateTime DataMatricula { get; set; } = DateTime.UtcNow;
        public string? Status { get; set; } 
    }
}
