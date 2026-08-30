using System.Net;
using System.Text;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu;
using gesFactu.Infrastructure.Integrations.VeriFactu.Validation;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace gesFactu.Infrastructure.Tests.VeriFactu;

public sealed class VeriFactuGatewaySoapClientTests
{
    private const string Nif = "89890001K";

    [Fact]
    public async Task SubmitBillingRecordAsync_ParsesAcceptedResponseAndRealCsv()
    {
        var gateway = CreateGateway(AcceptedSoapResponse());

        var result = await gateway.SubmitBillingRecordAsync(CreateRequest());

        Assert.True(result.IsAccepted);
        Assert.Equal(AeatResponseCode.Success, result.ResponseCode);
        Assert.Equal("CSV-PRUEBA-123", result.SubmissionId);
        Assert.Equal("Correcto", result.StatusCode);
        Assert.Equal("Correcto", result.RecordStatus);
        Assert.Null(result.ErrorCode);
        Assert.False(result.IsDuplicate);
        Assert.NotNull(result.ServerTimestamp);
        Assert.Contains("RespuestaRegFactuSistemaFacturacion", result.RawResponsePayload);
    }

    [Fact]
    public async Task SubmitBillingRecordAsync_ParsesDuplicateWithoutInventingCsv()
    {
        var gateway = CreateGateway(DuplicateSoapResponse());

        var result = await gateway.SubmitBillingRecordAsync(CreateRequest());

        Assert.False(result.IsAccepted);
        Assert.Equal(AeatResponseCode.DuplicateError, result.ResponseCode);
        Assert.Null(result.SubmissionId);
        Assert.Equal("3000", result.ErrorCode);
        Assert.True(result.IsDuplicate);
        Assert.Equal("Correcta", result.DuplicateRecordStatus);
    }

    [Fact]
    public async Task SubmitBillingRecordAsync_ThrowsDifferentiatedSoapFault()
    {
        var gateway = CreateGateway(
            SoapFaultResponse(),
            HttpStatusCode.InternalServerError);

        var ex = await Assert.ThrowsAsync<VeriFactuSoapFaultException>(
            () => gateway.SubmitBillingRecordAsync(CreateRequest()));

        Assert.Contains("SOAP Fault", ex.Message);
        Assert.NotNull(ex.RawResponsePayload);
    }

    private static VeriFactuGatewaySoapClient CreateGateway(
        string responseBody,
        HttpStatusCode statusCode = HttpStatusCode.OK)
    {
        var httpClient = new HttpClient(
            new FixedResponseHandler(responseBody, statusCode));

        var xsdPath = Path.Combine(AppContext.BaseDirectory, "VERIFACTU");
        var validator = new XmlSchemaValidator(
            NullLogger<XmlSchemaValidator>.Instance,
            xsdPath);

        var options = Options.Create(new VeriFactuOptions
        {
            Environment = VeriFactuEntorno.Test
        });

        return new VeriFactuGatewaySoapClient(
            httpClient,
            options,
            validator,
            NullLogger<VeriFactuGatewaySoapClient>.Instance);
    }

    private static VeriFactuSubmissionRequest CreateRequest()
        => new()
        {
            TaxpayerNif = Nif,
            SignedXmlContent =
                $$"""
                <sfLR:RegFactuSistemaFacturacion
                    xmlns:sfLR="{{RegistroAltaXmlBuilder.NsSfLr.NamespaceName}}"
                    xmlns:sf="{{RegistroAltaXmlBuilder.NsSf.NamespaceName}}">
                </sfLR:RegFactuSistemaFacturacion>
                """
        };

