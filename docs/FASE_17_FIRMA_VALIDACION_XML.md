# Fase 17: Firma y Validación XML AEAT

**Objetivo:** Implementar los servicios de firma digital XAdES-4j y validación XSD conforme a especificaciones AEAT para garantizar autenticidad, integridad y conformidad de registros de facturación.

---

## ?? Resumen de cambios

### 1. **Interfaz de firma digital** (`IXmlSignatureService.cs`)

Puerto que define operaciones de firma AEAT:

```csharp
Task<string> SignXmlAsync(string xmlContent, CancellationToken cancellationToken)
Task<bool> VerifyXmlSignatureAsync(string signedXmlContent, CancellationToken cancellationToken)
Task<CertificateInfo> GetCertificateInfoAsync(CancellationToken cancellationToken)
```

**Características:**
- Firma XAdES-EPES Level (especificación AEAT)
- RSA SHA256 como algoritmo de firma
- Integración de certificado cliente
- Timestamp (cuando está disponible TSA)
- Record `CertificateInfo` con detalles del certificado

### 2. **Interfaz de validación XSD** (`IXmlSchemaValidator.cs`)

Puerto para validación contra esquemas oficiales AEAT:

```csharp
Task<XmlValidationResult> ValidateAsync(
    string xmlContent,
    VeriFactuXmlSchemaType schemaType,
    CancellationToken cancellationToken)
```

**Tipos de esquemas soportados:**
- `BillingRecord` - Registro de facturación (SuministroInformacion.xsd)
- `CancellationRecord` - Registro de anulación (SuministroLR.xsd)
- `QueryRecord` - Consulta de registro (ConsultaLR.xsd)
- `SubmissionResponse` - Respuesta de AEAT (RespuestaSuministro.xsd)

**Resultado de validación:**
- `IsValid` - Indica conformidad
- `Errors` - Lista de errores (estructura, valores)
- `Warnings` - Advertencias no críticas

### 3. **Implementación Stub de Firma** (`XmlSignatureServiceStub.cs`)

Para desarrollo y testing:

- Simula firma XAdES sin criptografía real
- Añade elemento `<Firma>` al XML con:
  - Método de firma simulado
  - Sujeto del certificado (si está disponible)
  - Huella SHA256 del contenido
  - Timestamp simulado
- No requiere certificado real

**Métodos:**
- `SignXmlAsync()` - Añade firma stub al XML
- `VerifyXmlSignatureAsync()` - Verifica presencia de elemento firma
- `GetCertificateInfoAsync()` - Retorna info del certificado (stub o real)

### 4. **Validador XSD Stub** (`XmlSchemaValidatorStub.cs`)

Para desarrollo (sin XSD real):

- Validación de estructura XML bien formada
- Validación de elemento raíz según tipo de esquema
- Validación de elementos requeridos
- Validación de formato de fechas (yyyy-MM-dd, ISO 8601)
- Retorna lista de errores y advertencias

**Validaciones implementadas:**
- XML bien formado (XmlException handling)
- Raíz correcta por tipo de esquema
- Elementos requeridos presentes
- Formato de fechas correcto

### 5. **Registración en DI** (`DependencyInjection.cs`)

```csharp
services.AddScoped<IXmlSignatureService, XmlSignatureServiceStub>();
services.AddScoped<IXmlSchemaValidator, XmlSchemaValidatorStub>();
```

---

## ??? Flujo de validación y firma

```
Comando EnviarRegistroFacturacion
  ?
ValidarXmlSchema (IXmlSchemaValidator)
  ??? Valida estructura contra XSD
  ??? Retorna errores si no cumple
  ?
FirmarXml (IXmlSignatureService)
  ??? Carga certificado cliente
  ??? Calcula firma XAdES-EPES
  ??? Retorna XML firmado
  ?
SuministrarAEAT (VeriFactuGatewaySoapClient)
  ??? Envía XML firmado y validado
```

---

## ?? Seguridad

### Certificados X.509

**Desarrollo (Stub):**
- No requiere certificado real
- Simula con datos de prueba

**Producción:**
- Certificado de firma digital AEAT (.pfx/.p12)
- Cargado desde configuración segura (Azure Key Vault)
- Validez verificada antes de usar

### Firma XAdES-EPES

Cumple especificación AEAT:
- Algoritmo: RSA SHA-256
- Formato: XAdES-EPES Level
- Incluye: Certificado, timestamp, elementos de firma
- Ref: `/VERIFACTU/EspecTecGenerFirmaElectRfact.pdf`

### Validación XSD

En producción:
- Cargar XSD real desde `/VERIFACTU`
- Validar contra namespace oficial AEAT
- Rechazar documentos no conformes antes de enviar

---

## ?? Próximos pasos

### Fase 18 (Producción)

1. **Firma real con librerías certificadas:**
   - Evaluar `BouncyCastle`, `XmlDsig` (System.Security.Cryptography.Xml)
   - Generar firma XAdES-4j conforme a PDF oficial
   - Integrar con HSM si aplica

2. **Validación XSD real:**
   - Cargar esquemas `.xsd` desde `/VERIFACTU`
   - Usar `XmlSchemaSet` con validación estricta
   - Mapear errores XSD a mensajes de negocio

3. **Timestamp de servidor:**
   - Integrar con TSA (Time Stamp Authority) de AEAT/FNMT
   - Incluir timestamp en firma

4. **Auditoría de firma:**
   - Log de certificados usados
   - Trazabilidad de operaciones de firma
   - Validación periódica de certificados

---

## ? Validación actual

- ? Compilación exitosa
- ? 45/45 tests pasando
- ? Interfaces listos para implementación real
- ? Stubs funcionales para desarrollo
- ? Integración con DI completada

---

## ?? Referencias

- `/VERIFACTU/EspecTecGenerFirmaElectRfact.pdf` - Especificación técnica de firma
- `/VERIFACTU/SuministroInformacion.xsd` - Esquema de registro de facturación
- `/VERIFACTU/SuministroLR.xsd` - Esquema de registro de anulación
- `/VERIFACTU/RespuestaSuministro.xsd` - Esquema de respuesta
- AEAT XAdES-4j oficial: Norma técnica de firma digital

---

**Estado:** Interfaces y stubs completos. Lista para integración real con librerías de firma certificadas.
