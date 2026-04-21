using Consolidado.Domain.Entities;

namespace Consolidado.Domain.Interface
{
    public interface IConsolidadoRepository
    {
        Task<ConsolidadoDiario?> ObterPorDataAsync(DateOnly data, CancellationToken ct = default);
        Task SalvarAsync(ConsolidadoDiario consolidado, CancellationToken ct = default);
    }
}
