using Consolidado.Domain.Interface;
using Consolidado.Infrastructure.Cache;
using MediatR;

namespace Consolidado.Application.UseCases.ObterConsolidado
{
    public class ObterConsolidadoHandler : IRequestHandler<ObterConsolidadoQuery, ConsolidadoDto?>
    {
        private readonly IConsolidadoRepository _repository;
        private readonly IConsolidadoCache _cache;
        private readonly ILogger<ObterConsolidadoHandler> _logger;

        public ObterConsolidadoHandler(
            IConsolidadoRepository repository,
            IConsolidadoCache cache,
            ILogger<ObterConsolidadoHandler> logger)
        {
            _repository = repository;
            _cache = cache;
            _logger = logger;
        }

        public async Task<ConsolidadoDto?> Handle(ObterConsolidadoQuery query, CancellationToken ct)
        {
            // 1. Tenta cache primeiro (cache-aside)
            var cached = await _cache.ObterAsync(query.Data, ct);
            if (cached is not null)
            {
                _logger.LogDebug("Cache hit para consolidado de {Data}", query.Data);
                return cached;
            }

            // 2. Cache miss — busca no banco
            _logger.LogDebug("Cache miss para consolidado de {Data}. Buscando no banco.", query.Data);
            var consolidado = await _repository.ObterPorDataAsync(query.Data, ct);

            if (consolidado is null)
                return null;

            var dto = new ConsolidadoDto(
                consolidado.Data,
                consolidado.TotalCreditos,
                consolidado.TotalDebitos,
                consolidado.SaldoFinal,
                consolidado.AtualizadoEm);

            // 3. Popula cache para próximas requisições
            await _cache.SetarAsync(query.Data, dto, ct);

            return dto;
        }
    }
}
