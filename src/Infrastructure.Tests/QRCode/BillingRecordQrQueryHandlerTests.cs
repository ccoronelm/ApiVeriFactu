using gesFactu.Application.Common;
using gesFactu.Application.RegistrosFacturacion.Queries.ObtenerQr;
using gesFactu.Domain.Entities;
using gesFactu.Domain.ValueObjects;
using gesFactu.Infrastructure.Integrations.QRCode;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Persistence;
using gesFactu.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.QRCode;

public sealed class BillingRecordQrQueryHandlerTests
{
    [Fact]
    public async Task Handle_Alta_DevuelveUrlYPng()
    {
        await using var context = CreateContext();
        var repository = new BillingRecordRepository(context);
        var record = CreateRecord();

        await repository.AddAsync(record);
        await context.SaveChangesAsync();

        var handler = new GetBillingRecordQrQueryHandler(
            repository,
            new QRCodeGenerator(
                Options.Create(new VeriFactuOptions
                {
                    Environment = VeriFactuEntorno.Test
                })));

        var result = await handler.Handle(
            new GetBillingRecordQrQuery(record.Id),
            CancellationToken.None);

        var success =
            Assert.IsType<Result<BillingRecordQrDto>.SuccessWithValue>(result);

        Assert.Contains("nif=12345678A", success.Value.VerificationUrl);
        Assert.Contains("numserie=A%2F001", success.Value.VerificationUrl);
        Assert.True(success.Value.PngBytes.Length > 100);
    }

    [Fact]
    public async Task Handle_RegistroAnulacion_RechazaQr()
    {
        await using var context = CreateContext();
        var repository = new BillingRecordRepository(context);
        var record = CreateRecord();
        record.RecordType = BillingRecord.CancellationRecordType;

        await repository.AddAsync(record);
        await context.SaveChangesAsync();

        var handler = new GetBillingRecordQrQueryHandler(
            repository,
            new QRCodeGenerator(
                Options.Create(new VeriFactuOptions
                {
                    Environment = VeriFactuEntorno.Test
                })));

        var result = await handler.Handle(
            new GetBillingRecordQrQuery(record.Id),
            CancellationToken.None);

        var error = Assert.IsType<Result<BillingRecordQrDto>.DomainError>(result);
        Assert.Equal("QR_NOT_APPLICABLE", error.Code);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase("qr-" + Guid.NewGuid().ToString("N"))
            .Options;

        return new ApplicationDbContext(options);
    }

    private static BillingRecord CreateRecord()
    {
        var nif = ((ValueObjectResult<TaxpayerNif>.SuccessWithValue)
            TaxpayerNif.Create("12345678A")).Value;
        var series = ((ValueObjectResult<InvoiceSeries>.SuccessWithValue)
            InvoiceSeries.Create("A/")).Value;
        var number = ((ValueObjectResult<InvoiceNumber>.SuccessWithValue)
            InvoiceNumber.Create("001")).Value;
        var identifier = ((ValueObjectResult<InvoiceIdentifier>.SuccessWithValue)
            InvoiceIdentifier.Create(
                nif,
                series,
                number,
                new DateOnly(2026, 8, 30))).Value;

        var total = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(121m)).Value;
        var tax = ((ValueObjectResult<Money>.SuccessWithValue)
            Money.Create(21m)).Value;

        var record = BillingRecord.Create(
            identifier,
            "Emisor pruebas",
            "B98588544",
            "INICIATIVAS EN ANALISIS CLINICOS SL",
            "Factura prueba QR",
            total,
            tax);

        record.SetComputedHash(
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA");

        return record;
    }
}
