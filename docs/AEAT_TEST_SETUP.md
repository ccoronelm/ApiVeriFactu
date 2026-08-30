# Primera prueba real contra AEAT TEST

Este documento prepara la primera remisión real de un **RegistroAlta F1** desde `gesFactu` al entorno oficial de pruebas VERI*FACTU.

> No guardar NIF, certificados, contraseñas ni datos reales en Git. En Development se usan **.NET User Secrets**.

## 1. Certificado

Instalar el certificado electrónico con clave privada en `Cert:\CurrentUser\My`.

Para localizar su thumbprint en PowerShell:

```powershell
Get-ChildItem Cert:\CurrentUser\My |
  Select-Object Subject, Thumbprint, NotAfter, HasPrivateKey
```

El certificado elegido debe tener clave privada, estar vigente y ser utilizable para autenticación ante AEAT.

## 2. Configurar User Secrets

Desde la raíz del repositorio:

```powershell
$project = "src/Api/gesFactu.Api/gesFactu.Api.csproj"

dotnet user-secrets set "VeriFactu:Environment" "Test" --project $project
dotnet user-secrets set "VeriFactu:AllowProduction" "false" --project $project
dotnet user-secrets set "VeriFactu:ClientMode" "SoapClient" --project $project

dotnet user-secrets set "VeriFactu:Taxpayer:Nif" "<NIF_REAL>" --project $project
dotnet user-secrets set "VeriFactu:Taxpayer:Name" "<NOMBRE_O_RAZON_REAL>" --project $project
dotnet user-secrets set "VeriFactu:Certificate:Thumbprint" "<THUMBPRINT>" --project $project

dotnet user-secrets set "VeriFactu:SistemaInformatico:NombreRazon" "<PRODUCTOR_REAL>" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:Nif" "<NIF_PRODUCTOR_REAL>" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:NombreSistemaInformatico" "gesFactu" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:IdSistemaInformatico" "<ID_1_O_2_CARACTERES>" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:Version" "1.0.0" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:NumeroInstalacion" "<NUMERO_INSTALACION>" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:TipoUsoPosibleSoloVerifactu" "S" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:TipoUsoPosibleMultiOT" "N" --project $project
dotnet user-secrets set "VeriFactu:SistemaInformatico:IndicadorMultiplesOT" "N" --project $project
```

Los datos de `SistemaInformatico` deben corresponder a la realidad del producto y del productor; no deben inventarse.

## 3. Base de datos PostgreSQL

La contraseña real no se guarda en Git. En Development se configura la conexión mediante User Secrets:

```powershell
$project = "src/Api/gesFactu.Api/gesFactu.Api.csproj"
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=gesFactuDb;Username=gesfactu;Password=<PASSWORD>" --project $project

$env:GESFACTU_DESIGN_CONNECTION = "Host=localhost;Port=5432;Database=gesFactuDb;Username=gesfactu;Password=<PASSWORD>"
dotnet tool restore
dotnet ef database update --project src/Infrastructure/gesFactu.Infrastructure.csproj --startup-project src/Api/gesFactu.Api/gesFactu.Api.csproj --connection $env:GESFACTU_DESIGN_CONNECTION
Remove-Item Env:GESFACTU_DESIGN_CONNECTION
```

## 4. Compilar y probar

```powershell
dotnet restore gesFactuSolucion.sln
dotnet build gesFactuSolucion.sln
dotnet test gesFactuSolucion.sln
```

Todos los tests deben estar verdes.

## 5. Arrancar la API

```powershell
dotnet run --project src/Api/gesFactu.Api/gesFactu.Api.csproj
```

Con `ClientMode=SoapClient`, el arranque falla si falta el certificado, si no tiene clave privada, si está caducado, si faltan datos fiscales o si se intenta Production sin `AllowProduction=true`.

## 6. Crear la primera factura F1

Para F1, AEAT exige el bloque `Destinatarios`. La petición debe incluir un destinatario real, distinto del obligado emisor.

Usar una factura matemáticamente coherente; para la primera prueba, por ejemplo base 100,00, IVA 21,00 y total 121,00.

```http
POST /api/v1/BillingRecords
Content-Type: application/json
```

```json
{
  "issuerNif": "<NIF_REAL>",
  "invoiceSeries": "VF/",
  "invoiceNumber": "000001",
  "issueDate": "30-08-2026",
  "issuerName": "<NOMBRE_O_RAZON_REAL>",
  "recipientNif": "<NIF_DESTINATARIO_REAL>",
  "recipientName": "<NOMBRE_DESTINATARIO_REAL>",
  "description": "Servicio de prueba",
  "totalAmount": 121.00,
  "totalTaxAmount": 21.00
}
```

`RecipientNif` debe tener 9 caracteres y ser distinto de `IssuerNif`.

gesFactu calcula internamente `FechaHoraHusoGenRegistro`, huella SHA-256, encadenamiento y la identidad del registro anterior. El cliente **no envía PreviousRecordHash**.

## 7. Encolar la remisión

Con el `BillingRecordId` obtenido:

```http
POST /api/v1/BillingRecords/{id}/submit
```

Antes de crear el Outbox, gesFactu genera el XML y lo valida contra los XSD oficiales.

La respuesta inmediata contiene un `CorrelationId` local. El CSV AEAT permanece `null` hasta que exista una respuesta aceptada.

## 8. Consultar estado e intentos

```http
GET /api/v1/BillingRecords/{id}
GET /api/v1/BillingRecords/{id}/submission-attempts
```

El historial conserva cada intento, la respuesta recibida y el CSV cuando AEAT lo devuelve.

## 9. Flujo interno

```text
BillingRecord
   ↓
XML RegistroAlta
   ↓
validación XSD oficial
   ↓
Transactional Outbox
   ↓
claim exclusivo PostgreSQL (`FOR UPDATE SKIP LOCKED`)
   ↓
SubmissionAttempt
   ↓
HTTPS mTLS + SOAP 1.1
   ↓
respuesta validada contra XSD
   ↓
persistir respuesta / CSV / estado
   ↓
cerrar Outbox
```

Si el resultado de una comunicación quedó incierto y un reintento recibe el error AEAT 3000, gesFactu solo lo reconcilia como éxito cuando `RegistroDuplicado` confirma que el registro existente está `Correcta` o `AceptadaConErrores`. No se inventa un CSV.

## 10. Endpoint de pruebas

```text
https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP
```

## 11. Certificado no significa firma XML

En VERI*FACTU el certificado se usa para autenticación HTTPS/mTLS. No se añade XAdES al RegistroAlta; `ds:Signature` es opcional en el XSD oficial.

## 12. Alcance de esta fase

Esta preparación cubre el primer **RegistroAlta F1** contra AEAT TEST. No implica que estén terminados todavía otros tipos de factura, rectificativas, anulaciones, consultas o el resto de obligaciones funcionales del producto.
