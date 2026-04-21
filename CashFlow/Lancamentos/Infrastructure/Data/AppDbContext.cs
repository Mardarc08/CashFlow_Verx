using Lancamentos.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Lancamento> Lancamentos => Set<Lancamento>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Lancamento>(e =>
            {
                e.HasKey(x => x.Id);
                e.Property(x => x.Valor).HasColumnType("numeric(18,2)").IsRequired();
                e.Property(x => x.Tipo).IsRequired();
                e.Property(x => x.MeioLancamento);
                e.Property(x => x.Descricao).HasMaxLength(255).IsRequired();
                e.Property(x => x.Data).IsRequired();
                e.Property(x => x.DataCriacao).IsRequired();
                e.HasIndex(x => x.Data); // índice para queries por data
            });
        }
    }
}
