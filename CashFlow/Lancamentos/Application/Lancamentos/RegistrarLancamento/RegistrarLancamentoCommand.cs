using Lancamentos.Domain.Enum;
using MediatR;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento
{
    public record RegistrarLancamentoCommand(
        decimal Valor,
        TipoLancamento Tipo,
        string Descricao,
        DateOnly Data,
        MeioLancamento? MeioLancamento
    ) : IRequest<RegistrarLancamentoResponse>;

    public record RegistrarLancamentoResponse(Guid Id, DateTime CriadoEm);
}
