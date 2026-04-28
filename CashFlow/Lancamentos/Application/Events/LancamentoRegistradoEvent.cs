using Lancamentos.Domain.Enum;
using Lancamentos.Domain.Enum;

namespace Lancamentos.Application.Events
{
    public record LancamentoRegistradoEvent(
        Guid LancamentoId,
        decimal Valor,
        TipoLancamento Tipo,
        MeioLancamento? MeioLancamento,
        DateOnly Data,
        DateTime OcorridoEm
    );
}