    private static string AcceptedSoapResponse()
        => $$"""
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
            xmlns:sfR="https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd"
            xmlns:sf="https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd">
          <soapenv:Body>
            <sfR:RespuestaRegFactuSistemaFacturacion>
              <sfR:CSV>CSV-PRUEBA-123</sfR:CSV>
              <sfR:DatosPresentacion>
                <sf:NIFPresentador>{{Nif}}</sf:NIFPresentador>
                <sf:TimestampPresentacion>2026-08-30T08:00:00+02:00</sf:TimestampPresentacion>
              </sfR:DatosPresentacion>
              <sfR:Cabecera>
                <sf:ObligadoEmision>
                  <sf:NombreRazon>EMISOR PRUEBAS</sf:NombreRazon>
                  <sf:NIF>{{Nif}}</sf:NIF>
                </sf:ObligadoEmision>
              </sfR:Cabecera>
              <sfR:TiempoEsperaEnvio>60</sfR:TiempoEsperaEnvio>
              <sfR:EstadoEnvio>Correcto</sfR:EstadoEnvio>
              <sfR:RespuestaLinea>
                <sfR:IDFactura>
                  <sf:IDEmisorFactura>{{Nif}}</sf:IDEmisorFactura>
                  <sf:NumSerieFactura>A/0001</sf:NumSerieFactura>
                  <sf:FechaExpedicionFactura>30-08-2026</sf:FechaExpedicionFactura>
                </sfR:IDFactura>
                <sfR:Operacion>
                  <sf:TipoOperacion>Alta</sf:TipoOperacion>
                </sfR:Operacion>
                <sfR:EstadoRegistro>Correcto</sfR:EstadoRegistro>
              </sfR:RespuestaLinea>
            </sfR:RespuestaRegFactuSistemaFacturacion>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private static string DuplicateSoapResponse()
        => $$"""
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/"
            xmlns:sfR="https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd"
            xmlns:sf="https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd">
          <soapenv:Body>
            <sfR:RespuestaRegFactuSistemaFacturacion>
              <sfR:Cabecera>
                <sf:ObligadoEmision>
                  <sf:NombreRazon>EMISOR PRUEBAS</sf:NombreRazon>
                  <sf:NIF>{{Nif}}</sf:NIF>
                </sf:ObligadoEmision>
              </sfR:Cabecera>
              <sfR:TiempoEsperaEnvio>60</sfR:TiempoEsperaEnvio>
              <sfR:EstadoEnvio>Incorrecto</sfR:EstadoEnvio>
              <sfR:RespuestaLinea>
                <sfR:IDFactura>
                  <sf:IDEmisorFactura>{{Nif}}</sf:IDEmisorFactura>
                  <sf:NumSerieFactura>A/0001</sf:NumSerieFactura>
                  <sf:FechaExpedicionFactura>30-08-2026</sf:FechaExpedicionFactura>
                </sfR:IDFactura>
                <sfR:Operacion>
                  <sf:TipoOperacion>Alta</sf:TipoOperacion>
                </sfR:Operacion>
                <sfR:EstadoRegistro>Incorrecto</sfR:EstadoRegistro>
                <sfR:CodigoErrorRegistro>3000</sfR:CodigoErrorRegistro>
                <sfR:DescripcionErrorRegistro>Registro de facturación duplicado.</sfR:DescripcionErrorRegistro>
                <sfR:RegistroDuplicado>
                  <sf:IdPeticionRegistroDuplicado>PETICION-123</sf:IdPeticionRegistroDuplicado>
                  <sf:EstadoRegistroDuplicado>Correcta</sf:EstadoRegistroDuplicado>
                </sfR:RegistroDuplicado>
              </sfR:RespuestaLinea>
            </sfR:RespuestaRegFactuSistemaFacturacion>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private static string SoapFaultResponse()
        => """
        <soapenv:Envelope xmlns:soapenv="http://schemas.xmlsoap.org/soap/envelope/">
          <soapenv:Body>
            <soapenv:Fault>
              <faultcode>soapenv:Client</faultcode>
              <faultstring>Solicitud inválida</faultstring>
            </soapenv:Fault>
          </soapenv:Body>
        </soapenv:Envelope>
        """;

    private sealed class FixedResponseHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _statusCode;

        public FixedResponseHandler(
            string body,
            HttpStatusCode statusCode)
        {
            _body = body;
            _statusCode = statusCode;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(
                    _body,
                    Encoding.UTF8,
                    "text/xml")
            };

            return Task.FromResult(response);
        }
    }
}
