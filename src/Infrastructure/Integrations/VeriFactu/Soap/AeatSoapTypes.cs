namespace gesFactu.Infrastructure.Integrations.VeriFactu.Soap;

/// <summary>
/// Tipos SOAP generados/mapeados desde WSDL de AEAT.
/// 
/// Estos tipos representan las estructuras SOAP que recibe/devuelve AEAT.
/// Cuando se use una herramienta de generación WSDL real (ej: dotnet-svcutil),
/// estos tipos pueden ser reemplazados o completados automáticamente.
/// 
/// Por ahora, definimos la estructura que necesitamos para la integración.
/// 
/// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml
/// Ref: /VERIFACTU/SuministroInformacion.xsd.xml
/// Ref: /VERIFACTU/RespuestaSuministro.xsd.xml
/// </summary>

/// <summary>
/// Solicitud SOAP para enviar un registro de facturación.
/// Mapea a: RegFactuSistemaFacturacion
/// </summary>
public sealed class RegFactuSistemaFacturacionRequest
{
    /// <summary>
    /// ID de tercero (contribuyente) que envía el registro.
    /// </summary>
    public string? IdTercero { get; set; }

    /// <summary>
    /// Clave de acceso para autenticación en AEAT (si aplica).
    /// </summary>
    public string? ClaveAcceso { get; set; }

    /// <summary>
    /// Contenido XML del registro de facturación ya firmado.
    /// Este es el bloque principal de datos VERI*FACTU.
    /// </summary>
    public string? RegistroFacturacionXml { get; set; }

    /// <summary>
    /// Nombre del archivo de adjunto (opcional, para documentación del envío).
    /// </summary>
    public string? NombreArchivo { get; set; }
}

/// <summary>
/// Respuesta SOAP de AEAT tras envío de registro.
/// </summary>
public sealed class RegFactuSistemaFacturacionResponse
{
    /// <summary>
    /// Identificador único del envío asignado por AEAT.
    /// </summary>
    public string? IdEnvio { get; set; }

    /// <summary>
    /// Información de resultado / estado de la solicitud.
    /// </summary>
    public InformacionResultado? Resultado { get; set; }
}

/// <summary>
/// Contenedor de información de resultado de AEAT.
/// </summary>
public sealed class InformacionResultado
{
    /// <summary>
    /// Código de estado de la respuesta (numérico).
    /// Ejemplos: "0" (aceptado), "1" (en proceso), "2" (rechazado).
    /// </summary>
    public string? CodigoEstado { get; set; }

    /// <summary>
    /// Descripción del estado.
    /// </summary>
    public string? DescripcionEstado { get; set; }

    /// <summary>
    /// URL del CSV (AEAT reference number) si aplica.
    /// </summary>
    public string? Csv { get; set; }

    /// <summary>
    /// Timestamp del servidor de AEAT.
    /// </summary>
    public DateTime? FechaHora { get; set; }

    /// <summary>
    /// Colección de incidencias / validaciones (si las hay).
    /// </summary>
    public List<Incidencia>? Incidencias { get; set; }
}

/// <summary>
/// Incidencia o validación retornada por AEAT.
/// </summary>
public sealed class Incidencia
{
    /// <summary>
    /// Código de error / validación (ej: "F-0001", "R-0001").
    /// </summary>
    public string? Codigo { get; set; }

    /// <summary>
    /// Descripción del error / validación.
    /// </summary>
    public string? Descripcion { get; set; }

    /// <summary>
    /// Tipo de incidencia: "Aviso", "Error", "Informativo".
    /// </summary>
    public string? Tipo { get; set; }
}

/// <summary>
/// Solicitud SOAP para consultar el estado de un registro.
/// Mapea a: ConsultaFactuSistemaFacturacion
/// </summary>
public sealed class ConsultaFactuSistemaFacturacionRequest
{
    /// <summary>
    /// Identificador del envío a consultar (retornado por RegFactuSistemaFacturacionResponse.IdEnvio).
    /// </summary>
    public string? IdEnvio { get; set; }

    /// <summary>
    /// ID de tercero (contribuyente).
    /// </summary>
    public string? IdTercero { get; set; }

    /// <summary>
    /// Clave de acceso para autenticación.
    /// </summary>
    public string? ClaveAcceso { get; set; }
}

/// <summary>
/// Respuesta SOAP de consulta de estado.
/// </summary>
public sealed class ConsultaFactuSistemaFacturacionResponse
{
    /// <summary>
    /// Identificador del envío consultado.
    /// </summary>
    public string? IdEnvio { get; set; }

    /// <summary>
    /// Estado actual del registro.
    /// Posibles valores según AEAT: "Aceptado", "Rechazado", "En proceso", etc.
    /// </summary>
    public string? EstadoRegistro { get; set; }

    /// <summary>
    /// Información de resultado.
    /// </summary>
    public InformacionResultado? Resultado { get; set; }
}

/// <summary>
/// Solicitud SOAP para cancelar un registro (requerimiento).
/// Mapea a: RegFactuSistemaFacturacion (con parámetros de cancelación)
/// </summary>
public sealed class CancelacionRegistroRequest
{
    /// <summary>
    /// ID del envío a cancelar.
    /// </summary>
    public string? IdEnvio { get; set; }

    /// <summary>
    /// ID de tercero (contribuyente).
    /// </summary>
    public string? IdTercero { get; set; }

    /// <summary>
    /// Clave de acceso.
    /// </summary>
    public string? ClaveAcceso { get; set; }

    /// <summary>
    /// Motivo de la cancelación (ej: "Anulación", "Rectificación").
    /// </summary>
    public string? MotivoCancelacion { get; set; }

    /// <summary>
    /// Contenido XML del registro de cancelación (si aplica).
    /// </summary>
    public string? RegistroCancelacionXml { get; set; }
}

/// <summary>
/// Respuesta SOAP de cancelación.
/// </summary>
public sealed class CancelacionRegistroResponse
{
    /// <summary>
    /// ID del nuevo envío de cancelación.
    /// </summary>
    public string? IdEnvio { get; set; }

    /// <summary>
    /// Información de resultado.
    /// </summary>
    public InformacionResultado? Resultado { get; set; }
}
