using FluentValidation;

namespace gesFactu.Application.RegistrosFacturacion.Commands.CrearRegistro;

/// <summary>
/// Validación de entrada para RegistroAlta F1/F2.
/// </summary>
public sealed class CreateBillingRecordCommandValidator
    : AbstractValidator<CreateBillingRecordCommand>
{
    public CreateBillingRecordCommandValidator()
    {
        RuleFor(x => x.IssuerNif)
            .NotEmpty().WithMessage("El NIF/CIF del emisor es requerido")
            .Length(9).WithMessage("El NIF debe tener exactamente 9 caracteres");

        RuleFor(x => x.InvoiceSeries)
            .NotEmpty().WithMessage("La serie de factura es requerida")
            .MaximumLength(60).WithMessage(
                "La serie de factura no puede superar 60 caracteres");

        RuleFor(x => x.InvoiceNumber)
            .NotEmpty().WithMessage("El número de factura es requerido")
            .MaximumLength(60).WithMessage(
                "El número de factura no puede superar 60 caracteres");

        RuleFor(x => x.IssueDate)
            .NotEmpty().WithMessage("La fecha de expedición es requerida")
            .Must(IsValidDate).WithMessage(
                "La fecha debe tener formato dd-MM-yyyy");

        RuleFor(x => x.IssuerName)
            .NotEmpty().WithMessage("El nombre del emisor es requerido")
            .MaximumLength(120).WithMessage(
                "El nombre del emisor no puede superar 120 caracteres");

        RuleFor(x => x.InvoiceType)
            .NotEmpty()
            .Must(x => x.Trim().ToUpperInvariant() is "F1" or "F2")
            .WithMessage("TipoFactura debe ser F1 o F2.");

        When(x => IsF1(x.InvoiceType), () =>
        {
            RuleFor(x => x.RecipientNif)
                .NotEmpty().WithMessage(
                    "El NIF del destinatario es requerido para F1")
                .Length(9).WithMessage(
                    "El NIF del destinatario debe tener exactamente 9 caracteres");

            RuleFor(x => x.RecipientName)
                .NotEmpty().WithMessage(
                    "El nombre o razón social del destinatario es requerido para F1")
                .MaximumLength(120).WithMessage(
                    "El nombre o razón social del destinatario no puede superar 120 caracteres");
        });

        When(
            x => IsF2(x.InvoiceType) &&
                 (!string.IsNullOrWhiteSpace(x.RecipientNif) ||
                  !string.IsNullOrWhiteSpace(x.RecipientName)),
            () =>
            {
                RuleFor(x => x.RecipientNif)
                    .NotEmpty()
                    .Length(9).WithMessage(
                        "Si F2 identifica destinatario, su NIF debe tener exactamente 9 caracteres");

                RuleFor(x => x.RecipientName)
                    .NotEmpty()
                    .MaximumLength(120).WithMessage(
                        "El nombre del destinatario no puede superar 120 caracteres");
            });

        RuleFor(x => x)
            .Must(x =>
                string.IsNullOrWhiteSpace(x.RecipientNif) ||
                !string.Equals(
                    x.IssuerNif?.Trim(),
                    x.RecipientNif.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            .WithMessage(
                "El NIF del destinatario debe ser distinto del NIF del obligado emisor")
            .WithName("RecipientNif");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("La descripción de la operación es requerida")
            .MaximumLength(500).WithMessage(
                "La descripción no puede superar 500 caracteres");

        RuleFor(x => x.TotalAmount)
            .GreaterThanOrEqualTo(0).WithMessage(
                "El importe total no puede ser negativo")
            .Must(HasMaxTwoDecimals).WithMessage(
                "El importe total no puede tener más de 2 decimales");

        RuleFor(x => x.TotalTaxAmount)
            .GreaterThanOrEqualTo(0).WithMessage(
                "La cuota total no puede ser negativa")
            .Must(HasMaxTwoDecimals).WithMessage(
                "La cuota total no puede tener más de 2 decimales");

        RuleFor(x => x)
            .Must(x => x.TotalTaxAmount <= x.TotalAmount)
            .WithMessage(
                "La cuota de impuesto no puede ser mayor que el importe total")
            .WithName("TotalTaxAmount");
    }

    private static bool IsF1(string? invoiceType)
        => string.Equals(
            invoiceType?.Trim(),
            "F1",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsF2(string? invoiceType)
        => string.Equals(
            invoiceType?.Trim(),
            "F2",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsValidDate(string dateString)
        => DateTime.TryParseExact(
            dateString,
            "dd-MM-yyyy",
            null,
            System.Globalization.DateTimeStyles.None,
            out _);

    private static bool HasMaxTwoDecimals(decimal value)
        => decimal.Round(value, 2) == value;
}
