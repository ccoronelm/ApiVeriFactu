using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Options;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;

/// <summary>
/// Construye RegistroAnulacion dentro de RegFactuSistemaFacturacion.
/// Ref: SuministroInformacion.xsd / RegistroFacturacionAnulacionType.
/// </summary>
public sealed class RegistroAnulacionXmlBuilderAdapter : IRegistroAnulacionXmlBuilder
{
    private readonly IOptions<VeriFactuOptions> _options;

    public RegistroAnulacionXmlBuilderAdapter(IOptions<VeriFactuOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string BuildRegFactuXml(RegistroAnulacionData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var options = _options.Value;
        ValidateConfiguration(options, data);

        var ns = RegistroAltaXmlBuilder.NsSf;
        var nsLr = RegistroAltaXmlBuilder.NsSfLr;

        XElement encadenamiento;
        if (string.IsNullOrWhiteSpace(data.PreviousRecordHash))
        {
            encadenamiento = new XElement(
                ns + "Encadenamiento",
                new XElement(ns + "PrimerRegistro", "S"));
        }
        else
        {
            if (string.IsNullOrWhiteSpace(data.PreviousIssuerNif) ||
                string.IsNullOrWhiteSpace(data.PreviousInvoiceNumber) ||
                !data.PreviousIssueDate.HasValue)
            {
                throw new InvalidOperationException(
                    "RegistroAnterior requiere identidad completa y huella del RF anterior.");
            }

            encadenamiento = new XElement(
                ns + "Encadenamiento",
                new XElement(
                    ns + "RegistroAnterior",
                    new XElement(ns + "IDEmisorFactura", data.PreviousIssuerNif),
                    new XElement(
                        ns + "NumSerieFactura",
                        RegistroAltaXmlBuilder.BuildNumSerieFactura(
                            data.PreviousInvoiceSeries,
                            data.PreviousInvoiceNumber)),
                    new XElement(
                        ns + "FechaExpedicionFactura",
                        RegistroAltaXmlBuilder.FormatFechaAeat(
                            data.PreviousIssueDate.Value)),
                    new XElement(ns + "Huella", data.PreviousRecordHash)));
        }

        var si = options.SistemaInformatico;
        var sistemaInformatico = new XElement(
            ns + "SistemaInformatico",
            new XElement(ns + "NombreRazon", si.NombreRazon),
            new XElement(ns + "NIF", si.Nif),
            new XElement(ns + "NombreSistemaInformatico", si.NombreSistemaInformatico),
            new XElement(ns + "IdSistemaInformatico", si.IdSistemaInformatico),
            new XElement(ns + "Version", si.Version),
            new XElement(ns + "NumeroInstalacion", si.NumeroInstalacion),
            new XElement(ns + "TipoUsoPosibleSoloVerifactu", si.TipoUsoPosibleSoloVerifactu),
            new XElement(ns + "TipoUsoPosibleMultiOT", si.TipoUsoPosibleMultiOT),
            new XElement(ns + "IndicadorMultiplesOT", si.IndicadorMultiplesOT));

        var registroAnulacion = new XElement(
            ns + "RegistroAnulacion",
            new XElement(ns + "IDVersion", "1.0"),
            new XElement(
                ns + "IDFactura",
                new XElement(ns + "IDEmisorFacturaAnulada", data.IssuerNif),
                new XElement(
                    ns + "NumSerieFacturaAnulada",
                    RegistroAltaXmlBuilder.BuildNumSerieFactura(
                        data.InvoiceSeries,
                        data.InvoiceNumber)),
                new XElement(
                    ns + "FechaExpedicionFacturaAnulada",
                    RegistroAltaXmlBuilder.FormatFechaAeat(data.IssueDate))),
            string.IsNullOrWhiteSpace(data.SinRegistroPrevio)
                ? null
                : new XElement(ns + "SinRegistroPrevio", data.SinRegistroPrevio),
            string.IsNullOrWhiteSpace(data.RechazoPrevio)
                ? null
                : new XElement(ns + "RechazoPrevio", data.RechazoPrevio),
            encadenamiento,
            sistemaInformatico,
            new XElement(ns + "FechaHoraHusoGenRegistro", data.FechaHoraHusoGenRegistro),
            new XElement(ns + "TipoHuella", "01"),
            new XElement(ns + "Huella", data.ComputedHash));

        var cabecera = new XElement(
            nsLr + "Cabecera",
            new XElement(
                ns + "ObligadoEmision",
                new XElement(ns + "NombreRazon", options.Taxpayer.Name),
                new XElement(ns + "NIF", options.Taxpayer.Nif)));

        var root = new XElement(
            nsLr + "RegFactuSistemaFacturacion",
            new XAttribute(XNamespace.Xmlns + "sfLR", nsLr.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sf", ns.NamespaceName),
            cabecera,
            new XElement(nsLr + "RegistroFactura", registroAnulacion));

        var document = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            root);

        return document.Declaration?.ToString() + document.ToString();
    }

    private static void ValidateConfiguration(
        VeriFactuOptions options,
        RegistroAnulacionData data)
    {
        if (string.IsNullOrWhiteSpace(options.Taxpayer.Nif) ||
            string.IsNullOrWhiteSpace(options.Taxpayer.Name))
        {
            throw new InvalidOperationException(
                "Falta configurar VeriFactu:Taxpayer:Nif/Name.");
        }

        if (!string.Equals(
                options.Taxpayer.Nif.Trim(),
                data.IssuerNif.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El emisor de la anulación no coincide con el obligado tributario configurado.");
        }

        if (string.IsNullOrWhiteSpace(data.ComputedHash))
            throw new InvalidOperationException("RegistroAnulacion requiere Huella.");

        if (string.IsNullOrWhiteSpace(data.FechaHoraHusoGenRegistro))
            throw new InvalidOperationException(
                "RegistroAnulacion requiere FechaHoraHusoGenRegistro.");

        var si = options.SistemaInformatico;
        if (string.IsNullOrWhiteSpace(si.NombreRazon) ||
            string.IsNullOrWhiteSpace(si.Nif) ||
            string.IsNullOrWhiteSpace(si.NombreSistemaInformatico) ||
            string.IsNullOrWhiteSpace(si.IdSistemaInformatico) ||
            string.IsNullOrWhiteSpace(si.Version) ||
            string.IsNullOrWhiteSpace(si.NumeroInstalacion))
        {
            throw new InvalidOperationException(
                "Configuración incompleta de SistemaInformatico.");
        }
    }
}
