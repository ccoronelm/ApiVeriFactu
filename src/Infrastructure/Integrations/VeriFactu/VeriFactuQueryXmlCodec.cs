using System.Globalization;
using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Construcción y parsing de ConsultaFactuSistemaFacturacion según
/// ConsultaLR.xsd y RespuestaConsultaLR.xsd oficiales.
/// </summary>
public static class VeriFactuQueryXmlCodec
{
    public static readonly XNamespace NsQuery =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/ConsultaLR.xsd";

    public static readonly XNamespace NsResponse =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaConsultaLR.xsd";

    private static readonly XNamespace NsSf = RegistroAltaXmlBuilder.NsSf;

    public static string BuildRequest(VeriFactuQueryRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateRequest(request);

        var filter = new XElement(
            NsQuery + "FiltroConsulta",
            new XElement(
                NsQuery + "PeriodoImputacion",
                new XElement(NsSf + "Ejercicio", request.FiscalYear),
                new XElement(NsSf + "Periodo", request.Period)));

        AddOptional(filter, "NumSerieFactura", request.InvoiceNumber);

        if (!string.IsNullOrWhiteSpace(request.CounterpartyNif))
        {
            filter.Add(
                new XElement(
                    NsQuery + "Contraparte",
                    new XElement(NsSf + "NombreRazon", request.CounterpartyName),
                    new XElement(NsSf + "NIF", request.CounterpartyNif)));
        }

        if (request.IssueDate.HasValue)
        {
            filter.Add(
                new XElement(
                    NsQuery + "FechaExpedicionFactura",
                    new XElement(
                        NsSf + "FechaExpedicionFactura",
                        FormatDate(request.IssueDate.Value))));
        }
        else if (request.IssueDateFrom.HasValue || request.IssueDateTo.HasValue)
        {
            filter.Add(
                new XElement(
                    NsQuery + "FechaExpedicionFactura",
                    new XElement(
                        NsSf + "RangoFechaExpedicion",
                        request.IssueDateFrom.HasValue
                            ? new XElement(
                                NsSf + "Desde",
                                FormatDate(request.IssueDateFrom.Value))
                            : null,
                        request.IssueDateTo.HasValue
                            ? new XElement(
                                NsSf + "Hasta",
                                FormatDate(request.IssueDateTo.Value))
                            : null)));
        }

        if (request.System is not null)
        {
            filter.Add(
                new XElement(
                    NsQuery + "SistemaInformatico",
                    new XElement(NsSf + "NombreRazon", request.System.ProducerName),
                    new XElement(NsSf + "NIF", request.System.ProducerNif),
                    string.IsNullOrWhiteSpace(request.System.SystemName)
                        ? null
                        : new XElement(
                            NsSf + "NombreSistemaInformatico",
                            request.System.SystemName),
                    new XElement(
                        NsSf + "IdSistemaInformatico",
                        request.System.SystemId),
                    string.IsNullOrWhiteSpace(request.System.Version)
                        ? null
                        : new XElement(NsSf + "Version", request.System.Version),
                    new XElement(
                        NsSf + "NumeroInstalacion",
                        request.System.InstallationNumber)));
        }

        AddOptional(filter, "RefExterna", request.ExternalReference);

        if (request.PaginationKey is not null)
        {
            filter.Add(
                new XElement(
                    NsQuery + "ClavePaginacion",
                    new XElement(
                        NsSf + "IDEmisorFactura",
                        request.PaginationKey.IssuerNif),
                    new XElement(
                        NsSf + "NumSerieFactura",
                        request.PaginationKey.InvoiceNumber),
                    new XElement(
                        NsSf + "FechaExpedicionFactura",
                        FormatDate(request.PaginationKey.IssueDate))));
        }

        XElement? additional = null;
        if (request.ShowIssuerName || request.ShowSystemInformation)
        {
            additional = new XElement(
                NsQuery + "DatosAdicionalesRespuesta",
                request.ShowIssuerName
                    ? new XElement(NsQuery + "MostrarNombreRazonEmisor", "S")
                    : null,
                request.ShowSystemInformation
                    ? new XElement(NsQuery + "MostrarSistemaInformatico", "S")
                    : null);
        }

        var root = new XElement(
            NsQuery + "ConsultaFactuSistemaFacturacion",
            new XAttribute(XNamespace.Xmlns + "sfLRC", NsQuery.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sf", NsSf.NamespaceName),
            new XElement(
                NsQuery + "Cabecera",
                new XElement(NsSf + "IDVersion", "1.0"),
                new XElement(
                    NsSf + "ObligadoEmision",
                    new XElement(NsSf + "NombreRazon", request.TaxpayerName),
                    new XElement(NsSf + "NIF", request.TaxpayerNif))),
            filter,
            additional);

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root);

        return document.Declaration + document.ToString(SaveOptions.DisableFormatting);
    }

