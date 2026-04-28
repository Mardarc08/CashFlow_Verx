using MediatR;

namespace Consolidado.Application.UseCases.ObterConsolidado
{
    public record ObterConsolidadoQuery(DateOnly Data) : IRequest<ConsolidadoDto?>;

    public record ConsolidadoDto(
        DateOnly Data,
        decimal TotalCreditos,
        decimal TotalDebitos,
        decimal SaldoFinal,
        DateTime AtualizadoEm
    );
}
