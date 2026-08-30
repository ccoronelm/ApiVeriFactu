namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Entorno AEAT al que apunta la integración.
/// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml — service locations
/// </summary>
public enum VeriFactuEntorno
{
    /// <summary>
    /// Entorno de pruebas AEAT.
    /// Endpoint: https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP
    /// </summary>
    Test,

    /// <summary>
    /// Entorno de producción AEAT.
    /// Endpoint: https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP
    /// </summary>
    Production
}

/// <summary>
/// Datos del obligado tributario que emite las facturas.
/// Es la persona física/jurídica sujeta a la obligación de facturación.
/// Distinto del productor del sistema informático.
/// Ref: /VERIFACTU/SuministroInformacion.xsd - CabeceraType/ObligadoEmision
/// </summary>
public sealed class ObligadoTributarioOptions
{
    /// <summary>
    /// NIF del obligado tributario (9 caracteres, formato AEAT).
    /// Obtener desde User Secrets en Development: VeriFactu:Taxpayer:Nif
    /// </summary>
    public string Nif { get; set; } = string.Empty;

    /// <summary>
    /// Nombre o razón social del obligado tributario (máx 120 chars).
    /// Obtener desde User Secrets en Development: VeriFactu:Taxpayer:Name
    /// </summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Opciones de certificado X.509 para autenticación mTLS con AEAT.
/// NOTA: La autenticación es mTLS (certificado en capa HTTPS).
/// VERI*FACTU en modalidad sistema verificable NO requiere firma XAdES del XML.
/// El campo ds:Signature en el XSD es opcional (minOccurs=0).
/// Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml
/// </summary>
public sealed class CertificateOptions
{
    /// <summary>
    /// Thumbprint del certificado en el almacén Windows (CurrentUser/My).
    /// Obtener desde User Secrets: VeriFactu:Certificate:Thumbprint
    /// Preferible para Development sobre PFX en disco.
    /// </summary>
    public string? Thumbprint { get; set; }

    /// <summary>
    /// Ruta del archivo PFX/P12 (alternativa al almacén Windows).
    /// </summary>
    public string? PfxPath { get; set; }

    /// <summary>
    /// Contraseña del PFX. Usar User Secrets, nunca hardcodear.
    /// </summary>
    public string? PfxPassword { get; set; }
}

/// <summary>
/// Datos del sistema informático (productor del software).
/// Es la entidad que desarrolla/mantiene gesFactu. Distinta del obligado tributario.
/// Ref: /VERIFACTU/SuministroInformacion.xsd - SistemaInformaticoType
/// Todos los campos son obligatorios según el XSD.
/// </summary>
public sealed class SistemaInformaticoOptions
{
    /// <summary>
    /// Nombre o razón social del productor del sistema informático (máx 120 chars). Obligatorio.
    /// </summary>
    public string NombreRazon { get; set; } = string.Empty;

    /// <summary>
    /// NIF del productor del sistema informático (9 chars). Obligatorio si se usa NIF.
    /// </summary>
    public string Nif { get; set; } = string.Empty;

    /// <summary>
    /// Nombre comercial del sistema informático (máx 30 chars). Obligatorio.
    /// </summary>
    public string NombreSistemaInformatico { get; set; } = "gesFactu";

    /// <summary>
    /// Identificador del sistema informático (máx 2 chars). Obligatorio.
    /// </summary>
    public string IdSistemaInformatico { get; set; } = string.Empty;

    /// <summary>
    /// Versión del sistema informático (máx 50 chars). Obligatorio.
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Número de instalación (máx 100 chars). Obligatorio.
    /// </summary>
    public string NumeroInstalacion { get; set; } = string.Empty;

    /// <summary>
    /// S si el sistema solo puede emitir registros VERI*FACTU, N en caso contrario.
    /// Ref: /VERIFACTU/SuministroInformacion.xsd - SiNoType. Obligatorio.
    /// </summary>
    public string TipoUsoPosibleSoloVerifactu { get; set; } = "S";

    /// <summary>
    /// S si el sistema puede ser usado por múltiples obligados tributarios. Obligatorio.
    /// </summary>
    public string TipoUsoPosibleMultiOT { get; set; } = "N";

    /// <summary>
    /// S si actualmente el sistema es usado por múltiples obligados tributarios. Obligatorio.
    /// </summary>
    public string IndicadorMultiplesOT { get; set; } = "N";
}

/// <summary>
/// Configuración raíz para la integración AEAT VERI*FACTU.
/// Se carga desde appsettings.json bajo la sección "VeriFactu".
/// Los secretos (NIF, Thumbprint, PfxPassword) se obtienen de User Secrets en Development.
/// </summary>
public sealed class VeriFactuOptions
{
    public const string SectionName = "VeriFactu";

    /// <summary>
    /// Entorno AEAT: Test o Production.
    /// En entorno ASP.NET Core Development solo se permite Test (validación fail-fast al startup).
    /// </summary>
    public VeriFactuEntorno Environment { get; set; } = VeriFactuEntorno.Test;

    /// <summary>
    /// Cerrojo explícito para impedir apuntar accidentalmente a producción.
    /// Debe permanecer false salvo activación deliberada en un despliegue productivo.
    /// </summary>
    public bool AllowProduction { get; set; } = false;

    /// <summary>
    /// Modo de cliente: "Stub" (tests unitarios), "SoapClient" (integración real con AEAT).
    /// </summary>
    public string ClientMode { get; set; } = "Stub";

    /// <summary>
    /// Timeout en segundos para llamadas SOAP a AEAT.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Número máximo de reintentos para errores transitorios.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Milisegundos de espera inicial entre reintentos (backoff exponencial).
    /// </summary>
    public int RetryDelayMs { get; set; } = 1000;

    /// <summary>
    /// Datos del obligado tributario que emite las facturas.
    /// </summary>
    public ObligadoTributarioOptions Taxpayer { get; set; } = new();

    /// <summary>
    /// Configuración del certificado X.509 para autenticación mTLS.
    /// </summary>
    public CertificateOptions Certificate { get; set; } = new();

    /// <summary>
    /// Datos del sistema informático (productor del software).
    /// </summary>
    public SistemaInformaticoOptions SistemaInformatico { get; set; } = new();

    // Endpoints oficiales AEAT
    // Ref: /VERIFACTU/SistemaFacturacion.wsdl.xml
    // port SistemaVerifactuPruebas  -> https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP
    // port SistemaVerifactu         -> https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP

    public const string EndpointTest = "https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";
    public const string EndpointProduction = "https://www1.agenciatributaria.gob.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP";

    /// <summary>
    /// Devuelve el endpoint correcto según el entorno configurado.
    /// </summary>
    public string GetEndpoint() => Environment == VeriFactuEntorno.Production
        ? EndpointProduction
        : EndpointTest;
}

