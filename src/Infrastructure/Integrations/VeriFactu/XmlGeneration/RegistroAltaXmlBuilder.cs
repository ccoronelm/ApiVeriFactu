using System.Globalization;
using System.Xml.Linq;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.XmlGeneration;

/// <summary>
/// Constantes de namespaces XML y métodos de formato para el XML VERI*FACTU.
///
/// Ref: /VERIFACTU/SuministroLR.xsd.xml
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml
/// </summary>
internal static class RegistroAltaXmlBuilder
{
    /// <summary>
    /// Namespace raíz de SuministroInformacion (sf).
    /// Ref: SuministroInformacion.xsd.xml — targetNamespace
    /// </summary>
    internal static readonly XNamespace NsSf =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";

    /// <summary>
    /// Namespace de SuministroLR (sfLR).
    /// Ref: SuministroLR.xsd.xml — targetNamespace
    /// </summary>
    internal static readonly XNamespace NsSfLr =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroLR.xsd";

    /// <summary>
    /// Formatea una fecha como DD-MM-AAAA según la especificación AEAT.
    /// Ref: SuministroInformacion.xsd.xml — type FechaType pattern \d{2}-\d{2}-\d{4}
    /// </summary>
    internal static string FormatFechaAeat(DateOnly fecha)
        => fecha.ToString("dd-MM-yyyy", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formatea un importe con 2 decimales usando punto como separador, sin símbolo de moneda.
    /// Ref: SuministroInformacion.xsd.xml — ImporteType / fractionDigits 2
    /// </summary>
    internal static string FormatImporte(decimal importe)
        => importe.ToString("0.00", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formatea un tipo impositivo (porcentaje) con hasta 2 decimales.
    /// Ref: SuministroInformacion.xsd.xml — TipoImpositivo fractionDigits 2
    /// </summary>
    internal static string FormatTipo(decimal tipo)
        => tipo.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>
    /// Construye el NumSerieFactura concatenando serie + número, o solo número si la serie está vacía.
    /// La concatenación debe respetar el valor máximo de 60 caracteres del XSD.
    /// Ref: SuministroInformacion.xsd.xml — NumSerieFacturaType maxLength 60
    /// </summary>
    internal static string BuildNumSerieFactura(string? series, string number)
    {
        if (string.IsNullOrWhiteSpace(series))
            return number ?? string.Empty;

        return $"{series.Trim()}{number?.Trim()}";
    }
}
