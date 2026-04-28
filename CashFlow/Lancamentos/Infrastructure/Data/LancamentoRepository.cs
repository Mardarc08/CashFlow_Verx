using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Lancamentos.Infrastructure.Data
{
    public class LancamentoRepository : ILancamentoRepository
    {
        private readonly AppDbContext _context;

        public LancamentoRepository(AppDbContext context) => _context = context;

        public async Task AdicionarLancamentoAsync(Lancamento lancamento, CancellationToken ct = default)
        {
            await _context.Lancamentos.AddAsync(lancamento, ct);
            await _context.SaveChangesAsync(ct);
        }

        public async Task<IEnumerable<Lancamento>> ListarLancamentosPorDataAsync(DateOnly data, CancellationToken ct = default)
        {
            return await _context.Lancamentos
                .Where(l => l.Data == data)
                .OrderByDescending(l => l.DataCriacao)
                .ToListAsync(ct);
        }
    }
}