    public static VeriFactuQueryResult ParseResponse(
        XElement response,
        string? rawSoap = null)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.Name != NsResponse + "RespuestaConsultaFactuSistemaFacturacion")
        {
            throw new VeriFactuCommunicationException(
                "La raíz no es RespuestaConsultaFactuSistemaFacturacion.",
                isTransient: false);
        }

        var period = response.Element(NsResponse + "PeriodoImputacion")
            ?? throw ProtocolError("Respuesta de consulta sin PeriodoImputacion.");

        var year = RequiredValue(
            period.Element(NsResponse + "Ejercicio"),
            "PeriodoImputacion/Ejercicio");
        var month = RequiredValue(
            period.Element(NsResponse + "Periodo"),
            "PeriodoImputacion/Periodo");
        var pagination = RequiredValue(
            response.Element(NsResponse + "IndicadorPaginacion"),
            "IndicadorPaginacion");
        var result = RequiredValue(
            response.Element(NsResponse + "ResultadoConsulta"),
            "ResultadoConsulta");

        if (pagination is not ("S" or "N"))
            throw ProtocolError($"IndicadorPaginacion inválido: {pagination}.");

        if (result is not ("ConDatos" or "SinDatos"))
            throw ProtocolError($"ResultadoConsulta inválido: {result}.");

        var records = response
            .Elements(NsResponse + "RegistroRespuestaConsultaFactuSistemaFacturacion")
            .Select(ParseRecord)
            .ToArray();

        if (result == "SinDatos" && records.Length != 0)
            throw ProtocolError("AEAT devolvió ResultadoConsulta=SinDatos con registros.");

        if (result == "ConDatos" && records.Length == 0)
            throw ProtocolError("AEAT devolvió ResultadoConsulta=ConDatos sin registros.");

        var keyElement = response.Element(NsResponse + "ClavePaginacion");
        var nextPage = keyElement is null ? null : ParsePaginationKey(keyElement);

        if (pagination == "S" && nextPage is null)
            throw ProtocolError("IndicadorPaginacion=S requiere ClavePaginacion.");

        if (pagination == "N" && nextPage is not null)
            throw ProtocolError("IndicadorPaginacion=N no debe devolver ClavePaginacion.");

        return new VeriFactuQueryResult
        {
            FiscalYear = year,
            Period = month,
            Result = result,
            HasMorePages = pagination == "S",
            NextPageKey = nextPage,
            Records = records,
            RawResponsePayload = rawSoap
        };
    }

    private static VeriFactuQueryRecord ParseRecord(XElement element)
    {
        var id = element.Element(NsResponse + "IDFactura")
            ?? throw ProtocolError("Registro de consulta sin IDFactura.");

        var data = element.Element(NsResponse + "DatosRegistroFacturacion")
            ?? throw ProtocolError("Registro de consulta sin DatosRegistroFacturacion.");

        var state = element.Element(NsResponse + "EstadoRegistro")
            ?? throw ProtocolError("Registro de consulta sin EstadoRegistro.");

        var issuerNif = RequiredValue(
            id.Element(NsSf + "IDEmisorFactura"),
            "IDFactura/IDEmisorFactura");
        var invoiceNumber = RequiredValue(
            id.Element(NsSf + "NumSerieFactura"),
            "IDFactura/NumSerieFactura");
        var issueDate = ParseAeatDate(
            RequiredValue(
                id.Element(NsSf + "FechaExpedicionFactura"),
                "IDFactura/FechaExpedicionFactura"),
            "IDFactura/FechaExpedicionFactura");

        var recordStatus = RequiredValue(
            state.Element(NsResponse + "EstadoRegistro"),
            "EstadoRegistro/EstadoRegistro");

        return new VeriFactuQueryRecord
        {
            IssuerNif = issuerNif,
            InvoiceNumber = invoiceNumber,
            IssueDate = issueDate,
            IssuerName = OptionalValue(data.Element(NsResponse + "NombreRazonEmisor")),
            InvoiceType = OptionalValue(data.Element(NsResponse + "TipoFactura")),
            RectificationType = OptionalValue(data.Element(NsResponse + "TipoRectificativa")),
            Description = OptionalValue(data.Element(NsResponse + "DescripcionOperacion")),
            TotalTaxAmount = ParseOptionalDecimal(
                data.Element(NsResponse + "CuotaTotal"),
                "CuotaTotal"),
            TotalAmount = ParseOptionalDecimal(
                data.Element(NsResponse + "ImporteTotal"),
                "ImporteTotal"),
            Hash = OptionalValue(data.Element(NsResponse + "Huella")),
            RegisterTimestamp = OptionalValue(
                data.Element(NsResponse + "FechaHoraHusoGenRegistro")),
            RecordStatus = recordStatus,
            ErrorCode = OptionalValue(
                state.Element(NsResponse + "CodigoErrorRegistro")),
            ErrorDescription = OptionalValue(
                state.Element(NsResponse + "DescripcionErrorRegistro")),
            LastModifiedAt = ParseOptionalTimestamp(
                state.Element(NsResponse + "TimestampUltimaModificacion"))
        };
    }

    private static VeriFactuPaginationKey ParsePaginationKey(XElement element)
        => new()
        {
            IssuerNif = RequiredValue(
                element.Element(NsSf + "IDEmisorFactura"),
                "ClavePaginacion/IDEmisorFactura"),
            InvoiceNumber = RequiredValue(
                element.Element(NsSf + "NumSerieFactura"),
                "ClavePaginacion/NumSerieFactura"),
            IssueDate = ParseAeatDate(
                RequiredValue(
                    element.Element(NsSf + "FechaExpedicionFactura"),
                    "ClavePaginacion/FechaExpedicionFactura"),
                "ClavePaginacion/FechaExpedicionFactura")
        };

    private static void ValidateRequest(VeriFactuQueryRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TaxpayerNif) ||
            request.TaxpayerNif.Trim().Length != 9)
        {
            throw new ArgumentException(
                "TaxpayerNif debe tener 9 caracteres.",
                nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.TaxpayerName) ||
            request.TaxpayerName.Trim().Length > 120)
        {
            throw new ArgumentException(
                "TaxpayerName es obligatorio y admite máximo 120 caracteres.",
                nameof(request));
        }

        if (request.FiscalYear.Length != 4 ||
            !int.TryParse(
                request.FiscalYear,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out _))
        {
            throw new ArgumentException(
                "FiscalYear debe tener formato YYYY.",
                nameof(request));
        }

        if (!Enumerable.Range(1, 12)
            .Select(x => x.ToString("00", CultureInfo.InvariantCulture))
            .Contains(request.Period, StringComparer.Ordinal))
        {
            throw new ArgumentException(
                "Period debe estar entre 01 y 12.",
                nameof(request));
        }

        var hasCounterpartyNif = !string.IsNullOrWhiteSpace(request.CounterpartyNif);
        var hasCounterpartyName = !string.IsNullOrWhiteSpace(request.CounterpartyName);
        if (hasCounterpartyNif != hasCounterpartyName)
        {
            throw new ArgumentException(
                "CounterpartyNif y CounterpartyName deben informarse juntos.",
                nameof(request));
        }

        if (hasCounterpartyNif && request.CounterpartyNif!.Trim().Length != 9)
            throw new ArgumentException(
                "CounterpartyNif debe tener 9 caracteres.",
                nameof(request));

        if (request.IssueDate.HasValue &&
            (request.IssueDateFrom.HasValue || request.IssueDateTo.HasValue))
        {
            throw new ArgumentException(
                "IssueDate es incompatible con IssueDateFrom/IssueDateTo.",
                nameof(request));
        }

        if (request.IssueDateFrom.HasValue &&
            request.IssueDateTo.HasValue &&
            request.IssueDateFrom.Value >= request.IssueDateTo.Value)
        {
            throw new ArgumentException(
                "IssueDateFrom debe ser anterior a IssueDateTo.",
                nameof(request));
        }

        if (request.InvoiceNumber?.Length > 60)
            throw new ArgumentException(
                "InvoiceNumber admite máximo 60 caracteres.",
                nameof(request));

        if (request.ExternalReference?.Length > 60)
            throw new ArgumentException(
                "ExternalReference admite máximo 60 caracteres.",
                nameof(request));

        if (request.System is not null)
            ValidateSystem(request.System);
    }

    private static void ValidateSystem(VeriFactuSystemFilter system)
    {
        if (string.IsNullOrWhiteSpace(system.ProducerName) ||
            system.ProducerName.Length > 120 ||
            string.IsNullOrWhiteSpace(system.ProducerNif) ||
            system.ProducerNif.Length != 9 ||
            string.IsNullOrWhiteSpace(system.SystemId) ||
            system.SystemId.Length > 2 ||
            string.IsNullOrWhiteSpace(system.InstallationNumber) ||
            system.InstallationNumber.Length > 100)
        {
            throw new ArgumentException(
                "Filtro SistemaInformatico incompleto o fuera de longitud AEAT.",
                nameof(system));
        }

        if (system.SystemName?.Length > 30 || system.Version?.Length > 50)
            throw new ArgumentException(
                "Filtro SistemaInformatico supera longitudes AEAT.",
                nameof(system));
    }

    private static void AddOptional(
        XElement parent,
        string name,
        string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            parent.Add(new XElement(NsQuery + name, value.Trim()));
    }

    private static string FormatDate(DateOnly date)
        => date.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    private static DateOnly ParseAeatDate(string value, string field)
    {
        if (DateOnly.TryParseExact(
                value,
                "dd-MM-yyyy",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        throw ProtocolError($"{field} contiene fecha AEAT inválida: {value}.");
    }

    private static decimal? ParseOptionalDecimal(XElement? element, string field)
    {
        var value = OptionalValue(element);
        if (value is null)
            return null;

        if (decimal.TryParse(
                value,
                NumberStyles.Number | NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var number))
        {
            return number;
        }

        throw ProtocolError($"{field} contiene un decimal inválido: {value}.");
    }

    private static DateTimeOffset? ParseOptionalTimestamp(XElement? element)
    {
        var value = OptionalValue(element);
        if (value is null)
            return null;

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var timestamp))
        {
            return timestamp;
        }

        throw ProtocolError($"TimestampUltimaModificacion inválido: {value}.");
    }

    private static string RequiredValue(XElement? element, string field)
        => OptionalValue(element)
            ?? throw ProtocolError($"Respuesta de consulta sin {field}.");

    private static string? OptionalValue(XElement? element)
        => string.IsNullOrWhiteSpace(element?.Value)
            ? null
            : element!.Value.Trim();

    private static VeriFactuCommunicationException ProtocolError(string message)
        => new(message, isTransient: false);
}
