using Consolidado.Domain.Entities;
using Consolidado.Domain.Interface;
using Microsoft.EntityFrameworkCore;

namespace Consolidado.Infrastructure.Persistence
{
    public class ConsolidadoRepository : IConsolidadoRepository
    {
        private readonly AppDbContext _context;

        public ConsolidadoRepository(AppDbContext context) => _context = context;

        public async Task<ConsolidadoDiario?> ObterPorDataAsync(DateOnly data, CancellationToken ct = default)
            => await _context.Consolidados.FirstOrDefaultAsync(c => c.Data == data, ct);

        public async Task SalvarAsync(ConsolidadoDiario consolidado, CancellationToken ct = default)
        {
            var exists = await _context.Consolidados.AnyAsync(c => c.Id == consolidado.Id, ct);

            if (exists)
                _context.Consolidados.Update(consolidado);
            else
                await _context.Consolidados.AddAsync(consolidado, ct);

            await _context.SaveChangesAsync(ct);
        }
    }
}
