using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Options;
using System.Xml.Linq;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;

/// <summary>
/// Implementación del puerto IRegistroAltaXmlBuilder.
/// Traduce RegistroAltaData al XML document/literal definido por los XSD oficiales AEAT.
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
        ValidateConfiguration(opts, data);

        var ns = RegistroAltaXmlBuilder.NsSf;
        var nsLr = RegistroAltaXmlBuilder.NsSfLr;
        var taxpayer = opts.Taxpayer;
        var si = opts.SistemaInformatico;

        XElement encadenamiento;
        if (string.IsNullOrWhiteSpace(data.PreviousRecordHash))
        {
            encadenamiento = new XElement(
                ns + "Encadenamiento",
                new XElement(ns + "PrimerRegistro", "S"));
        }
        else
        {
            // Si hay huella anterior, su identidad completa es obligatoria.
            if (string.IsNullOrWhiteSpace(data.PreviousIssuerNif) ||
                string.IsNullOrWhiteSpace(data.PreviousInvoiceNumber) ||
                !data.PreviousIssueDate.HasValue)
            {
                throw new InvalidOperationException(
                    "RegistroAnterior requiere NIF, número/serie de factura, fecha y huella del RF anterior.");
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
            {
                detalle.Add(new XElement(
                    ns + "TipoImpositivo",
                    RegistroAltaXmlBuilder.FormatTipo(d.TipoImpositivo.Value)));
            }

            detalle.Add(new XElement(
                ns + "BaseImponibleOimporteNoSujeto",
                RegistroAltaXmlBuilder.FormatImporte(d.BaseImponible)));

            if (d.CuotaRepercutida.HasValue)
            {
                detalle.Add(new XElement(
                    ns + "CuotaRepercutida",
                    RegistroAltaXmlBuilder.FormatImporte(d.CuotaRepercutida.Value)));
            }

            desgloseElement.Add(detalle);
        }

        var registroAlta = new XElement(
            ns + "RegistroAlta",
            new XElement(ns + "IDVersion", "1.0"),
            new XElement(
                ns + "IDFactura",
                new XElement(ns + "IDEmisorFactura", taxpayer.Nif),
                new XElement(
                    ns + "NumSerieFactura",
                    RegistroAltaXmlBuilder.BuildNumSerieFactura(
                        data.InvoiceSeries,
                        data.InvoiceNumber)),
                new XElement(
                    ns + "FechaExpedicionFactura",
                    RegistroAltaXmlBuilder.FormatFechaAeat(data.IssueDate))),
            new XElement(ns + "NombreRazonEmisor", taxpayer.Name),
            data.IsSubsanacion
                ? new XElement(ns + "Subsanacion", "S")
                : null,
            new XElement(ns + "TipoFactura", data.TipoFactura),
            new XElement(ns + "DescripcionOperacion", data.Description),
            string.IsNullOrWhiteSpace(data.RecipientNif)
                ? null
                : new XElement(
                    ns + "Destinatarios",
                    new XElement(
                        ns + "IDDestinatario",
                        new XElement(ns + "NombreRazon", data.RecipientName),
                        new XElement(ns + "NIF", data.RecipientNif))),
            desgloseElement,
            new XElement(ns + "CuotaTotal", RegistroAltaXmlBuilder.FormatImporte(data.CuotaTotal)),
            new XElement(ns + "ImporteTotal", RegistroAltaXmlBuilder.FormatImporte(data.ImporteTotal)),
            encadenamiento,
            sistemaInformatico,
            new XElement(ns + "FechaHoraHusoGenRegistro", data.FechaHoraHusoGenRegistro),
            new XElement(ns + "TipoHuella", "01"),
            new XElement(ns + "Huella", data.ComputedHash));

        // Cabecera es elemento local de RegFactuSistemaFacturacion y, por
        // elementFormDefault="qualified" de SuministroLR.xsd, pertenece a sfLR.
        // Su contenido está tipado por sf:CabeceraType.
        var cabecera = new XElement(
            nsLr + "Cabecera",
            new XElement(
                ns + "ObligadoEmision",
                new XElement(ns + "NombreRazon", taxpayer.Name),
                new XElement(ns + "NIF", taxpayer.Nif)));

        var regFactu = new XElement(
            nsLr + "RegFactuSistemaFacturacion",
            new XAttribute(XNamespace.Xmlns + "sfLR", nsLr.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "sf", ns.NamespaceName),
            cabecera,
            new XElement(nsLr + "RegistroFactura", registroAlta));

        var doc = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            regFactu);

        return doc.Declaration?.ToString() + doc.ToString();
    }

    private static void ValidateConfiguration(
        VeriFactuOptions options,
        RegistroAltaData data)
    {
        var taxpayer = options.Taxpayer;
        var si = options.SistemaInformatico;

        if (string.IsNullOrWhiteSpace(taxpayer.Nif) ||
            string.IsNullOrWhiteSpace(taxpayer.Name))
        {
            throw new InvalidOperationException(
                "Falta configurar VeriFactu:Taxpayer:Nif y VeriFactu:Taxpayer:Name.");
        }

        if (!string.Equals(
                taxpayer.Nif.Trim(),
                data.IssuerNif.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El NIF del registro no coincide con el obligado tributario configurado.");
        }

        if (!string.Equals(
                taxpayer.Name.Trim(),
                data.IssuerName.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El nombre o razón social del registro no coincide con el obligado tributario configurado.");
        }

        var requiredSistemaFields = new Dictionary<string, string?>
        {
            ["NombreRazon"] = si.NombreRazon,
            ["Nif"] = si.Nif,
            ["NombreSistemaInformatico"] = si.NombreSistemaInformatico,
            ["IdSistemaInformatico"] = si.IdSistemaInformatico,
            ["Version"] = si.Version,
            ["NumeroInstalacion"] = si.NumeroInstalacion,
            ["TipoUsoPosibleSoloVerifactu"] = si.TipoUsoPosibleSoloVerifactu,
            ["TipoUsoPosibleMultiOT"] = si.TipoUsoPosibleMultiOT,
            ["IndicadorMultiplesOT"] = si.IndicadorMultiplesOT
        };

        var missing = requiredSistemaFields
            .Where(x => string.IsNullOrWhiteSpace(x.Value))
            .Select(x => x.Key)
            .ToArray();

        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Faltan datos de VeriFactu:SistemaInformatico: {string.Join(", ", missing)}.");
        }

        if (data.TipoFactura is not ("F1" or "F2"))
        {
            throw new InvalidOperationException(
                "RegistroAlta solo soporta F1/F2 en esta fase.");
        }

        var hasRecipientNif = !string.IsNullOrWhiteSpace(data.RecipientNif);
        var hasRecipientName = !string.IsNullOrWhiteSpace(data.RecipientName);

        if (data.TipoFactura == "F1" && (!hasRecipientNif || !hasRecipientName))
        {
            throw new InvalidOperationException(
                "RegistroAlta F1 requiere el bloque Destinatarios.");
        }

        if (hasRecipientNif != hasRecipientName)
        {
            throw new InvalidOperationException(
                "NIF y nombre del destinatario deben informarse juntos.");
        }

        if (hasRecipientNif)
        {
            if (data.RecipientNif.Trim().Length != 9)
            {
                throw new InvalidOperationException(
                    "El NIF del destinatario debe tener exactamente 9 caracteres.");
            }

            if (string.Equals(
                    taxpayer.Nif.Trim(),
                    data.RecipientNif.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "El NIF del destinatario debe ser distinto del NIF del obligado emisor.");
            }
        }

        if (data.Detalles.Count == 0)
            throw new InvalidOperationException("RegistroAlta requiere al menos un DetalleDesglose.");

        if (string.IsNullOrWhiteSpace(data.ComputedHash))
            throw new InvalidOperationException("RegistroAlta requiere la huella calculada.");
    }
}
