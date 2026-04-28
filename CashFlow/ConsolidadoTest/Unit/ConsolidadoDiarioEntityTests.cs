using Consolidado.Application.UseCases.ObterConsolidado;
using Consolidado.Domain.Entities;
using Consolidado.Domain.Interface;
using Consolidado.Infrastructure.Cache;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ConsolidadoTest.Unit
{
    public class ConsolidadoDiarioEntityTests
    {
        [Fact]
        public void Criar_DevePossuirSaldoZerado()
        {
            var consolidado = ConsolidadoDiario.Criar(new DateOnly(2025, 1, 15));

            consolidado.TotalCreditos.Should().Be(0);
            consolidado.TotalDebitos.Should().Be(0);
            consolidado.SaldoFinal.Should().Be(0);
        }

        [Fact]
        public void AplicarCredito_DeveAumentarTotalCreditos()
        {
            var consolidado = ConsolidadoDiario.Criar(new DateOnly(2025, 1, 15));

            consolidado.AplicarCredito(300m);
            consolidado.AplicarCredito(200m);

            consolidado.TotalCreditos.Should().Be(500m);
            consolidado.SaldoFinal.Should().Be(500m);
        }

        [Fact]
        public void AplicarDebito_DeveAumentarTotalDebitos()
        {
            var consolidado = ConsolidadoDiario.Criar(new DateOnly(2025, 1, 15));

            consolidado.AplicarCredito(1000m);
            consolidado.AplicarDebito(400m);

            consolidado.TotalDebitos.Should().Be(400m);
            consolidado.SaldoFinal.Should().Be(600m);
        }

        [Fact]
        public void SaldoFinal_DeveSerCreditosMenosDebitos()
        {
            var consolidado = ConsolidadoDiario.Criar(new DateOnly(2025, 1, 15));

            consolidado.AplicarCredito(500m);
            consolidado.AplicarDebito(150m);
            consolidado.AplicarCredito(200m);
            consolidado.AplicarDebito(50m);

            consolidado.SaldoFinal.Should().Be(500m);
        }
    }

    public class ObterConsolidadoHandlerTests
    {
        private readonly IConsolidadoRepository _repository = Substitute.For<IConsolidadoRepository>();
        private readonly IConsolidadoCache _cache = Substitute.For<IConsolidadoCache>();
        private readonly ILogger<ObterConsolidadoHandler> _logger = Substitute.For<ILogger<ObterConsolidadoHandler>>();
        private readonly ObterConsolidadoHandler _handler;

        public ObterConsolidadoHandlerTests()
        {
            _handler = new ObterConsolidadoHandler(_repository, _cache, _logger);
        }

        [Fact]
        public async Task Handle_CacheHit_NaoDeveBuscarNoBanco()
        {
            // Arrange
            var data = new DateOnly(2025, 1, 15);
            var cachedDto = new ConsolidadoDto(data, 500m, 200m, 300m, DateTime.UtcNow);
            _cache.ObterAsync(data).Returns(cachedDto);

            // Act
            var result = await _handler.Handle(new ObterConsolidadoQuery(data), CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.SaldoFinal.Should().Be(300m);
            await _repository.DidNotReceive().ObterPorDataAsync(Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_CacheMiss_DeveBuscarNoBancoEPopularCache()
        {
            // Arrange
            var data = new DateOnly(2025, 1, 15);
            var consolidado = ConsolidadoDiario.Criar(data);
            consolidado.AplicarCredito(1000m);
            consolidado.AplicarDebito(300m);

            _cache.ObterAsync(data).Returns((ConsolidadoDto?)null);
            _repository.ObterPorDataAsync(data).Returns(consolidado);

            // Act
            var result = await _handler.Handle(new ObterConsolidadoQuery(data), CancellationToken.None);

            // Assert
            result.Should().NotBeNull();
            result!.TotalCreditos.Should().Be(1000m);
            result.TotalDebitos.Should().Be(300m);
            result.SaldoFinal.Should().Be(700m);

            await _cache.Received(1).SetarAsync(data, Arg.Any<ConsolidadoDto>(), Arg.Any<CancellationToken>());
        }

        [Fact]
        public async Task Handle_SemConsolidadoNoBanco_DeveRetornarNull()
        {
            // Arrange
            var data = new DateOnly(2025, 1, 15);
            _cache.ObterAsync(data).Returns((ConsolidadoDto?)null);
            _repository.ObterPorDataAsync(data).Returns((ConsolidadoDiario?)null);

            // Act
            var result = await _handler.Handle(new ObterConsolidadoQuery(data), CancellationToken.None);

            // Assert
            result.Should().BeNull();
        }
    }
}
