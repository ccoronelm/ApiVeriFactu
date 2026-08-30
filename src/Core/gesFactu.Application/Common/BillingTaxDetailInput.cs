namespace gesFactu.Application.Common;

/// <summary>
/// Entrada agnóstica de infraestructura para un DetalleDesglose VERI*FACTU.
/// </summary>
public sealed record BillingTaxDetailInput(
    string? TaxCode,
    string? RegimeCode,
    string? OperationQualification,
    string? ExemptionCause,
    decimal? TaxRate,
    decimal TaxBase,
    decimal? TaxAmount,
    decimal? EquivalenceSurchargeRate = null,
    decimal? EquivalenceSurchargeAmount = null);
