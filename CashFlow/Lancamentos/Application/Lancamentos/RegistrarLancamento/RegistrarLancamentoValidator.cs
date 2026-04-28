using FluentValidation;

namespace Lancamentos.Application.Lancamentos.RegistrarLancamento
{
    public class RegistrarLancamentoValidator : AbstractValidator<RegistrarLancamentoCommand>
    {
        public RegistrarLancamentoValidator() {
            RuleFor(x => x.Valor).GreaterThan(0).WithMessage("O valor do lançamento deve ser maior que zero");
            
            RuleFor(x => x.Tipo)
            .IsInEnum().WithMessage("Tipo de lançamento inválido. Use 1 (Débito) ou 2 (Crédito).");
            
            RuleFor(x => x.MeioLancamento)
            .IsInEnum().WithMessage("Meio de lançamento inválido. Use 1 (Dinheiro), 2 (Pix), 3 (Cartão) ou 4 (Transferência).");

            RuleFor(x => x.Descricao)
                .NotEmpty().WithMessage("A descrição é obrigatória.")
                .MaximumLength(255).WithMessage("A descrição deve ter no máximo 255 caracteres.");

            RuleFor(x => x.Data)
                .NotEmpty().WithMessage("A data é obrigatória.")
                .LessThanOrEqualTo(DateOnly.FromDateTime(DateTime.UtcNow))
                .WithMessage("A data não pode ser futura.");
        }

    }
}
