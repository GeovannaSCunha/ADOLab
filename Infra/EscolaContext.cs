using System.Collections.Generic;
using System.Reflection.Emit;
using ADOLab.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ADOLab.Infra
{
    public class EscolaContext : DbContext
    {
        public EscolaContext(DbContextOptions<EscolaContext> options) : base(options) { }

        public DbSet<Aluno> Alunos => Set<Aluno>();
        public DbSet<Professor> Professores => Set<Professor>();
        public DbSet<Disciplina> Disciplinas => Set<Disciplina>();
        public DbSet<Matricula> Matriculas => Set<Matricula>();

        protected override void OnModelCreating(ModelBuilder mb)
        {
            base.OnModelCreating(mb);

            // ===== Aluno =====
            mb.Entity<Aluno>(e =>
            {
                e.ToTable("Alunos"); 
                e.HasKey(x => x.Id);
                e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
                e.Property(x => x.Email).HasMaxLength(100).IsRequired();
                e.Property(x => x.Idade).IsRequired();
                e.Property(x => x.DataNascimento).IsRequired();
            });

            // ===== Professor =====
            mb.Entity<Professor>(e =>
            {
                e.ToTable("Professores");
                e.HasKey(x => x.Id);
                e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
                e.Property(x => x.Email).HasMaxLength(100).IsRequired();

                e.HasMany(x => x.Disciplinas)
                 .WithOne(d => d.Professor)
                 .HasForeignKey(d => d.ProfessorId)
                 .OnDelete(DeleteBehavior.Restrict);
            });

            // ===== Disciplina =====
            mb.Entity<Disciplina>(e =>
            {
                e.ToTable("Disciplinas");
                e.HasKey(x => x.Id);
                e.Property(x => x.Nome).HasMaxLength(100).IsRequired();
            });

            // ===== Matricula (chave composta) =====
            mb.Entity<Matricula>(e =>
            {
                e.ToTable("Matriculas");
                e.HasKey(x => new { x.AlunoId, x.DisciplinaId }); 

                e.HasOne(m => m.Aluno)
                 .WithMany(a => a.Matriculas)
                 .HasForeignKey(m => m.AlunoId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.HasOne(m => m.Disciplina)
                 .WithMany(d => d.Matriculas)
                 .HasForeignKey(m => m.DisciplinaId)
                 .OnDelete(DeleteBehavior.Cascade);

                e.Property(x => x.Status).HasMaxLength(30);
            });
        }
    }
}
