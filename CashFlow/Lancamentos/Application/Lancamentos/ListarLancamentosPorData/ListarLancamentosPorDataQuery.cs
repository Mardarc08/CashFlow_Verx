using Lancamentos.Domain.Enum;
using MediatR;

namespace Lancamentos.Application.Lancamentos.ListarLancamentosPorData
{
    public record ListarLancamentosPorDataQuery(DateOnly Data) : IRequest<IEnumerable<LancamentoDto>>;

    public record LancamentoDto(
        Guid Id,
        decimal Valor,
        TipoLancamento Tipo,
        string TipoDescricao,
        string Descricao,
        DateOnly Data,
        DateTime DataCriacao
    );
}
