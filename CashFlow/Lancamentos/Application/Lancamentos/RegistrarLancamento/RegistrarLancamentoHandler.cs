using FluentValidation;
using Lancamentos.Application.Events;
using Lancamentos.Domain.Interfaces;
using Lancamentos.Domain.Entities;
using MediatR;
using Lancamentos.Infrastructure.Messaging;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento
{
    public class RegistrarLancamentoHandler : IRequestHandler<RegistrarLancamentoCommand, RegistrarLancamentoResponse>
    {
        private readonly ILancamentoRepository _repository;
        private readonly IPubSubPublisher _publisher;
        private readonly IValidator<RegistrarLancamentoCommand> _validator;
        private readonly ILogger<RegistrarLancamentoHandler> _logger;

        public RegistrarLancamentoHandler(
            ILancamentoRepository repository,
            IPubSubPublisher publisher,
            IValidator<RegistrarLancamentoCommand> validator,
            ILogger<RegistrarLancamentoHandler> logger)
        {
            _repository = repository;
            _publisher = publisher;
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

            // Publicar evento (fire-and-forget seguro — falha aqui não derruba o lançamento)
            try
            {
                var evento = new LancamentoRegistradoEvent(
                    lancamento.Id,
                    lancamento.Valor,
                    lancamento.Tipo,
                    lancamento.MeioLancamento,
                    lancamento.Data,
                    DateTime.UtcNow);

                await _publisher.PublicarAsync(evento, cancellationToken);
            }
            catch (Exception ex)
            {
                // Log da falha mas não propaga — lançamento já foi persistido
                // O consolidado pode ser recalculado via reconciliação
                _logger.LogError(ex, "Falha ao publicar evento para o lançamento {Id}. Será reconciliado.", lancamento.Id);
            }

            _logger.LogInformation("Lançamento {Id} registrado com sucesso.", lancamento.Id);

            return new RegistrarLancamentoResponse(lancamento.Id, lancamento.DataCriacao);
        }
    }
}
