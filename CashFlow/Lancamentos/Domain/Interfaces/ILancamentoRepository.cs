using Lancamentos.Application.Lancamentos.ListarLancamentosPorData;
using Lancamentos.Domain.Entities;

namespace Lancamentos.Domain.Interfaces
{
    public interface ILancamentoRepository
    {
        Task AdicionarLancamentoAsync(Lancamento lancamento, CancellationToken cancellationToken = default);
        Task<IEnumerable<Lancamento>> ListarLancamentosPorDataAsync(DateOnly data, CancellationToken cancellationToken = default);
    }
}
