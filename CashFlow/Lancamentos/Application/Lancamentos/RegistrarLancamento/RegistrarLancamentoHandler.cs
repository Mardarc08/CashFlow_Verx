using Lancamentos.Application.Events;
using Lancamentos.Domain.Interfaces;
using Lancamentos.Domain.Entities;
using Lancamentos.Infrastructure.Messaging;
using FluentValidation;
using MediatR;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento
{
    public class RegistrarLancamentoHandler : IRequestHandler<RegistrarLancamentoCommand, RegistrarLancamentoResponse>
    {
        private readonly ILancamentoRepository _repository;
        private readonly IKafkaProducer _producer;
        private readonly IValidator<RegistrarLancamentoCommand> _validator;
        private readonly ILogger<RegistrarLancamentoHandler> _logger;

        public RegistrarLancamentoHandler(
            ILancamentoRepository repository,
        IKafkaProducer producer,
            IValidator<RegistrarLancamentoCommand> validator,
            ILogger<RegistrarLancamentoHandler> logger)
        {
            _repository = repository;
            _producer = producer;
            _validator = validator;
            _logger = logger;
        }

        public async Task<RegistrarLancamentoResponse> Handle(RegistrarLancamentoCommand registrarLancamento, CancellationToken cancellationToken)
        {
            // Validar se o lancamento recebido atende aos requisitos
            var validation = await _validator.ValidateAsync(registrarLancamento, cancellationToken);
            if (!validation.IsValid)
                throw new ValidationException(validation.Errors);

            // Criar entidade de domínio a partir do contrato recebido
            var lancamento = Lancamento.Criar(registrarLancamento.Valor, registrarLancamento.Tipo, registrarLancamento.Descricao, registrarLancamento.Data, registrarLancamento.MeioLancamento);

            // Persiste os dados
            await _repository.AdicionarLancamentoAsync(lancamento, cancellationToken);


            // 4. Publicar evento no Kafka
            try
            {
                var evento = new LancamentoRegistradoEvent(
                    lancamento.Id,
                    lancamento.Valor,
                    lancamento.Tipo,
                    lancamento.MeioLancamento,
                    lancamento.Data,
                    DateTime.UtcNow);

                await _producer.PublicarAsync(evento, cancellationToken);
            }
            catch (Exception ex)
            {
                // Lançamento já persistido — falha no Kafka não derruba a operação
                // Pode ser reconciliado via replay do offset ou reprocessamento manual
                _logger.LogError(ex, "Falha ao publicar evento Kafka para o lançamento {Id}.", lancamento.Id);
            }

            _logger.LogInformation("Lançamento {Id} registrado com sucesso.", lancamento.Id);

            return new RegistrarLancamentoResponse(lancamento.Id, lancamento.DataCriacao);
        }
    }
}
