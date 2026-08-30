using gesFactu.Domain.Common;

namespace gesFactu.Domain.Entities;

/// <summary>
/// Línea persistida del Desglose/DetalleDesglose de un RegistroAlta VERI*FACTU.
/// </summary>
public class BillingTaxDetail : BaseDomainModel
{
    public int BillingRecordId { get; set; }
    public BillingRecord? BillingRecord { get; set; }

    public string? TaxCode { get; set; }
    public string? RegimeCode { get; set; }
    public string? OperationQualification { get; set; }
    public string? ExemptionCause { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal TaxBase { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? EquivalenceSurchargeRate { get; set; }
    public decimal? EquivalenceSurchargeAmount { get; set; }

    private BillingTaxDetail() { }

    public static BillingTaxDetail Create(
        string? taxCode,
        string? regimeCode,
        string? operationQualification,
        string? exemptionCause,
        decimal? taxRate,
        decimal taxBase,
        decimal? taxAmount,
        decimal? equivalenceSurchargeRate = null,
        decimal? equivalenceSurchargeAmount = null)
    {
        var operation = operationQualification?.Trim().ToUpperInvariant();
        var exemption = exemptionCause?.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(operation) == string.IsNullOrWhiteSpace(exemption))
            throw new InvalidOperationException(
                "Cada DetalleDesglose debe informar exactamente CalificacionOperacion u OperacionExenta.");

        if (!string.IsNullOrWhiteSpace(operation) &&
            operation is not ("S1" or "S2" or "N1" or "N2"))
        {
            throw new InvalidOperationException(
                "CalificacionOperacion debe ser S1, S2, N1 o N2.");
        }

        if (!string.IsNullOrWhiteSpace(exemption) &&
            exemption is not ("E1" or "E2" or "E3" or "E4" or "E5" or "E6" or "E7" or "E8"))
        {
            throw new InvalidOperationException(
                "OperacionExenta debe estar entre E1 y E8.");
        }

        ValidateTwoDecimals(taxBase, nameof(taxBase));
        ValidateOptionalPercentage(taxRate, nameof(taxRate));
        ValidateOptionalAmount(taxAmount, nameof(taxAmount));
        ValidateOptionalPercentage(equivalenceSurchargeRate, nameof(equivalenceSurchargeRate));
        ValidateOptionalAmount(equivalenceSurchargeAmount, nameof(equivalenceSurchargeAmount));

        if (equivalenceSurchargeRate.HasValue != equivalenceSurchargeAmount.HasValue)
            throw new InvalidOperationException(
                "TipoRecargoEquivalencia y CuotaRecargoEquivalencia deben informarse juntos.");

        if (!string.IsNullOrWhiteSpace(exemption) || operation is "N1" or "N2")
        {
            if (taxRate.HasValue || taxAmount.HasValue ||
                equivalenceSurchargeRate.HasValue || equivalenceSurchargeAmount.HasValue)
            {
                throw new InvalidOperationException(
                    "Operaciones exentas o no sujetas no pueden informar tipo, cuota ni recargo.");
            }
        }
        else if (operation == "S2")
        {
            if (taxRate != 0m || taxAmount != 0m)
                throw new InvalidOperationException(
                    "S2 requiere TipoImpositivo=0 y CuotaRepercutida=0.");

            if (equivalenceSurchargeRate.HasValue)
                throw new InvalidOperationException(
                    "S2 no puede informar recargo de equivalencia.");
        }
        else if (operation == "S1")
        {
            if (!taxRate.HasValue || !taxAmount.HasValue)
                throw new InvalidOperationException(
                    "S1 requiere TipoImpositivo y CuotaRepercutida.");
        }

        return new BillingTaxDetail
        {
            TaxCode = string.IsNullOrWhiteSpace(taxCode) ? "01" : taxCode.Trim(),
            RegimeCode = string.IsNullOrWhiteSpace(regimeCode) ? "01" : regimeCode.Trim(),
            OperationQualification = operation,
            ExemptionCause = exemption,
            TaxRate = taxRate,
            TaxBase = taxBase,
            TaxAmount = taxAmount,
            EquivalenceSurchargeRate = equivalenceSurchargeRate,
            EquivalenceSurchargeAmount = equivalenceSurchargeAmount
        };
    }

    private static void ValidateTwoDecimals(decimal value, string field)
    {
        if (decimal.Round(value, 2) != value)
            throw new InvalidOperationException($"{field} no puede tener más de 2 decimales.");
    }

    private static void ValidateOptionalAmount(decimal? value, string field)
    {
        if (value.HasValue)
            ValidateTwoDecimals(value.Value, field);
    }

    private static void ValidateOptionalPercentage(decimal? value, string field)
    {
        if (!value.HasValue)
            return;

        if (value.Value < 0m || value.Value > 100m ||
            decimal.Round(value.Value, 2) != value.Value)
        {
            throw new InvalidOperationException(
                $"{field} debe estar entre 0 y 100 y tener máximo 2 decimales.");
        }
    }
}
