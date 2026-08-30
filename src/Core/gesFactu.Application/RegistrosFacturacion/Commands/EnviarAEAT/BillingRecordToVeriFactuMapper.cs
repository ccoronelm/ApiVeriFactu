using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Mapper que traduce un BillingRecord al payload de envío a AEAT.
///
/// El XML se genera mediante IRegistroAltaXmlBuilder en Infrastructure.
/// FechaHoraHusoGenRegistro debe ser exactamente el mismo valor persistido
/// que se usó para calcular la huella.
/// </summary>
public static class BillingRecordToVeriFactuMapper
{
    public static VeriFactuSubmissionRequest MapToSubmissionRequest(
        BillingRecord billingRecord,
        IRegistroAltaXmlBuilder xmlBuilder,
        BillingRecord? previousRecord = null)
    {
        ArgumentNullException.ThrowIfNull(billingRecord);
        ArgumentNullException.ThrowIfNull(xmlBuilder);

        if (string.IsNullOrWhiteSpace(billingRecord.ComputedHash))
        {
            throw new InvalidOperationException(
                "BillingRecord debe tener ComputedHash calculado antes de generar el XML de envío.");
        }

        if (string.IsNullOrWhiteSpace(billingRecord.RegisterTimestamp))
        {
            throw new InvalidOperationException(
                "BillingRecord debe tener RegisterTimestamp persistido antes de generar el XML de envío.");
        }

        var baseImponible = billingRecord.TotalAmount - billingRecord.TotalTaxAmount;
        var detalles = new List<DetalleDesgloseData>
        {
            new()
            {
                Impuesto = "01",
                ClaveRegimen = "01",
                CalificacionOperacion = "S1",
                TipoImpositivo = baseImponible > 0
                    ? Math.Round(billingRecord.TotalTaxAmount / baseImponible * 100, 2)
                    : (decimal?)null,
                BaseImponible = baseImponible,
                CuotaRepercutida = billingRecord.TotalTaxAmount
            }
        };

        var data = new RegistroAltaData
        {
            IssuerNif = billingRecord.IssuerNif,
            IssuerName = billingRecord.IssuerName,
            InvoiceSeries = billingRecord.InvoiceSeries,
            InvoiceNumber = billingRecord.InvoiceNumber,
            IssueDate = billingRecord.IssueDate,
            TipoFactura = "F1",
            Description = billingRecord.Description,
            CuotaTotal = billingRecord.TotalTaxAmount,
            ImporteTotal = billingRecord.TotalAmount,
            Detalles = detalles,

            ComputedHash = billingRecord.ComputedHash,
            PreviousRecordHash = billingRecord.PreviousRecordHash,
            PreviousIssueDate = previousRecord?.IssueDate,
            PreviousIssuerNif = previousRecord?.IssuerNif,
            PreviousInvoiceSeries = previousRecord?.InvoiceSeries,
            PreviousInvoiceNumber = previousRecord?.InvoiceNumber,

            FechaHoraHusoGenRegistro = billingRecord.RegisterTimestamp
        };

        var xmlContent = xmlBuilder.BuildRegFactuXml(data);

        return new VeriFactuSubmissionRequest
        {
            TaxpayerNif = billingRecord.IssuerNif,
            SignedXmlContent = xmlContent,
            PreviousRecordHash = billingRecord.PreviousRecordHash
        };
    }
}
