using System.Globalization;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Domain.Entities;

namespace gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;

/// <summary>
/// Política de preparación temporal antes de remitir un RF a AEAT.
///
/// AEAT valida que FechaHoraHusoGenRegistro esté próxima a su hora de sistema.
/// Si el registro lleva demasiado tiempo creado pero todavía no ha sido remitido,
/// se refrescan timestamp y huella justo antes de encolarlo.
///
/// Solo debe aplicarse cuando el registro sea el último RF generado de la cadena,
/// porque cambiar su huella invalidaría cualquier descendiente ya creado.
/// </summary>
public static class SubmissionTimestampPolicy
{
    /// <summary>
    /// Umbral interno conservador. AEAT comunica un margen de 240 segundos;
    /// refrescamos a partir de 120 para dejar holgura al Outbox y a la red.
    /// </summary>
    public static readonly TimeSpan RefreshAfter = TimeSpan.FromSeconds(120);

    public static bool RequiresRefresh(
        BillingRecord record,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);

        if (!DateTimeOffset.TryParseExact(
                record.RegisterTimestamp,
                "yyyy-MM-ddTHH:mm:sszzz",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var registerTimestamp))
        {
            throw new InvalidOperationException(
                "FechaHoraHusoGenRegistro no tiene el formato esperado yyyy-MM-ddTHH:mm:sszzz.");
        }

        var skew = now.ToUniversalTime() - registerTimestamp.ToUniversalTime();

        return Math.Abs(skew.TotalSeconds) >= RefreshAfter.TotalSeconds;
    }

    public static void RefreshTimestampAndHash(
        BillingRecord record,
        IHashCalculator hashCalculator,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(hashCalculator);

        record.RegisterTimestamp = now.ToString(
            "yyyy-MM-ddTHH:mm:sszzz",
            CultureInfo.InvariantCulture);

        var issueDate = record.IssueDate.ToString(
            "dd-MM-yyyy",
            CultureInfo.InvariantCulture);

        if (record.RecordType == BillingRecord.CancellationRecordType)
        {
            record.SetComputedHash(
                hashCalculator.CalculateCancellationHash(
                    new CancellationRecordHashInput
                    {
                        PreviousHash = record.PreviousRecordHash ?? string.Empty,
                        IssuerNif = record.IssuerNif,
                        InvoiceSeries = record.InvoiceSeries,
                        InvoiceNumber = record.InvoiceNumber,
                        IssueDate = issueDate,
                        RegisterTimestamp = record.RegisterTimestamp
                    }));

            return;
        }

        record.SetComputedHash(
            hashCalculator.CalculateChainHash(
                new BillingRecordHashInput
                {
                    PreviousHash = record.PreviousRecordHash ?? string.Empty,
                    IssuerNif = record.IssuerNif,
                    InvoiceSeries = record.InvoiceSeries,
                    InvoiceNumber = record.InvoiceNumber,
                    IssueDate = issueDate,
                    InvoiceType = record.InvoiceType,
                    TotalAmount = record.TotalAmount,
                    TotalTaxAmount = record.TotalTaxAmount,
                    RegisterTimestamp = record.RegisterTimestamp
                }));
    }
}
