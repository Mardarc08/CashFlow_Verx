using Confluent.Kafka;
using Consolidado.Domain.Entities;
using Consolidado.Domain.Enum;
using Consolidado.Domain.Interface;
using Consolidado.Infrastructure.Cache;
using System.Text.Json;

namespace Consolidado.Application.Events
{
    // Evento recebido do serviço de Lançamentos via Kafka
    public record LancamentoRegistradoEvent(
        Guid LancamentoId,
        decimal Valor,
        TipoLancamento Tipo,
        DateOnly Data,
        DateTime OcorridoEm
    );

    public class LancamentoRegistradoConsumer : BackgroundService
    {
        private readonly IConsumer<string, string> _consumer;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConsolidadoCache _cache;
        private readonly ILogger<LancamentoRegistradoConsumer> _logger;
        private readonly string _topicName;

        public LancamentoRegistradoConsumer(
            IConsumer<string, string> consumer,
            IServiceScopeFactory scopeFactory,
            IConsolidadoCache cache,
            ILogger<LancamentoRegistradoConsumer> logger,
            IConfiguration configuration)
        {
            _consumer = consumer;
            _scopeFactory = scopeFactory;
            _cache = cache;
            _logger = logger;
            _topicName = configuration["Kafka:Topics:LancamentoRegistrado"] ?? "lancamento-registrado";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Kafka Consumer iniciado para o tópico '{TopicName}'.", _topicName);

            _consumer.Subscribe(_topicName);

            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        var result = _consumer.Consume(stoppingToken);

                        if (result.IsPartitionEOF)
                            continue;

                        await ProcessarMensagemAsync(result, stoppingToken);
                        _consumer.Commit(result);
                    }
                    catch (ConsumeException ex)
                    {
                        _logger.LogError(ex, "Erro ao consumir mensagem do Kafka.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Consumer cancelado.");
            }
            finally
            {
                _consumer.Close();
            }
        }

        private async Task ProcessarMensagemAsync(ConsumeResult<string, string> result, CancellationToken ct)
        {
            try
            {
                var evento = JsonSerializer.Deserialize<LancamentoRegistradoEvent>(result.Message.Value);

                if (evento is null)
                {
                    _logger.LogWarning("Mensagem inválida recebida. Chave: {Chave}, Offset: {Offset}", 
                        result.Message.Key, result.Offset);
                    return;
                }

                await ProcessarEventoAsync(evento, ct);

                _logger.LogInformation(
                    "Mensagem processada com sucesso. Chave: {Chave}, Offset: {Offset}", 
                    result.Message.Key, result.Offset);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Erro ao deserializar mensagem. Chave: {Chave}", result.Message.Key);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar mensagem. Chave: {Chave}", result.Message.Key);
                throw; // Retentar a mensagem
            }
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
            _logger.LogInformation("Parando Kafka Consumer.");
            _consumer.Unsubscribe();
            _consumer.Dispose();
            await base.StopAsync(ct);
        }
    }
}
