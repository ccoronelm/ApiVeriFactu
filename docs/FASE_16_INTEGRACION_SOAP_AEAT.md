# Fase 16: Integración SOAP/WSDL Real AEAT

**Objetivo:** Crear la estructura y componentes necesarios para la integración real con AEAT VERI*FACTU mediante SOAP/WSDL, reemplazando el stub con un cliente SOAP funcional.

---

## ?? Resumen de cambios

### 1. **Tipos SOAP generados** (`AeatSoapTypes.cs`)

Definición de estructuras SOAP que representan el contrato con AEAT:

- `RegFactuSistemaFacturacionRequest` / `RegFactuSistemaFacturacionResponse` - Envío de registros
- `ConsultaFactuSistemaFacturacionRequest` / `ConsultaFactuSistemaFacturacionResponse` - Consulta de estado
- `CancelacionRegistroRequest` / `CancelacionRegistroResponse` - Cancelación de registros
- `InformacionResultado` / `Incidencia` - Detalles de respuesta AEAT

**Nota:** Estos tipos permanecen en Infrastructure y nunca se exponen a Application/Domain, respetando la Anti-Corruption Layer.

### 2. **Anti-Corruption Layer Mapper** (`AeatSoapMapper.cs`)

Clase estática que mapea bidireccionalmenteal entre:
- Tipos de negocio (`VeriFactuSubmissionRequest`, `VeriFactuSubmissionResult`, etc.)
- Tipos SOAP (`RegFactuSistemaFacturacionRequest`, etc.)

Incluye lógica de clasificación de códigos AEAT:
- **Success** (0, 1000): Aceptación
- **ValidationError**: Errores de validación XML
- **TemporaryError**: Errores transientes (timeout, 503)
- **DuplicateError**: Registros duplicados
- **AuthenticationError**: Problemas de certificado
- **BusinessRejection**: Rechazos por reglas AEAT

### 3. **Cliente SOAP Real** (`VeriFactuGatewaySoapClient.cs`)

Implementación de `IVeriFactuGateway` que realiza llamadas SOAP reales a AEAT:

- **SubmitBillingRecordAsync()** ? Envía registro a `RegFactuSistemaFacturacion`
- **QueryBillingRecordAsync()** ? Consulta estado en `ConsultaFactuSistemaFacturacion`
- **CancelBillingRecordAsync()** ? Cancela registro en `AnulaRegistroFacturacion`

Características:
- Construcción de SOAP envelopes tipados
- Manejo de certificados X.509 para autenticación
- Timeout configurable
- Logging estructurado
- Validación de argumentos

**Nota:** La deserialización XML (ParseSoapResponse) está simplificada por ahora. En producción, usar `XDocument` con XPath/XElement.

### 4. **Configuración** (`VeriFactuOptions.cs`)

Clase de opciones que se vincula a `appsettings.json`:

```
VeriFactu:
  UseStaging: true/false
  ClientMode: "Stub" | "SoapClient"
  ProductionEndpoint: URL AEAT producción
  StagingEndpoint: URL AEAT staging
  CertificatePath: ruta del .pfx/.p12
  CertificatePassword: contraseña del certificado
  TimeoutSeconds: timeout HTTP
  MaxRetries: intentos de retry
  RetryDelayMs: espera inicial entre reintentos
```

### 5. **Registración DI** (`VeriFactuServiceCollectionExtensions.cs`)

Extension method `AddVeriFactuClient()` que:
- Lee configuración de `VeriFactuOptions`
- Selecciona implementación (Stub vs SoapClient) basado en `ClientMode`
- Registra `HttpClientFactory` con certificados y validación SSL
- Configura handler HTTP con manejo de certificados auto-firmados en staging

### 6. **Actualización DI Principal** (`DependencyInjection.cs`)

Cambio:
```csharp
// Antes:
services.AddScoped<IVeriFactuGateway, VeriFactuGatewayStub>();

// Ahora:
services.AddVeriFactuClient(configuration);
```

### 7. **Configuración de aplicación**

- `appsettings.json`: Modo desarrollo (Stub, staging)
- `appsettings.Production.json`: Modo producción (SoapClient, certificados desde Azure Key Vault)

---

## ??? Arquitectura de flujo

```
Client API Request
  ?
Command Handler (Application)
  ?
IVeriFactuGateway (puerto)
  ?
  ??? VeriFactuGatewayStub (desarrollo)
  ?
  ??? VeriFactuGatewaySoapClient (producción)
        ?
        AeatSoapMapper.ToSoapSubmissionRequest()
        ?
        HttpClient + SOAP envelope
        ?
        AEAT SOAP endpoint
        ?
        AeatSoapMapper.FromSoapSubmissionResponse()
        ?
VeriFactuSubmissionResult (modelo de negocio)
```

---

## ?? Seguridad

### Certificados X.509

1. **En desarrollo (Stub):** No se requiere certificado
2. **En staging:** Certificado auto-firmado permitido (validación permisiva)
3. **En producción:** Certificado real de AEAT, ruta desde config, contraseña desde Azure Key Vault

### Nunca loguear

- Contraseñas de certificados
- Contenido privado del certificado
- Payloads SOAP completos (solo resúmenes)

---

## ?? Próximos pasos

1. **Integración SOAP real:**
   - Usar herramienta `dotnet-svcutil` o `Add Connected Service` para generar tipos desde WSDL
   - Reemplazar tipos manuales `AeatSoapTypes` con generados

2. **Parsing XML robusto:**
   - Implementar `ParseSoapResponse<T>()` con `XDocument`
   - Validación contra XSD oficial AEAT
   - Manejo de SOAP Faults

3. **Firma XML:**
   - Integrar firma XAdES-4j para registros
   - Validación de firma en respuestas

4. **Observabilidad:**
   - Tracing de llamadas SOAP (correlation ID)
   - Alertas para tasas de error
   - Métricas de latencia

---

## ? Validación

- ? Compilación exitosa
- ? 45/45 tests pasando
- ? Estructura lista para integración real
- ? Switcheable Stub ? SOAP Client vía configuración

---

## ?? Referencias

- `/VERIFACTU/SistemaFacturacion.wsdl.xml` - Contrato SOAP AEAT
- `/VERIFACTU/SuministroInformacion.xsd.xml` - Estructura de registro
- `/VERIFACTU/RespuestaSuministro.xsd.xml` - Estructura de respuesta
- `/VERIFACTU/Validaciones_Errores_Veri-Factu.pdf` - Códigos de error AEAT

---

**Estado:** MVP de integración SOAP completado. Cliente configurable (stub/real).
