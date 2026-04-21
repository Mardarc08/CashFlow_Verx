using Consolidado.Domain.Entities;
using Consolidado.Domain.Enum;
using Consolidado.Domain.Interface;
using Consolidado.Infrastructure.Cache;
using Google.Cloud.PubSub.V1;
using System.Text.Json;

namespace Consolidado.Application.Events
{
    // Evento recebido do serviço de Lançamentos via Pub/Sub
    public record LancamentoRegistradoEvent(
        Guid LancamentoId,
        decimal Valor,
        TipoLancamento Tipo,
        MeioLancamento MeioLancamento,
        DateOnly Data,
        DateTime OcorridoEm
    );

    public class LancamentoRegistradoConsumer : BackgroundService
    {
        private readonly SubscriberClient _subscriber;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConsolidadoCache _cache;
        private readonly ILogger<LancamentoRegistradoConsumer> _logger;

        public LancamentoRegistradoConsumer(
            SubscriberClient subscriber,
            IServiceScopeFactory scopeFactory,
            IConsolidadoCache cache,
            ILogger<LancamentoRegistradoConsumer> logger)
        {
            _subscriber = subscriber;
            _scopeFactory = scopeFactory;
            _cache = cache;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Consumer Pub/Sub iniciado.");

            await _subscriber.StartAsync(async (message, ct) =>
            {
                try
                {
                    var json = message.Data.ToStringUtf8();
                    var evento = JsonSerializer.Deserialize<LancamentoRegistradoEvent>(json);

                    if (evento is null)
                    {
                        _logger.LogWarning("Mensagem inválida recebida. MessageId: {Id}", message.MessageId);
                        return SubscriberClient.Reply.Nack; // reencaminha para DLQ após N tentativas
                    }

                    await ProcessarEventoAsync(evento, ct);
                    return SubscriberClient.Reply.Ack;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Erro ao processar mensagem {Id}.", message.MessageId);
                    return SubscriberClient.Reply.Nack; // Pub/Sub vai retentar automaticamente
                }
            });
        }

        private async Task ProcessarEventoAsync(LancamentoRegistradoEvent evento, CancellationToken ct)
        {
            // Cria scope para injetar repositório (scoped service em BackgroundService)
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IConsolidadoRepository>();

            // Busca ou cria o consolidado do dia
            var consolidado = await repository.ObterPorDataAsync(evento.Data, ct)
                              ?? ConsolidadoDiario.Criar(evento.Data);

            // Aplica o lançamento
            if (evento.Tipo == TipoLancamento.Credito)
                consolidado.AplicarCredito(evento.Valor);
            else
                consolidado.AplicarDebito(evento.Valor);

            await repository.SalvarAsync(consolidado, ct);

            // Invalida o cache para forçar leitura atualizada
            await _cache.InvalidarAsync(evento.Data, ct);

            _logger.LogInformation(
                "Consolidado de {Data} atualizado. Saldo: {Saldo:C}",
                evento.Data,
                consolidado.SaldoFinal);
        }

        public override async Task StopAsync(CancellationToken ct)
        {
            await _subscriber.StopAsync(ct);
            await base.StopAsync(ct);
        }
    }
}
