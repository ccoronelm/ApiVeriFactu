using gesFactu.Domain.Entities;

namespace gesFactu.Application.Common;

public static class BillingTaxDetailFactory
{
    public static IReadOnlyList<BillingTaxDetail> Create(
        IReadOnlyList<BillingTaxDetailInput>? input,
        decimal totalAmount,
        decimal totalTaxAmount)
    {
        if (input is null || input.Count == 0)
            return [CreateLegacyDetail(totalAmount, totalTaxAmount)];

        if (input.Count > 12)
            throw new InvalidOperationException(
                "VERI*FACTU admite un máximo de 12 DetalleDesglose.");

        var details = input
            .Select(x => BillingTaxDetail.Create(
                x.TaxCode,
                x.RegimeCode,
                x.OperationQualification,
                x.ExemptionCause,
                x.TaxRate,
                x.TaxBase,
                x.TaxAmount,
                x.EquivalenceSurchargeRate,
                x.EquivalenceSurchargeAmount))
            .ToArray();

        var calculatedTax = details.Sum(
            x => (x.TaxAmount ?? 0m) + (x.EquivalenceSurchargeAmount ?? 0m));
        var calculatedTotal = details.Sum(
            x => x.TaxBase +
                 (x.TaxAmount ?? 0m) +
                 (x.EquivalenceSurchargeAmount ?? 0m));

        if (decimal.Round(calculatedTax, 2) != decimal.Round(totalTaxAmount, 2))
        {
            throw new InvalidOperationException(
                $"CuotaTotal ({totalTaxAmount:0.00}) no coincide con la suma de cuotas y recargos ({calculatedTax:0.00}).");
        }

        if (decimal.Round(calculatedTotal, 2) != decimal.Round(totalAmount, 2))
        {
            throw new InvalidOperationException(
                $"ImporteTotal ({totalAmount:0.00}) no coincide con la suma del desglose ({calculatedTotal:0.00}).");
        }

        return details;
    }

    private static BillingTaxDetail CreateLegacyDetail(
        decimal totalAmount,
        decimal totalTaxAmount)
    {
        var taxBase = totalAmount - totalTaxAmount;
        var taxRate = taxBase == 0m
            ? 0m
            : Math.Round(totalTaxAmount / taxBase * 100m, 2);

        return BillingTaxDetail.Create(
            "01",
            "01",
            "S1",
            null,
            taxRate,
            taxBase,
            totalTaxAmount);
    }
}
