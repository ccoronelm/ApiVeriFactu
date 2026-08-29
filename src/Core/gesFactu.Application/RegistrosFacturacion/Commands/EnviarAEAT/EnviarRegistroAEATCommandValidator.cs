using FluentValidation;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Validador para el comando de envío a AEAT.
/// </summary>
public sealed class EnviarRegistroAEATCommandValidator : AbstractValidator<EnviarRegistroAEATCommand>
{
    public EnviarRegistroAEATCommandValidator()
    {
        RuleFor(x => x.BillingRecordId)
            .GreaterThan(0)
            .WithMessage("El ID del registro debe ser válido (mayor que 0)");
    }
}
