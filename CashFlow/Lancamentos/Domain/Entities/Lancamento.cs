using Lancamentos.Domain.Enum;

namespace Lancamentos.Domain.Entities
{
    public class Lancamento
    {
        public Guid Id { get; private set; }
        public decimal Valor { get; private set; }
        public TipoLancamento Tipo { get; private set; }
        public MeioLancamento? MeioLancamento { get; private set; }
        public string Descricao { get; private set; }
        public DateOnly Data { get; private set; }
        public DateTime DataCriacao { get; private set; }

        private Lancamento() { } // EF Core

        public static Lancamento Criar(decimal valor, TipoLancamento tipo, string descricao, DateOnly data, MeioLancamento? meioLancamento)
        {
            return new Lancamento
            {
                Id = Guid.NewGuid(),
                Valor = valor,
                Tipo = tipo,
                MeioLancamento = meioLancamento,
                Descricao = descricao,
                Data = data,
                DataCriacao = DateTime.UtcNow
            };
        }
    }
}
