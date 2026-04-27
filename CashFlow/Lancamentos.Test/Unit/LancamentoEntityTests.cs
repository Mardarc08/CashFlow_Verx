using FluentAssertions;
using Lancamentos.Domain.Entities;
using Lancamentos.Domain.Enum;
using Xunit;

namespace Testes.Unit
{
    public class LancamentoEntityTests
    {
        [Fact]
        public void Criar_ComDadosValidos_DeveRetornarLancamentoCorreto()
        {
            // Arrange
            var valor = 250.75m;
            var tipo = TipoLancamento.Credito;
            var descricao = "Pagamento de cliente";
            var data = new DateOnly(2025, 1, 15);
            var meio = MeioLancamento.Cartao;

            // Act
            var lancamento = Lancamento.Criar(valor, tipo, descricao, data, meio);

            // Assert
            lancamento.Id.Should().NotBeEmpty();
            lancamento.Valor.Should().Be(valor);
            lancamento.Tipo.Should().Be(tipo);
            lancamento.Descricao.Should().Be(descricao);
            lancamento.Data.Should().Be(data);
            lancamento.DataCriacao.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        }

        [Fact]
        public void Criar_DoisLancamentos_DevemTerIdsDistintos()
        {
            // Act
            var l1 = Lancamento.Criar(100m, TipoLancamento.Credito, "L1", DateOnly.FromDateTime(DateTime.UtcNow), MeioLancamento.Pix);
            var l2 = Lancamento.Criar(100m, TipoLancamento.Credito, "L2", DateOnly.FromDateTime(DateTime.UtcNow), MeioLancamento.Dinheiro);

            // Assert
            l1.Id.Should().NotBe(l2.Id);
        }
    }
}
