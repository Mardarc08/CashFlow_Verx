using Consolidado.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure.Persistence
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<ConsolidadoDiario> Consolidados => Set<ConsolidadoDiario>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ConsolidadoDiario>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.TotalCreditos).HasColumnType("decimal(18,2)").IsRequired();
                e.Property(x => x.TotalDebitos).HasColumnType("decimal(18,2)").IsRequired();
                e.Property(x => x.Data).HasColumnType("date").IsRequired();
                e.Property(x => x.AtualizadoEm).HasColumnType("datetime2").IsRequired();
                e.HasIndex(x => x.Data).IsUnique(); // um consolidado por dia
                                                    
                e.Ignore(x => x.SaldoFinal); // SaldoFinal é uma propriedade computada — não mapeada no banco
            });
        }
    }
}
