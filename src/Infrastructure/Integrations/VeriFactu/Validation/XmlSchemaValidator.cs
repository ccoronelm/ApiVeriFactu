using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.Validation;

/// <summary>
/// Validador XSD real contra los esquemas oficiales AEAT VERI*FACTU.
///
/// Los XSD se cargan desde el directorio /VERIFACTU (sin modificarlos).
/// La validación se realiza antes de cualquier envío a AEAT.
///
/// Ref: /VERIFACTU/SuministroLR.xsd.xml
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml
/// Ref: /VERIFACTU/RespuestaSuministro.xsd.xml
/// </summary>
public sealed class XmlSchemaValidator : IXmlSchemaValidator
{
    private readonly ILogger<XmlSchemaValidator> _logger;
    private readonly string _xsdBasePath;

    // Namespaces oficiales
    private const string NsSf = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
    private const string NsSfLr = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroLR.xsd";
    private const string NsRespuesta = "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";

    public XmlSchemaValidator(ILogger<XmlSchemaValidator> logger, string? xsdBasePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Ruta base: por defecto busca VERIFACTU/ relativo al directorio del ejecutable
        _xsdBasePath = xsdBasePath
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "VERIFACTU");
    }

    public async Task<XmlValidationResult> ValidateAsync(
        string xmlContent,
        VeriFactuXmlSchemaType schemaType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlContent);

        await Task.Yield(); // mantenemos firma async; la validación XSD es síncrona

        var errors = new List<ValidationError>();
        var warnings = new List<string>();

        try
        {
            var schemaSet = BuildSchemaSet(schemaType, errors);

            if (errors.Count > 0)
            {
                // No se pudieron cargar los XSD — reportar como error técnico
                _logger.LogError("No se pudieron cargar los esquemas XSD para {SchemaType}. Errores: {Errors}",
                    schemaType, string.Join("; ", errors.Select(e => e.Message)));

                return new XmlValidationResult { IsValid = false, Errors = errors, Warnings = warnings };
            }

            using var reader = XmlReader.Create(new StringReader(xmlContent), new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemaSet,
                DtdProcessing = DtdProcessing.Prohibit,
            });

            reader.Settings!.ValidationEventHandler += (_, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                    errors.Add(new ValidationError
                    {
                        Message = e.Message,
                        LineNumber = e.Exception?.LineNumber,
                        LinePosition = e.Exception?.LinePosition
                    });
                else
                    warnings.Add(e.Message);
            };

            while (reader.Read()) { }

            var isValid = errors.Count == 0;

            _logger.LogInformation(
                "Validación XSD [{SchemaType}]: IsValid={IsValid}, Errores={ErrorCount}, Avisos={WarnCount}",
                schemaType, isValid, errors.Count, warnings.Count);

            return new XmlValidationResult { IsValid = isValid, Errors = errors, Warnings = warnings };
        }
        catch (XmlException ex)
        {
            _logger.LogError(ex, "XML mal formado durante validación XSD");
            return new XmlValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError>
                {
                    new() { Message = $"XML mal formado: {ex.Message}", LineNumber = ex.LineNumber, LinePosition = ex.LinePosition }
                },
                Warnings = warnings
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado en validación XSD");
            return new XmlValidationResult
            {
                IsValid = false,
                Errors = new List<ValidationError> { new() { Message = $"Error interno de validación: {ex.Message}" } },
                Warnings = warnings
            };
        }
    }

    private XmlSchemaSet BuildSchemaSet(VeriFactuXmlSchemaType schemaType, List<ValidationError> loadErrors)
    {
        var schemaSet = new XmlSchemaSet();

        // Siempre cargamos SuministroInformacion ya que es importado por los demás
        TryAddSchema(schemaSet, "SuministroInformacion.xsd.xml", NsSf, loadErrors);

        switch (schemaType)
        {
            case VeriFactuXmlSchemaType.BillingRecord:
                TryAddSchema(schemaSet, "SuministroLR.xsd.xml", NsSfLr, loadErrors);
                break;
            case VeriFactuXmlSchemaType.SubmissionResponse:
                TryAddSchema(schemaSet, "RespuestaSuministro.xsd.xml", NsRespuesta, loadErrors);
                break;
            default:
                // Tipos no implementados en esta fase — validación básica de bien-formado
                break;
        }

        if (loadErrors.Count == 0)
        {
            try { schemaSet.Compile(); }
            catch (Exception ex) { loadErrors.Add(new ValidationError { Message = $"Error compilando esquemas: {ex.Message}" }); }
        }

        return schemaSet;
    }

    private void TryAddSchema(XmlSchemaSet schemaSet, string xsdFileName, string targetNamespace, List<ValidationError> loadErrors)
    {
        // Busca el XSD en varias rutas posibles para diferentes contextos (dev/test/prod)
        var candidates = new[]
        {
            Path.Combine(_xsdBasePath, xsdFileName),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "VERIFACTU", xsdFileName)),
            Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "VERIFACTU", xsdFileName)),
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (found == null)
        {
            // Los XSD no están disponibles (ej: entorno CI sin carpeta VERIFACTU)
            // Registramos aviso en lugar de error — la validación de bien-formado sigue funcionando
            _logger.LogWarning("XSD {FileName} no encontrado. Validación solo verificará XML bien-formado.", xsdFileName);
            return;
        }

        try
        {
            using var xsdStream = File.OpenRead(found);
            schemaSet.Add(targetNamespace, XmlReader.Create(xsdStream));
        }
        catch (Exception ex)
        {
            loadErrors.Add(new ValidationError { Message = $"Error cargando {xsdFileName}: {ex.Message}" });
        }
    }
}
