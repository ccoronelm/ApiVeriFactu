using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.QRCode;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.QRCode;

public sealed class QRCodeGeneratorTests
{
    [Fact]
    public void BuildVerificationUrl_Test_CoincideConEjemploOficial()
    {
        var generator = CreateGenerator(VeriFactuEntorno.Test);
        var data = new VeriFactuQrData
        {
            IssuerNif = "89890001K",
            InvoiceSeries = string.Empty,
            InvoiceNumber = "12345678-G33",
            IssueDate = new DateOnly(2024, 9, 1),
            TotalAmount = 241.4m
        };

        var url = generator.BuildVerificationUrl(data);

        Assert.Equal(
            "https://prewww2.aeat.es/wlpl/TIKE-CONT/ValidarQR" +
            "?nif=89890001K&numserie=12345678-G33" +
            "&fecha=01-09-2024&importe=241.4",
            url);
    }

    [Fact]
    public void BuildVerificationUrl_Production_UsaUrlOficial()
    {
        var generator = CreateGenerator(
            VeriFactuEntorno.Production,
            allowProduction: true);

        var url = generator.BuildVerificationUrl(new VeriFactuQrData
        {
            IssuerNif = "89890001K",
            InvoiceSeries = "A/",
            InvoiceNumber = "0001",
            IssueDate = new DateOnly(2026, 8, 30),
            TotalAmount = 121m
        });

        Assert.StartsWith(
            "https://www2.agenciatributaria.gob.es/wlpl/TIKE-CONT/ValidarQR?",
            url);
        Assert.Contains("numserie=A%2F0001", url);
        Assert.Contains("fecha=30-08-2026", url);
        Assert.Contains("importe=121", url);
    }

    [Fact]
    public void BuildVerificationUrl_ProductionBloqueada_FallaCerrado()
    {
        var generator = CreateGenerator(
            VeriFactuEntorno.Production,
            allowProduction: false);

        Assert.Throws<InvalidOperationException>(() =>
            generator.BuildVerificationUrl(new VeriFactuQrData
            {
                IssuerNif = "89890001K",
                InvoiceSeries = "A/",
                InvoiceNumber = "1",
                IssueDate = new DateOnly(2026, 8, 30),
                TotalAmount = 1m
            }));
    }

    [Fact]
    public void BuildVerificationUrl_AplicaUrlEncodingUtf8()
    {
        var generator = CreateGenerator(VeriFactuEntorno.Test);

        var url = generator.BuildVerificationUrl(new VeriFactuQrData
        {
            IssuerNif = "89890001K",
            InvoiceSeries = "A&",
            InvoiceNumber = "0001",
            IssueDate = new DateOnly(2026, 8, 30),
            TotalAmount = 12.34m
        });

        Assert.Contains("numserie=A%260001", url);
        Assert.DoesNotContain("numserie=A&0001", url);
    }

    [Fact]
    public async Task GeneratePngAsync_DevuelvePngReal()
    {
        var generator = CreateGenerator(VeriFactuEntorno.Test);

        var bytes = await generator.GeneratePngAsync(new VeriFactuQrData
        {
            IssuerNif = "89890001K",
            InvoiceSeries = "A/",
            InvoiceNumber = "0001",
            IssueDate = new DateOnly(2026, 8, 30),
            TotalAmount = 121m
        });

        Assert.True(bytes.Length > 100);
        Assert.Equal(0x89, bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'N', bytes[2]);
        Assert.Equal((byte)'G', bytes[3]);
        Assert.Equal(0x0D, bytes[4]);
        Assert.Equal(0x0A, bytes[5]);
        Assert.Equal(0x1A, bytes[6]);
        Assert.Equal(0x0A, bytes[7]);
    }

    [Fact]
    public void BuildVerificationUrl_RechazaMasDeDosDecimales()
    {
        var generator = CreateGenerator(VeriFactuEntorno.Test);

        Assert.Throws<ArgumentException>(() =>
            generator.BuildVerificationUrl(new VeriFactuQrData
            {
                IssuerNif = "89890001K",
                InvoiceSeries = "A/",
                InvoiceNumber = "0001",
                IssueDate = new DateOnly(2026, 8, 30),
                TotalAmount = 1.234m
            }));
    }

    private static QRCodeGenerator CreateGenerator(
        VeriFactuEntorno environment,
        bool allowProduction = false)
        => new(
            Options.Create(new VeriFactuOptions
            {
                Environment = environment,
                AllowProduction = allowProduction
            }));
}
