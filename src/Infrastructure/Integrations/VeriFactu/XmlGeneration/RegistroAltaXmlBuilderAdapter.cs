using gesFactu.Application.Common.Abstractions;
using gesFactu.Application.Common.Abstractions;
using gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;

/// <summary>
/// Implementación del puerto IRegistroAltaXmlBuilder.
/// Traduce RegistroAltaData (Application) al XML conforme a SuministroLR.xsd.
/// </summary>
public sealed class RegistroAltaXmlBuilderAdapter : IRegistroAltaXmlBuilder
{
    private readonly IOptions<VeriFactuOptions> _options;

    public RegistroAltaXmlBuilderAdapter(IOptions<VeriFactuOptions> options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public string BuildRegFactuXml(RegistroAltaData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var opts = _options.Value;

        // Construir Cabecera (ObligadoEmision)
        var ns = RegistroAltaXmlBuilder.NsSf;
        var nsLr = RegistroAltaXmlBuilder.NsSfLr;
        var nsSoap = XNamespace.None; // no usado aquí, solo el XML del registro

        var taxpayer = opts.Taxpayer;
        var si = opts.SistemaInformatico;

        // Encadenamiento
        XElement encadenamiento;
        if (string.IsNullOrWhiteSpace(data.PreviousRecordHash))
        {
            encadenamiento = new XElement(ns + "Encadenamiento",
                new XElement(ns + "PrimerRegistro", "S"));
        }
        else
        {
            encadenamiento = new XElement(ns + "Encadenamiento",
                new XElement(ns + "RegistroAnterior",
                    new XElement(ns + "IDEmisorFactura", data.PreviousIssuerNif ?? data.IssuerNif),
                    new XElement(ns + "NumSerieFactura",
                        RegistroAltaXmlBuilder.BuildNumSerieFactura(
                            data.PreviousInvoiceSeries ?? string.Empty,
                            data.PreviousInvoiceNumber ?? string.Empty)),
                    new XElement(ns + "FechaExpedicionFactura",
                        RegistroAltaXmlBuilder.FormatFechaAeat(
                            data.PreviousIssueDate ?? data.IssueDate)),
                    new XElement(ns + "Huella", data.PreviousRecordHash)));
        }

        // SistemaInformatico
        var sistemaInformatico = new XElement(ns + "SistemaInformatico",
            new XElement(ns + "NombreRazon", si.NombreRazon),
            new XElement(ns + "NIF", si.Nif),
            new XElement(ns + "NombreSistemaInformatico", si.NombreSistemaInformatico),
            new XElement(ns + "IdSistemaInformatico", si.IdSistemaInformatico),
            new XElement(ns + "Version", si.Version),
            new XElement(ns + "NumeroInstalacion", si.NumeroInstalacion),
            new XElement(ns + "TipoUsoPosibleSoloVerifactu", si.TipoUsoPosibleSoloVerifactu),
            new XElement(ns + "TipoUsoPosibleMultiOT", si.TipoUsoPosibleMultiOT),
            new XElement(ns + "IndicadorMultiplesOT", si.IndicadorMultiplesOT));

        // Desglose
        var desgloseElement = new XElement(ns + "Desglose");
        foreach (var d in data.Detalles)
        {
            var detalle = new XElement(ns + "DetalleDesglose");
            if (!string.IsNullOrWhiteSpace(d.Impuesto))
                detalle.Add(new XElement(ns + "Impuesto", d.Impuesto));
            if (!string.IsNullOrWhiteSpace(d.ClaveRegimen))
                detalle.Add(new XElement(ns + "ClaveRegimen", d.ClaveRegimen));
            if (!string.IsNullOrWhiteSpace(d.OperacionExenta))
                detalle.Add(new XElement(ns + "OperacionExenta", d.OperacionExenta));
            else
                detalle.Add(new XElement(ns + "CalificacionOperacion", d.CalificacionOperacion));
            if (d.TipoImpositivo.HasValue)
                detalle.Add(new XElement(ns + "TipoImpositivo",
                    RegistroAltaXmlBuilder.FormatTipo(d.TipoImpositivo.Value)));
            detalle.Add(new XElement(ns + "BaseImponibleOimporteNoSujeto",
                RegistroAltaXmlBuilder.FormatImporte(d.BaseImponible)));
            if (d.CuotaRepercutida.HasValue)
                detalle.Add(new XElement(ns + "CuotaRepercutida",
                    RegistroAltaXmlBuilder.FormatImporte(d.CuotaRepercutida.Value)));
            desgloseElement.Add(detalle);
        }

        // RegistroAlta
        var registroAlta = new XElement(ns + "RegistroAlta",
            new XElement(ns + "IDVersion", "1.0"),
            new XElement(ns + "IDFactura",
                new XElement(ns + "IDEmisorFactura", data.IssuerNif),
                new XElement(ns + "NumSerieFactura",
                    RegistroAltaXmlBuilder.BuildNumSerieFactura(data.InvoiceSeries, data.InvoiceNumber)),
                new XElement(ns + "FechaExpedicionFactura",
                    RegistroAltaXmlBuilder.FormatFechaAeat(data.IssueDate))),
            new XElement(ns + "NombreRazonEmisor", data.IssuerName),
            new XElement(ns + "TipoFactura", data.TipoFactura),
            new XElement(ns + "DescripcionOperacion", data.Description),
            desgloseElement,
            new XElement(ns + "CuotaTotal", RegistroAltaXmlBuilder.FormatImporte(data.CuotaTotal)),
            new XElement(ns + "ImporteTotal", RegistroAltaXmlBuilder.FormatImporte(data.ImporteTotal)),
            encadenamiento,
            sistemaInformatico,
            new XElement(ns + "FechaHoraHusoGenRegistro", data.FechaHoraHusoGenRegistro),
            new XElement(ns + "TipoHuella", "01"),
            new XElement(ns + "Huella", data.ComputedHash));

        // Cabecera
        var cabecera = new XElement(ns + "Cabecera",
            new XElement(ns + "ObligadoEmision",
                new XElement(ns + "NombreRazon", taxpayer.Name),
                new XElement(ns + "NIF", taxpayer.Nif)));

        // RegFactuSistemaFacturacion (raíz)
        var regFactu = new XElement(nsLr + "RegFactuSistemaFacturacion",
            new XAttribute(XNamespace.Xmlns + "sfLR", nsLr.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sf", ns.NamespaceName),
            cabecera,
            new XElement(nsLr + "RegistroFactura",
                registroAlta));

        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), regFactu);
        return doc.Declaration?.ToString() + doc.ToString();
    }
}
