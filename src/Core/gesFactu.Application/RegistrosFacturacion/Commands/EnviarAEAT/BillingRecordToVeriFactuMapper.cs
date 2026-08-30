using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Traduce un BillingRecord al XML de remisión AEAT correspondiente a su tipo:
/// RegistroAlta o RegistroAnulacion.
/// </summary>
public static class BillingRecordToVeriFactuMapper
{
    public static VeriFactuSubmissionRequest MapToSubmissionRequest(
        BillingRecord billingRecord,
        IRegistroAltaXmlBuilder altaXmlBuilder,
        IRegistroAnulacionXmlBuilder cancellationXmlBuilder,
        BillingRecord? previousRecord = null,
        BillingRecord? rectifiedRecord = null)
    {
        ArgumentNullException.ThrowIfNull(billingRecord);
        ArgumentNullException.ThrowIfNull(altaXmlBuilder);
        ArgumentNullException.ThrowIfNull(cancellationXmlBuilder);

        ValidateCommon(billingRecord, previousRecord);

        var xmlContent = billingRecord.RecordType switch
        {
            BillingRecord.CancellationRecordType =>
                BuildCancellationXml(
                    billingRecord,
                    cancellationXmlBuilder,
                    previousRecord),

            BillingRecord.AltaRecordType =>
                BuildAltaXml(
                    billingRecord,
                    altaXmlBuilder,
                    previousRecord,
                    rectifiedRecord),

            _ => throw new InvalidOperationException(
                $"Tipo de registro no soportado: {billingRecord.RecordType}.")
        };

        return new VeriFactuSubmissionRequest
        {
            TaxpayerNif = billingRecord.IssuerNif,
            SignedXmlContent = xmlContent,
            PreviousRecordHash = billingRecord.PreviousRecordHash
        };
    }

    private static string BuildAltaXml(
        BillingRecord billingRecord,
        IRegistroAltaXmlBuilder xmlBuilder,
        BillingRecord? previousRecord,
        BillingRecord? rectifiedRecord)
    {
        var baseImponible = billingRecord.TotalAmount - billingRecord.TotalTaxAmount;
        var detalles = new List<DetalleDesgloseData>
        {
            new()
            {
                Impuesto = "01",
                ClaveRegimen = "01",
                CalificacionOperacion = "S1",
                TipoImpositivo = baseImponible > 0
                    ? Math.Round(
                        billingRecord.TotalTaxAmount / baseImponible * 100,
                        2)
                    : (decimal?)null,
                BaseImponible = baseImponible,
                CuotaRepercutida = billingRecord.TotalTaxAmount
            }
        };

        if (billingRecord.RectifiesBillingRecordId.HasValue)
        {
            if (rectifiedRecord is null ||
                rectifiedRecord.Id != billingRecord.RectifiesBillingRecordId.Value)
            {
                throw new InvalidOperationException(
                    "La rectificativa requiere reconstruir la factura rectificada persistida.");
            }
        }
        else if (rectifiedRecord is not null)
        {
            throw new InvalidOperationException(
                "Se proporcionó una factura rectificada para un registro que no es rectificativo.");
        }

        var rectifiedInvoices = rectifiedRecord is null
            ? Array.Empty<FacturaRectificadaData>()
            : new[]
            {
                new FacturaRectificadaData
                {
                    IssuerNif = rectifiedRecord.IssuerNif,
                    InvoiceSeries = rectifiedRecord.InvoiceSeries,
                    InvoiceNumber = rectifiedRecord.InvoiceNumber,
                    IssueDate = rectifiedRecord.IssueDate
                }
            };

        ImporteRectificacionData? rectificationAmount = null;
        if (billingRecord.RectificationType == "S")
        {
            if (!billingRecord.RectifiedBaseAmount.HasValue ||
                !billingRecord.RectifiedTaxAmount.HasValue)
            {
                throw new InvalidOperationException(
                    "Rectificativa S requiere BaseRectificada y CuotaRectificada.");
            }

            rectificationAmount = new ImporteRectificacionData
            {
                BaseRectificada = billingRecord.RectifiedBaseAmount.Value,
                CuotaRectificada = billingRecord.RectifiedTaxAmount.Value,
                CuotaRecargoRectificado = billingRecord.RectifiedSurchargeAmount
            };
        }

        var data = new RegistroAltaData
        {
            IssuerNif = billingRecord.IssuerNif,
            IssuerName = billingRecord.IssuerName,
            InvoiceSeries = billingRecord.InvoiceSeries,
            InvoiceNumber = billingRecord.InvoiceNumber,
            IssueDate = billingRecord.IssueDate,
            RecipientNif = billingRecord.RecipientNif,
            RecipientName = billingRecord.RecipientName,
            IsSubsanacion = billingRecord.SubsanatesBillingRecordId.HasValue,
            TipoFactura = billingRecord.InvoiceType,
            TipoRectificativa = billingRecord.RectificationType,
            FacturasRectificadas = rectifiedInvoices,
            ImporteRectificacion = rectificationAmount,
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

        return xmlBuilder.BuildRegFactuXml(data);
    }

    private static string BuildCancellationXml(
        BillingRecord billingRecord,
        IRegistroAnulacionXmlBuilder xmlBuilder,
        BillingRecord? previousRecord)
    {
        if (!billingRecord.CancelsBillingRecordId.HasValue)
        {
            throw new InvalidOperationException(
                "RegistroAnulacion requiere CancelsBillingRecordId.");
        }

        var data = new RegistroAnulacionData
        {
            IssuerNif = billingRecord.IssuerNif,
            IssuerName = billingRecord.IssuerName,
            InvoiceSeries = billingRecord.InvoiceSeries,
            InvoiceNumber = billingRecord.InvoiceNumber,
            IssueDate = billingRecord.IssueDate,
            ComputedHash = billingRecord.ComputedHash!,
            PreviousRecordHash = billingRecord.PreviousRecordHash,
            PreviousIssueDate = previousRecord?.IssueDate,
            PreviousIssuerNif = previousRecord?.IssuerNif,
            PreviousInvoiceSeries = previousRecord?.InvoiceSeries,
            PreviousInvoiceNumber = previousRecord?.InvoiceNumber,
            FechaHoraHusoGenRegistro = billingRecord.RegisterTimestamp,
            SinRegistroPrevio = null,
            RechazoPrevio = null
        };

        return xmlBuilder.BuildRegFactuXml(data);
    }

    private static void ValidateCommon(
        BillingRecord billingRecord,
        BillingRecord? previousRecord)
    {
        if (string.IsNullOrWhiteSpace(billingRecord.ComputedHash))
        {
            throw new InvalidOperationException(
                "BillingRecord debe tener ComputedHash antes de generar XML.");
        }

        if (string.IsNullOrWhiteSpace(billingRecord.RegisterTimestamp))
        {
            throw new InvalidOperationException(
                "BillingRecord debe tener RegisterTimestamp antes de generar XML.");
        }

        if (billingRecord.PreviousBillingRecordId.HasValue && previousRecord is null)
        {
            throw new InvalidOperationException(
                "El RF anterior es obligatorio para construir RegistroAnterior.");
        }

        if (previousRecord is not null &&
            (!billingRecord.PreviousBillingRecordId.HasValue ||
             billingRecord.PreviousBillingRecordId.Value != previousRecord.Id ||
             !string.Equals(
                 billingRecord.PreviousRecordHash,
                 previousRecord.ComputedHash,
                 StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "La referencia persistida al RF anterior no coincide con el registro proporcionado.");
        }
    }
}
