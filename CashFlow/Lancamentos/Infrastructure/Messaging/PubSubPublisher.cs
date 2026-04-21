using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Lancamentos.Application.Events;
using System.Text.Json;

namespace Lancamentos.Infrastructure.Messaging
{
    public interface IPubSubPublisher
    {
        Task PublicarAsync(LancamentoRegistradoEvent evento, CancellationToken ct = default);
    }

    public class PubSubPublisher : IPubSubPublisher
    {
        private readonly PublisherClient _client;
        private readonly ILogger<PubSubPublisher> _logger;

        public PubSubPublisher(PublisherClient client, ILogger<PubSubPublisher> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task PublicarAsync(LancamentoRegistradoEvent evento, CancellationToken ct = default)
        {
            var json = JsonSerializer.Serialize(evento);
            var message = new PubsubMessage
            {
                Data = ByteString.CopyFromUtf8(json),
                Attributes =
            {
                ["eventType"] = nameof(LancamentoRegistradoEvent),
                ["lancamentoId"] = evento.LancamentoId.ToString()
            }
            };

            var messageId = await _client.PublishAsync(message);
            _logger.LogInformation("Evento publicado. MessageId: {MessageId}", messageId);
        }
    }
}
