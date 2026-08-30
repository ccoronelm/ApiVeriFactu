using System.Xml;
using System.Xml.Schema;
using gesFactu.Application.Common.Abstractions;
using Microsoft.Extensions.Logging;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.Validation;

/// <summary>
/// Validador XSD contra los esquemas oficiales AEAT incluidos en /VERIFACTU.
///
/// Política fail-closed:
/// - Si no se encuentra un XSD requerido, la validación falla.
/// - Si el esquema no está soportado en esta fase, la validación falla.
/// - No se realizan descargas de esquemas durante la validación.
/// </summary>
public sealed class XmlSchemaValidator : IXmlSchemaValidator
{
    private readonly ILogger<XmlSchemaValidator> _logger;
    private readonly string _xsdBasePath;

    private const string NsSf =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroInformacion.xsd";
    private const string NsSfLr =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/SuministroLR.xsd";
    private const string NsRespuesta =
        "https://www2.agenciatributaria.gob.es/static_files/common/internet/dep/aplicaciones/es/aeat/tike/cont/ws/RespuestaSuministro.xsd";
    private const string NsDs = "http://www.w3.org/2000/09/xmldsig#";

    public XmlSchemaValidator(
        ILogger<XmlSchemaValidator> logger,
        string? xsdBasePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _xsdBasePath = xsdBasePath
            ?? Path.Combine(AppContext.BaseDirectory, "VERIFACTU");
    }

    public async Task<XmlValidationResult> ValidateAsync(
        string xmlContent,
        VeriFactuXmlSchemaType schemaType,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(xmlContent);
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();

        var errors = new List<ValidationError>();
        var warnings = new List<string>();

        try
        {
            var schemaSet = BuildSchemaSet(schemaType, errors);

            if (errors.Count > 0)
            {
                _logger.LogError(
                    "No se pudo preparar la validación XSD [{SchemaType}]: {Errors}",
                    schemaType,
                    string.Join("; ", errors.Select(e => e.Message)));

                return new XmlValidationResult
                {
                    IsValid = false,
                    Errors = errors,
                    Warnings = warnings
                };
            }

            var settings = new XmlReaderSettings
            {
                ValidationType = ValidationType.Schema,
                Schemas = schemaSet,
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            settings.ValidationEventHandler += (_, e) =>
            {
                if (e.Severity == XmlSeverityType.Error)
                {
                    errors.Add(new ValidationError
                    {
                        Message = e.Message,
                        LineNumber = e.Exception?.LineNumber,
                        LinePosition = e.Exception?.LinePosition
                    });
                }
                else
                {
                    warnings.Add(e.Message);
                }
            };

            using var reader = XmlReader.Create(new StringReader(xmlContent), settings);
            while (reader.Read())
                cancellationToken.ThrowIfCancellationRequested();

            var isValid = errors.Count == 0;

            _logger.LogInformation(
                "Validación XSD [{SchemaType}]: IsValid={IsValid}, Errores={ErrorCount}, Avisos={WarningCount}",
                schemaType,
                isValid,
                errors.Count,
                warnings.Count);

            return new XmlValidationResult
            {
                IsValid = isValid,
                Errors = errors,
                Warnings = warnings
            };
        }
        catch (XmlException ex)
        {
            return new XmlValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new ValidationError
                    {
                        Message = $"XML mal formado: {ex.Message}",
                        LineNumber = ex.LineNumber,
                        LinePosition = ex.LinePosition
                    }
                ],
                Warnings = warnings
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error inesperado durante validación XSD");
            return new XmlValidationResult
            {
                IsValid = false,
                Errors =
                [
                    new ValidationError
                    {
                        Message = $"Error interno de validación: {ex.Message}"
                    }
                ],
                Warnings = warnings
            };
        }
    }

    private XmlSchemaSet BuildSchemaSet(
        VeriFactuXmlSchemaType schemaType,
        List<ValidationError> loadErrors)
    {
        var schemaSet = new XmlSchemaSet
        {
            // Los XSD oficiales se cargan explícitamente desde disco.
            // No permitimos resolución de recursos externos en runtime.
            XmlResolver = null
        };

        // SuministroInformacion importa XMLDSIG aunque ds:Signature es opcional en VERI*FACTU.
        // Para validar registros sin firma, basta declarar el elemento Signature como anyType
        // y evitar una descarga remota del XSD W3C.
        AddXmlDsigCompatibilitySchema(schemaSet);

        TryAddOfficialSchema(
            schemaSet,
            "SuministroInformacion.xsd.xml",
            NsSf,
            loadErrors);

        switch (schemaType)
        {
            case VeriFactuXmlSchemaType.BillingRecord:
                TryAddOfficialSchema(
                    schemaSet,
                    "SuministroLR.xsd.xml",
                    NsSfLr,
                    loadErrors);
                break;

            case VeriFactuXmlSchemaType.SubmissionResponse:
                TryAddOfficialSchema(
                    schemaSet,
                    "RespuestaSuministro.xsd.xml",
                    NsRespuesta,
                    loadErrors);
                break;

            default:
                loadErrors.Add(new ValidationError
                {
                    Message = $"Validación XSD no implementada para {schemaType}."
                });
                break;
        }

        if (loadErrors.Count == 0)
        {
            try
            {
                schemaSet.Compile();
            }
            catch (Exception ex)
            {
                loadErrors.Add(new ValidationError
                {
                    Message = $"Error compilando esquemas XSD: {ex.Message}"
                });
            }
        }

        return schemaSet;
    }

    private void TryAddOfficialSchema(
        XmlSchemaSet schemaSet,
        string xsdFileName,
        string targetNamespace,
        List<ValidationError> loadErrors)
    {
        // La ruta base es deliberadamente única. Los XSD oficiales se copian
        // al output/publish; si faltan allí, la validación debe fallar cerrada.
        var candidates = new[]
        {
            Path.GetFullPath(Path.Combine(_xsdBasePath, xsdFileName))
        };

        var found = candidates.FirstOrDefault(File.Exists);
        if (found is null)
        {
            loadErrors.Add(new ValidationError
            {
                Message =
                    $"XSD oficial requerido no encontrado: {xsdFileName}. " +
                    $"Rutas comprobadas: {string.Join(" | ", candidates)}"
            });
            return;
        }

        try
        {
            var readerSettings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null
            };

            using var reader = XmlReader.Create(found, readerSettings);
            schemaSet.Add(targetNamespace, reader);
        }
        catch (Exception ex)
        {
            loadErrors.Add(new ValidationError
            {
                Message = $"Error cargando {xsdFileName}: {ex.Message}"
            });
        }
    }

    private static void AddXmlDsigCompatibilitySchema(XmlSchemaSet schemaSet)
    {
        const string schema = """
            <xs:schema xmlns:xs="http://www.w3.org/2001/XMLSchema"
                       targetNamespace="http://www.w3.org/2000/09/xmldsig#"
                       xmlns:ds="http://www.w3.org/2000/09/xmldsig#"
                       elementFormDefault="qualified">
              <xs:element name="Signature" type="xs:anyType" />
            </xs:schema>
            """;

        using var reader = XmlReader.Create(new StringReader(schema));
        schemaSet.Add(NsDs, reader);
    }
}
