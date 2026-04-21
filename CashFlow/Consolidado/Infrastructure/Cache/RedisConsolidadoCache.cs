using Consolidado.Application.UseCases.ObterConsolidado;
using StackExchange.Redis;
using System.Text.Json;

namespace Consolidado.Infrastructure.Cache
{
    public interface IConsolidadoCache
    {
        Task<ConsolidadoDto?> ObterAsync(DateOnly data, CancellationToken ct = default);
        Task SetarAsync(DateOnly data, ConsolidadoDto dto, CancellationToken ct = default);
        Task InvalidarAsync(DateOnly data, CancellationToken ct = default);
    }

    public class RedisConsolidadoCache : IConsolidadoCache
    {
        private readonly IDatabase _redis;
        private readonly ILogger<RedisConsolidadoCache> _logger;
        private static readonly TimeSpan _ttl = TimeSpan.FromMinutes(5);

        public RedisConsolidadoCache(IConnectionMultiplexer redis, ILogger<RedisConsolidadoCache> logger)
        {
            _redis = redis.GetDatabase();
            _logger = logger;
        }

        private static string CacheKey(DateOnly data) => $"consolidado:{data:yyyy-MM-dd}";

        public async Task<ConsolidadoDto?> ObterAsync(DateOnly data, CancellationToken ct = default)
        {
            try
            {
                var value = await _redis.StringGetAsync(CacheKey(data));
                if (value.IsNullOrEmpty) return null;
                return JsonSerializer.Deserialize<ConsolidadoDto>((string)value);
            }
            catch (Exception ex)
            {
                // Falha no cache não pode derrubar a API — degrada graciosamente para o banco
                _logger.LogWarning(ex, "Falha ao ler cache Redis para {Data}. Continuando sem cache.", data);
                return null;
            }
        }

        public async Task SetarAsync(DateOnly data, ConsolidadoDto dto, CancellationToken ct = default)
        {
            try
            {
                var json = JsonSerializer.Serialize(dto);
                await _redis.StringSetAsync(CacheKey(data), json, _ttl);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao gravar cache Redis para {Data}.", data);
            }
        }

        public async Task InvalidarAsync(DateOnly data, CancellationToken ct = default)
        {
            try
            {
                await _redis.KeyDeleteAsync(CacheKey(data));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao invalidar cache Redis para {Data}.", data);
            }
        }
    }
}
