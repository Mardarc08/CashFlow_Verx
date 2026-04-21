using Lancamentos.Domain.Interfaces;
using MediatR;

namespace Lancamentos.Application.Lancamentos.ListarLancamentosPorData
{
    public class ListarLancamentosPorDataHandler : IRequestHandler<ListarLancamentosPorDataQuery, IEnumerable<LancamentoDto>>
    {
        private readonly ILancamentoRepository _repository;

        public ListarLancamentosPorDataHandler(ILancamentoRepository repository)
            => _repository = repository;

        public async Task<IEnumerable<LancamentoDto>> Handle(ListarLancamentosPorDataQuery query, CancellationToken ct)
        {
            var lancamentos = await _repository.ListarLancamentosPorDataAsync(query.Data, ct);

            return lancamentos.Select(l => new LancamentoDto(
                l.Id,
                l.Valor,
                l.Tipo,
                l.Tipo.ToString(),
                l.Descricao,
                l.Data,
                l.DataCriacao));
        }
    }
}