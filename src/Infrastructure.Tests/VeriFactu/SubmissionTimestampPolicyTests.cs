using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Commands.EnviarAEAT;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.VeriFactu;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class SubmissionTimestampPolicyTests
{
    [Fact]
    public void RequiresRefresh_RegistroReciente_NoRefresca()
    {
        var now = new DateTimeOffset(2026, 8, 30, 13, 0, 0, TimeSpan.FromHours(2));
        var record = CreateRecord("2026-08-30T12:59:00+02:00");

        Assert.False(
            SubmissionTimestampPolicy.RequiresRefresh(record, now));
    }

    [Fact]
    public void RequiresRefresh_RegistroConMasDeDosMinutos_Refresca()
    {
        var now = new DateTimeOffset(2026, 8, 30, 13, 0, 0, TimeSpan.FromHours(2));
        var record = CreateRecord("2026-08-30T12:57:59+02:00");

        Assert.True(
            SubmissionTimestampPolicy.RequiresRefresh(record, now));
    }

    [Fact]
    public void RefreshTimestampAndHash_UsaMismoTimestampEnHuella()
    {
        var now = new DateTimeOffset(2026, 8, 30, 13, 0, 0, TimeSpan.FromHours(2));
        var record = CreateRecord("2026-08-30T12:55:00+02:00");
        var calculator = new Sha256HashCalculator();
        var oldHash = record.ComputedHash;

        SubmissionTimestampPolicy.RefreshTimestampAndHash(
            record,
            calculator,
            now);

        Assert.Equal("2026-08-30T13:00:00+02:00", record.RegisterTimestamp);
        Assert.NotEqual(oldHash, record.ComputedHash);

        var expected = calculator.CalculateChainHash(
            new gesFactu.Application.Common.Abstractions.BillingRecordHashInput
            {
                PreviousHash = record.PreviousRecordHash!,
                IssuerNif = record.IssuerNif,
                InvoiceSeries = record.InvoiceSeries,
                InvoiceNumber = record.InvoiceNumber,
                IssueDate = "30-08-2026",
                InvoiceType = "F1",
                TotalAmount = record.TotalAmount,
                TotalTaxAmount = record.TotalTaxAmount,
                RegisterTimestamp = record.RegisterTimestamp
            });

        Assert.Equal(expected, record.ComputedHash);
    }

    private static BillingRecord CreateRecord(string registerTimestamp)
    {
        var nif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)
            TaxpayerNif.Create("12345678A")).Value;
        var series = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)
            InvoiceSeries.Create("VF/")).Value;
        var number = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)
            InvoiceNumber.Create("000001")).Value;
        var identifier = ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)
            InvoiceIdentifier.Create(
                nif,
                series,
                number,
                new DateOnly(2026, 8, 30))).Value;
        var total = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(121.23m)).Value;
        var tax = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(21.04m)).Value;

        var record = BillingRecord.Create(
            identifier,
            "EMISOR PRUEBAS",
            "87654321B",
            "DESTINATARIO PRUEBAS",
            "Servicio de prueba",
            total,
            tax,
            previousBillingRecordId: 10,
            previousRecordHash:
                "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            registerTimestamp: registerTimestamp);

        record.SetComputedHash(
            "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB");

        return record;
    }
}
