using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Integrations.VeriFactu.Mappers;

/// <summary>
/// Clasificación de códigos de respuesta AEAT para decisiones de retry.
/// Ref: /VERIFACTU/errores.properties.txt
/// </summary>
public static class AeatResponseCodeClassifier
{
    public static AeatResponseCode Classify(string? codigoEstado)
    {
        if (string.IsNullOrWhiteSpace(codigoEstado))
            return AeatResponseCode.Unknown;

        return codigoEstado switch
        {
            "Correcto" or "ParcialmenteCorrecto" => AeatResponseCode.Success,
            _ when codigoEstado.StartsWith("4") => AeatResponseCode.BusinessRejection,
            _ when codigoEstado.StartsWith("3") => AeatResponseCode.TemporaryError,
            _ => AeatResponseCode.Unknown
        };
    }
}
