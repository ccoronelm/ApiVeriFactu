using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Mapper que traduce un BillingRecord al payload de envío a AEAT.
///
/// Responsabilidad: construir RegistroAltaData con los datos fiscales correctos
/// y delegar la generación del XML a la capa de Infrastructure (IRegistroAltaXmlBuilder).
///
/// NO genera XML directamente — ese detalle pertenece a Infrastructure.
///
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml — RegistroFacturacionAltaType
/// </summary>
public static class BillingRecordToVeriFactuMapper
{
    /// <summary>
    /// Mapea un BillingRecord a una solicitud de envío AEAT.
    ///
    /// Precondición: el registro debe tener ComputedHash calculado.
    /// El XML se genera invocando IRegistroAltaXmlBuilder (Infrastructure).
    /// </summary>
    public static VeriFactuSubmissionRequest MapToSubmissionRequest(
        BillingRecord billingRecord,
        IRegistroAltaXmlBuilder xmlBuilder,
        BillingRecord? previousRecord = null,
        string? fechaHoraHusoGenRegistro = null)
    {
        ArgumentNullException.ThrowIfNull(billingRecord);
        ArgumentNullException.ThrowIfNull(xmlBuilder);

        if (string.IsNullOrWhiteSpace(billingRecord.ComputedHash))
            throw new InvalidOperationException(
                "BillingRecord debe tener ComputedHash calculado antes de generar el XML de envío.");

        // FechaHoraHusoGenRegistro: si no se proporciona, usar ahora con offset local
        var fechaHora = fechaHoraHusoGenRegistro
            ?? DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture);

        // Construir desglose. Para esta fase (factura ordinaria F1):
        // Se infiere un único DetalleDesglose a partir de CuotaTotal e ImporteTotal.
        // En fases posteriores, el desglose real vendrá del comando/dominio.
        var baseImponible = billingRecord.TotalAmount - billingRecord.TotalTaxAmount;
        var detalles = new List<DetalleDesgloseData>
        {
            new()
            {
                Impuesto             = "01",   // IVA — Ref: SuministroInformacion.xsd ImpuestoType
                ClaveRegimen         = "01",   // Régimen general
                CalificacionOperacion = "S1",  // Sujeta y no exenta sin ISP
                TipoImpositivo       = baseImponible > 0
                    ? Math.Round(billingRecord.TotalTaxAmount / baseImponible * 100, 2)
                    : (decimal?)null,
                BaseImponible        = baseImponible,
                CuotaRepercutida     = billingRecord.TotalTaxAmount,
            }
        };

        var data = new RegistroAltaData
        {
            IssuerNif     = billingRecord.IssuerNif,
            IssuerName    = billingRecord.IssuerName,
            InvoiceSeries = billingRecord.InvoiceSeries,
            InvoiceNumber = billingRecord.InvoiceNumber,
            IssueDate     = billingRecord.IssueDate,
            TipoFactura   = "F1",  // Factura ordinaria — Ref: ClaveTipoFacturaType
            Description   = billingRecord.Description,
            CuotaTotal    = billingRecord.TotalTaxAmount,
            ImporteTotal  = billingRecord.TotalAmount,
            Detalles      = detalles,

            ComputedHash       = billingRecord.ComputedHash,
            PreviousRecordHash = billingRecord.PreviousRecordHash,
            PreviousIssueDate  = previousRecord?.IssueDate,
            PreviousIssuerNif  = previousRecord?.IssuerNif,
            PreviousInvoiceSeries = previousRecord?.InvoiceSeries,
            PreviousInvoiceNumber = previousRecord?.InvoiceNumber,

            FechaHoraHusoGenRegistro = fechaHora,
        };

        var xmlContent = xmlBuilder.BuildRegFactuXml(data);

        return new VeriFactuSubmissionRequest
        {
            TaxpayerNif       = billingRecord.IssuerNif,
            SignedXmlContent  = xmlContent,
            PreviousRecordHash = billingRecord.PreviousRecordHash,
        };
    }
}

