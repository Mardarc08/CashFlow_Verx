using Confluent.Kafka;
using Lancamentos.Application.Events;
using System.Text.Json;

namespace Lancamentos.Infrastructure.Messaging
{
    public interface IKafkaProducer
    {
        Task PublicarAsync(LancamentoRegistradoEvent evento, CancellationToken ct = default);
    }

    public class KafkaProducer : IKafkaProducer, IDisposable
    {
        private readonly IProducer<string, string> _producer;
        private readonly string _topic;
        private readonly ILogger<KafkaProducer> _logger;

        public KafkaProducer(IConfiguration configuration, ILogger<KafkaProducer> logger)
        {
            _logger = logger;
            _topic = configuration["Kafka:Topic"]!;

            var config = new ProducerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"],

                // Garante que a mensagem foi persistida em todas as réplicas antes de confirmar
                Acks = Acks.All,

                // Retenta automaticamente em falhas transitórias
                MessageSendMaxRetries = 3,
                RetryBackoffMs = 1000,

                // Evita duplicatas em retentativas (idempotência no producer)
                EnableIdempotence = true,

                // Ordena mensagens por partição mesmo com retentativas
                MaxInFlight = 5
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }

        public async Task PublicarAsync(LancamentoRegistradoEvent evento, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(evento);

            // Chave = data do lançamento → garante ordering por dia na mesma partição
            var message = new Message<string, string>
            {
                Key = evento.Data.ToString("yyyy-MM-dd"),
                Value = json,
                Headers = new Headers
            {
                { "eventType", System.Text.Encoding.UTF8.GetBytes(nameof(LancamentoRegistradoEvent)) },
                { "lancamentoId", System.Text.Encoding.UTF8.GetBytes(evento.LancamentoId.ToString()) }
            }
            };

            var result = await _producer.ProduceAsync(_topic, message, ct);

            _logger.LogInformation(
                "Evento publicado no Kafka. Topic: {Topic} | Partition: {Partition} | Offset: {Offset}",
                result.Topic,
                result.Partition.Value,
                result.Offset.Value);
        }

        public void Dispose()
        {
            // Flush garante que mensagens em buffer sejam entregues antes de fechar
            _producer.Flush(TimeSpan.FromSeconds(10));
            _producer.Dispose();
        }
    }
}
