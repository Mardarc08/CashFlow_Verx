using FluentAssertions;
using FluentValidation;
using Lancamentos.Application.Events;
using Lancamentos.Application.Lancamentos.RegistrarLancamento;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Enum;
using Lancamentos.Domain.Interfaces;
using Lancamentos.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace Lancamentos.Test.Unit
{
    public class RegistrarLancamentoHandlerTests
    {
        private readonly ILancamentoRepository _repository = Substitute.For<ILancamentoRepository>();
        private readonly IKafkaProducer _producer = Substitute.For<IKafkaProducer>();
        private readonly ILogger<RegistrarLancamentoHandler> _logger = Substitute.For<ILogger<RegistrarLancamentoHandler>>();
        private readonly IValidator<RegistrarLancamentoCommand> _validator;
        private readonly RegistrarLancamentoHandler _handler;

        public RegistrarLancamentoHandlerTests()
        {
            _validator = new RegistrarLancamentoValidator();
            _handler = new RegistrarLancamentoHandler(_repository, _producer, _validator, _logger);
        }

        [Fact]
        public async Task Handle_ComDadosValidos_DeveRegistrarEPublicarEvento()
        {
            // Arrange
            var command = new RegistrarLancamentoCommand(
                Valor: 150.00m,
                Tipo: TipoLancamento.Credito,
                MeioLancamento: MeioLancamento.Dinheiro,
                Descricao: "Venda à vista",
                Data: DateOnly.FromDateTime(DateTime.UtcNow));

            // Act
            var response = await _handler.Handle(command, CancellationToken.None);

            // Assert
            response.Id.Should().NotBeEmpty();
            response.CriadoEm.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

            await _repository.Received(1).AdicionarLancamentoAsync(Arg.Any<Lancamento>(), Arg.Any<CancellationToken>());
            await _producer.Received(1).PublicarAsync(Arg.Any<LancamentoRegistradoEvent>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ComValorZero_DeveLancarValidationException()
        {
            // Arrange
            var command = new RegistrarLancamentoCommand(
                Valor: 0,
                Tipo: TipoLancamento.Debito,
                MeioLancamento: MeioLancamento.Dinheiro,
                Descricao: "Teste",
                Data: DateOnly.FromDateTime(DateTime.UtcNow));

            // Act
            var act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*valor deve ser maior que zero*");
        }

        [Fact]
        public async Task Handle_ComValorNegativo_DeveLancarValidationException()
        {
            // Arrange
            var command = new RegistrarLancamentoCommand(
                Valor: -50m,
                Tipo: TipoLancamento.Debito,
                MeioLancamento: MeioLancamento.Dinheiro,
                Descricao: "Teste",
                Data: DateOnly.FromDateTime(DateTime.UtcNow));

            // Act
            var act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ValidationException>();
        }

        [Fact]
        public async Task Handle_ComDescricaoVazia_DeveLancarValidationException()
        {
            // Arrange
            var command = new RegistrarLancamentoCommand(
                Valor: 100m,
                Tipo: TipoLancamento.Credito,
                MeioLancamento: null,
                Descricao: "",
                Data: DateOnly.FromDateTime(DateTime.UtcNow));

            // Act
            var act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*descrição é obrigatória*");
        }

        [Fact]
        public async Task Handle_FalhaNoPubSub_NaoDevePropagarErro()
        {
            // Arrange — Pub/Sub falha mas lançamento deve ser persistido
            var command = new RegistrarLancamentoCommand(
                Valor: 200m,
                Tipo: TipoLancamento.Credito,
                MeioLancamento: MeioLancamento.Pix,
                Descricao: "Pagamento cliente",
                Data: DateOnly.FromDateTime(DateTime.UtcNow));

            _producer
                .PublicarAsync(Arg.Any<LancamentoRegistradoEvent>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new Exception("PubSub indisponível"));

            // Act
            var act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert — não deve lançar exceção; resiliência é o comportamento esperado
            await act.Should().NotThrowAsync();
            await _repository.Received(1).AdicionarLancamentoAsync(Arg.Any<Lancamento>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_ComDataFutura_DeveLancarValidationException()
        {
            // Arrange
            var command = new RegistrarLancamentoCommand(
                Valor: 100m,
                Tipo: TipoLancamento.Credito,
                MeioLancamento: MeioLancamento.Dinheiro,
                Descricao: "Venda futura",
                Data: DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)));

            // Act
            var act = async () => await _handler.Handle(command, CancellationToken.None);

            // Assert
            await act.Should().ThrowAsync<ValidationException>()
                .WithMessage("*data não pode ser futura*");
        }
    }
}
