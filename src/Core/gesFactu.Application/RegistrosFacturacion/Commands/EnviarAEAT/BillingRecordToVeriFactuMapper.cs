using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Mapper que traduce modelos de dominio a solicitudes AEAT.
/// 
/// Parte de la Anti-Corruption Layer: convierte conceptos fiscales internos
/// a estructuras AEAT SOAP/XML sin exponer WSDL en Domain.
/// 
/// Ref: /VERIFACTU - Estructura de RegistroFacturacionAltaType
/// </summary>
public sealed class BillingRecordToVeriFactuMapper
{
    /// <summary>
    /// Mapea un BillingRecord a una solicitud AEAT.
    /// 
    /// Por ahora, genera XML básico. En producción, usaría librerías de serialización
    /// tipadas según XSD oficial.
    /// </summary>
    public static VeriFactuSubmissionRequest MapToSubmissionRequest(
        BillingRecord billingRecord,
        string certificatePath)
    {
        if (billingRecord == null)
            throw new ArgumentNullException(nameof(billingRecord));

        if (string.IsNullOrWhiteSpace(billingRecord.ComputedHash))
            throw new InvalidOperationException("BillingRecord debe tener un hash calculado antes de enviar");

        // En esta fase MVP, generamos XML simple.
        // En producción, usaríamos XSD tipado o generado de WSDL.
        var xmlContent = GenerateSimpleXml(billingRecord);

        return new VeriFactuSubmissionRequest
        {
            TaxpayerNif = billingRecord.IssuerNif,
            SignedXmlContent = xmlContent,  // En producción: firmado con certificado
            PreviousRecordHash = billingRecord.PreviousRecordHash
        };
    }

    /// <summary>
    /// Genera XML básico para la solicitud.
    /// 
    /// IMPORTANTE: En producción, esto debe:
    /// - Usar XSD oficiales de /VERIFACTU
    /// - Incluir firma digital con certificado
    /// - Respetar exactamente la estructura SOAP esperada
    /// - Validar contra XSD antes de enviar
    /// 
    /// Por ahora es solo estructura esquemática para MVP.
    /// </summary>
    private static string GenerateSimpleXml(BillingRecord billingRecord)
    {
        var xml = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<!-- NOTA: Este XML es ESQUEMÁTICO. En producción debe cumplir /VERIFACTU/SuministroInformacion.xsd.xml -->
<RegistroFacturacionAlta>
    <Contribuyente>
        <NIF>{billingRecord.IssuerNif}</NIF>
    </Contribuyente>
    <Factura>
        <Identificacion>
            <Serie>{billingRecord.InvoiceSeries}</Serie>
            <Numero>{billingRecord.InvoiceNumber}</Numero>
            <FechaExpedicion>{billingRecord.IssueDate:yyyy-MM-dd}</FechaExpedicion>
        </Identificacion>
        <Detalles>
            <ConceptoFactura>{billingRecord.Description}</ConceptoFactura>
            <ImporteTotal>{billingRecord.TotalAmount:F2}</ImporteTotal>
            <ImporteImpuesto>{billingRecord.TotalTaxAmount:F2}</ImporteImpuesto>
        </Detalles>
        <Huella>
            <HuellaAnterior>{(billingRecord.PreviousRecordHash ?? "PRIMERA")}</HuellaAnterior>
            <HuellaActual>{billingRecord.ComputedHash}</HuellaActual>
        </Huella>
    </Factura>
</RegistroFacturacionAlta>";

        return xml;
    }
}
