namespace Consolidado.Domain.Entities
{
    public class ConsolidadoDiario
    {
        public Guid Id { get; private set; }
        public DateOnly Data { get; private set; }
        public decimal TotalCreditos { get; private set; }
        public decimal TotalDebitos { get; private set; }
        public decimal SaldoFinal => TotalCreditos - TotalDebitos;
        public DateTime AtualizadoEm { get; private set; }

        private ConsolidadoDiario() { } // EF Core

        public static ConsolidadoDiario Criar(DateOnly data) => new()
        {
            Id = Guid.NewGuid(),
            Data = data,
            TotalCreditos = 0,
            TotalDebitos = 0,
            AtualizadoEm = DateTime.UtcNow
        };

        public void AplicarCredito(decimal valor)
        {
            TotalCreditos += valor;
            AtualizadoEm = DateTime.UtcNow;
        }

        public void AplicarDebito(decimal valor)
        {
            TotalDebitos += valor;
            AtualizadoEm = DateTime.UtcNow;
        }
    }
}
