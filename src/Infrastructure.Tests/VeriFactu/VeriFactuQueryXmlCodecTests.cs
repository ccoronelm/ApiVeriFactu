using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class VeriFactuQueryXmlCodecTests
{
    private const string Nif = "89890001K";

    [Fact]
    public async Task BuildRequest_ConFiltrosYPaginacion_ValidaConsultaLrOficial()
    {
        var request = new VeriFactuQueryRequest
        {
            TaxpayerNif = Nif,
            TaxpayerName = "EMISOR PRUEBAS",
            FiscalYear = "2026",
            Period = "08",
            InvoiceNumber = "A/0001",
            CounterpartyNif = "B98588544",
            CounterpartyName = "INICIATIVAS EN ANALISIS CLINICOS SL",
            IssueDateFrom = new DateOnly(2026, 8, 1),
            IssueDateTo = new DateOnly(2026, 8, 31),
            ExternalReference = "EXT-001",
            System = new VeriFactuSystemFilter
            {
                ProducerName = "PRODUCTOR PRUEBAS",
                ProducerNif = "89890001K",
                SystemName = "gesFactu",
                SystemId = "GF",
                Version = "1.0",
                InstallationNumber = "TEST-01"
            },
            PaginationKey = new VeriFactuPaginationKey
            {
                IssuerNif = Nif,
                InvoiceNumber = "A/0000",
                IssueDate = new DateOnly(2026, 8, 1)
            },
            ShowIssuerName = true,
            ShowSystemInformation = true
        };

        var xml = VeriFactuQueryXmlCodec.BuildRequest(request);

        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        var validation = await validator.ValidateAsync(
            xml,
            VeriFactuXmlSchemaType.QueryRecord);

        Assert.True(
            validation.IsValid,
            string.Join(" | ", validation.Errors.Select(x => x.Message)));

        var document = XDocument.Parse(xml);
        var nsQuery = VeriFactuQueryXmlCodec.NsQuery;
        var nsSf = (XNamespace)
            "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

        Assert.Equal(
            "2026",
            document.Descendants(nsSf + "Ejercicio").Single().Value);
        Assert.Equal(
            "08",
            document.Descendants(nsSf + "Periodo").Single().Value);
        Assert.Equal(
            "A/0001",
            document.Descendants(nsQuery + "NumSerieFactura").Single().Value);
        Assert.Single(document.Descendants(nsQuery + "ClavePaginacion"));
        Assert.Equal(
            "S",
            document.Descendants(nsQuery + "MostrarSistemaInformatico").Single().Value);
    }

    [Fact]
    public async Task ResponseFixture_ValidaRespuestaConsultaLrOficial()
    {
        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            Path.Combine(AppContext.BaseDirectory, "VERIFACTU"));

        var validation = await validator.ValidateAsync(
            ResponseWithPagination(),
            VeriFactuXmlSchemaType.QueryResponse);

        Assert.True(
            validation.IsValid,
            string.Join(" | ", validation.Errors.Select(x => x.Message)));
    }

    [Fact]
    public void ParseResponse_ConPaginacion_MapeaRegistrosYClaveSiguiente()
    {
        var response = XElement.Parse(ResponseWithPagination());

        var result = VeriFactuQueryXmlCodec.ParseResponse(
            response,
            "<soap>raw</soap>");

        Assert.Equal("2026", result.FiscalYear);
        Assert.Equal("08", result.Period);
        Assert.Equal("ConDatos", result.Result);
        Assert.True(result.HasMorePages);
        Assert.NotNull(result.NextPageKey);
        Assert.Single(result.Records);

        var record = result.Records.Single();
        Assert.Equal(Nif, record.IssuerNif);
        Assert.Equal("A/0001", record.InvoiceNumber);
        Assert.Equal(new DateOnly(2026, 8, 30), record.IssueDate);
        Assert.Equal("F1", record.InvoiceType);
        Assert.Equal(121m, record.TotalAmount);
        Assert.Equal(21m, record.TotalTaxAmount);
        Assert.Equal("Correcto", record.RecordStatus);
        Assert.Equal(
            "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            record.Hash);

        Assert.Equal("A/0001", result.NextPageKey!.InvoiceNumber);
        Assert.Equal("<soap>raw</soap>", result.RawResponsePayload);
    }

    [Fact]
    public void ParseResponse_IndicadorS_SinClavePaginacion_FallaCerrado()
    {
        var root = XElement.Parse(ResponseWithPagination());
        root.Element(
            VeriFactuQueryXmlCodec.NsResponse + "ClavePaginacion")!
            .Remove();

        var ex = Assert.Throws<VeriFactuCommunicationException>(
            () => VeriFactuQueryXmlCodec.ParseResponse(root));

        Assert.Contains("ClavePaginacion", ex.Message);
        Assert.False(ex.IsTransient);
    }

    private static string ResponseWithPagination()
        => $$"""
        <sfLRRC:RespuestaConsultaFactuSistemaFacturacion
          xmlns:sfLRRC="{{VeriFactuQueryXmlCodec.NsResponse}}"
          xmlns:sf="https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd">
          <sfLRRC:Cabecera>
            <sf:IDVersion>1.0</sf:IDVersion>
            <sf:ObligadoEmision>
              <sf:NombreRazon>EMISOR PRUEBAS</sf:NombreRazon>
              <sf:NIF>{{Nif}}</sf:NIF>
            </sf:ObligadoEmision>
          </sfLRRC:Cabecera>
          <sfLRRC:PeriodoImputacion>
            <sfLRRC:Ejercicio>2026</sfLRRC:Ejercicio>
            <sfLRRC:Periodo>08</sfLRRC:Periodo>
          </sfLRRC:PeriodoImputacion>
          <sfLRRC:IndicadorPaginacion>S</sfLRRC:IndicadorPaginacion>
          <sfLRRC:ResultadoConsulta>ConDatos</sfLRRC:ResultadoConsulta>
          <sfLRRC:RegistroRespuestaConsultaFactuSistemaFacturacion>
            <sfLRRC:IDFactura>
              <sf:IDEmisorFactura>{{Nif}}</sf:IDEmisorFactura>
              <sf:NumSerieFactura>A/0001</sf:NumSerieFactura>
              <sf:FechaExpedicionFactura>30-08-2026</sf:FechaExpedicionFactura>
            </sfLRRC:IDFactura>
            <sfLRRC:DatosRegistroFacturacion>
              <sfLRRC:NombreRazonEmisor>EMISOR PRUEBAS</sfLRRC:NombreRazonEmisor>
              <sfLRRC:TipoFactura>F1</sfLRRC:TipoFactura>
              <sfLRRC:DescripcionOperacion>Factura prueba</sfLRRC:DescripcionOperacion>
              <sfLRRC:CuotaTotal>21.00</sfLRRC:CuotaTotal>
              <sfLRRC:ImporteTotal>121.00</sfLRRC:ImporteTotal>
              <sfLRRC:FechaHoraHusoGenRegistro>2026-08-30T10:00:00+02:00</sfLRRC:FechaHoraHusoGenRegistro>
              <sfLRRC:TipoHuella>01</sfLRRC:TipoHuella>
              <sfLRRC:Huella>AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA</sfLRRC:Huella>
            </sfLRRC:DatosRegistroFacturacion>
            <sfLRRC:EstadoRegistro>
              <sfLRRC:TimestampUltimaModificacion>2026-08-30T10:01:00+02:00</sfLRRC:TimestampUltimaModificacion>
              <sfLRRC:EstadoRegistro>Correcto</sfLRRC:EstadoRegistro>
            </sfLRRC:EstadoRegistro>
          </sfLRRC:RegistroRespuestaConsultaFactuSistemaFacturacion>
          <sfLRRC:ClavePaginacion>
            <sf:IDEmisorFactura>{{Nif}}</sf:IDEmisorFactura>
            <sf:NumSerieFactura>A/0001</sf:NumSerieFactura>
            <sf:FechaExpedicionFactura>30-08-2026</sf:FechaExpedicionFactura>
          </sfLRRC:ClavePaginacion>
        </sfLRRC:RespuestaConsultaFactuSistemaFacturacion>
        """;
}
