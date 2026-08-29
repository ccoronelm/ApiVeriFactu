using System.Xml;
using System.Xml.Linq;
using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.Validation;

/// <summary>
/// Implementación stub de validación XSD para desarrollo.
/// 
/// Realiza validación básica de estructura XML sin XSD real.
/// En producción, cargar esquemas XSD reales desde /VERIFACTU.
/// 
/// Ref: /VERIFACTU - Archivos .xsd
/// </summary>
public sealed class XmlSchemaValidatorStub : IXmlSchemaValidator
{
    private readonly ILogger<XmlSchemaValidatorStub> _logger;

    public XmlSchemaValidatorStub(ILogger<XmlSchemaValidatorStub> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Valida estructura XML básica (no XSD real en stub).
    /// </summary>
    public async Task<XmlValidationResult> ValidateAsync(
        string xmlContent,
        VeriFactuXmlSchemaType schemaType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlContent);

        await Task.Delay(30, cancellationToken); // Simular latencia

        var errors = new List<ValidationError>();
        var warnings = new List<string>();

        try
        {
            // Validación 1: XML bien formado
            var doc = XDocument.Parse(xmlContent);
            _logger.LogDebug("XML bien formado");

            // Validación 2: Raíz esperada según tipo
            var rootName = doc.Root?.Name.LocalName ?? "";
            ValidateRootElement(rootName, schemaType, errors);

            // Validación 3: Elementos requeridos básicos
            ValidateRequiredElements(doc.Root, schemaType, errors);

            // Validación 4: Formato de fechas si existen
            ValidateDateFormats(doc.Root, warnings);

            var isValid = errors.Count == 0;

            _logger.LogInformation(
                "Validación XSD completada. SchemaType: {SchemaType}, IsValid: {IsValid}, Errors: {ErrorCount}",
                schemaType,
                isValid,
                errors.Count);

            return new XmlValidationResult
            {
                IsValid = isValid,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "Error de formato XML");
            return new XmlValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Message = $"Error de formato XML: {ex.Message}",
                        LineNumber = ex.LineNumber,
                        LinePosition = ex.LinePosition
                    }
                },
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado durante validación XSD");
            return new XmlValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new ValidationError
                    {
                        Message = $"Error: {ex.Message}"
                    }
                },
                Warnings = warnings
            };
        }
    }

    /// <summary>
    /// Valida el elemento raíz según tipo de esquema.
    /// </summary>
    private static void ValidateRootElement(
        string rootName,
        VeriFactuXmlSchemaType schemaType,
        List<ValidationError> errors)
    {
        var expectedRoot = schemaType switch
        {
            VeriFactuXmlSchemaType.BillingRecord => "SuministroInformacion",
            VeriFactuXmlSchemaType.CancellationRecord => "SuministroLR",
            VeriFactuXmlSchemaType.QueryRecord => "ConsultaLR",
            VeriFactuXmlSchemaType.SubmissionResponse => "RespuestaSuministro",
            _ => null
        };

        if (expectedRoot != null && rootName != expectedRoot)
        {
            errors.Add(new ValidationError
            {
                Message = $"Elemento raíz incorrecto. Esperado: {expectedRoot}, Encontrado: {rootName}"
            });
        }
    }

    /// <summary>
    /// Valida que elementos requeridos estén presentes.
    /// </summary>
    private static void ValidateRequiredElements(
        XElement? root,
        VeriFactuXmlSchemaType schemaType,
        List<ValidationError> errors)
    {
        if (root == null)
        {
            errors.Add(new ValidationError { Message = "Documento XML vacío" });
            return;
        }

        var requiredElements = schemaType switch
        {
            VeriFactuXmlSchemaType.BillingRecord => new[] { "RegistroFacturacion" },
            VeriFactuXmlSchemaType.CancellationRecord => new[] { "RegistroAnulacion" },
            VeriFactuXmlSchemaType.QueryRecord => new[] { "ConsultaRegistro" },
            VeriFactuXmlSchemaType.SubmissionResponse => new[] { "Resultado" },
            _ => Array.Empty<string>()
        };

        foreach (var elem in requiredElements)
        {
            if (root.Element(elem) == null)
            {
                errors.Add(new ValidationError
                {
                    Message = $"Elemento requerido no encontrado: {elem}"
                });
            }
        }
    }

    /// <summary>
    /// Valida formato de fechas en el XML.
    /// </summary>
    private static void ValidateDateFormats(XElement? root, List<string> warnings)
    {
        if (root == null)
            return;

        var dateElements = root.Descendants()
            .Where(e => e.Name.LocalName.Contains("Fecha") || e.Name.LocalName.Contains("fecha"))
            .ToList();

        foreach (var elem in dateElements)
        {
            var value = elem.Value;
            if (!string.IsNullOrEmpty(value))
            {
                // Esperar formato yyyy-MM-dd o yyyy-MM-ddTHH:mm:ssZ
                if (!System.DateTime.TryParseExact(value,
                    new[] { "yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHH:mm:ss" },
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None,
                    out _))
                {
                    warnings.Add($"Formato de fecha potencialmente incorrecto en {elem.Name}: {value}");
                }
            }
        }
    }
}
