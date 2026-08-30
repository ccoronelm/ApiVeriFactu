# Primera prueba real contra AEAT TEST

Este documento prepara la primera remisión real de un **RegistroAlta F1** desde \`gesFactu\` al entorno oficial de pruebas VERI*FACTU.

> No guardar NIF, certificados, contraseñas ni datos reales en Git. En Development se usan **.NET User Secrets**.

## 1. Certificado

Instalar el certificado electrónico con clave privada en \`Cert:\CurrentUser\My\`.

Para localizar su thumbprint en PowerShell:

\`\`\`powershell
Get-ChildItem Cert:\CurrentUser\My |
  Select-Object Subject, Thumbprint, NotAfter, HasPrivateKey
\`\`\`

El certificado elegido debe tener clave privada, estar vigente y ser utilizable para la autenticación ante AEAT.

## 2. Configurar User Secrets

Desde la raíz del repositorio:

\`\`\`powershell
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
\`\`\`

Los datos de \`SistemaInformatico\` deben corresponder a la realidad del producto y del productor; no deben inventarse.

## 3. Base de datos

\`\`\`powershell
dotnet ef database update --project src/Infrastructure/gesFactu.Infrastructure.csproj --startup-project src/Api/gesFactu.Api/gesFactu.Api.csproj
\`\`\`

## 4. Compilar y probar

\`\`\`powershell
dotnet restore gesFactuSolucion.sln
dotnet build gesFactuSolucion.sln
dotnet test gesFactuSolucion.sln
\`\`\`

Todos los tests deben estar verdes.

## 5. Arrancar la API

\`\`\`powershell
dotnet run --project src/Api/gesFactu.Api/gesFactu.Api.csproj
\`\`\`

Con \`ClientMode=SoapClient\`, el arranque falla si falta el certificado, si no tiene clave privada, si está caducado, si faltan datos fiscales o si se intenta Production sin \`AllowProduction=true\`.

## 6. Primera factura

Usar un F1 matemáticamente coherente, por ejemplo base 100,00; IVA 21,00; total 121,00; fecha válida; serie+número no usados antes.

gesFactu calcula internamente \`FechaHoraHusoGenRegistro\`, huella SHA-256, encadenamiento, XML y validación XSD. El cliente **no envía PreviousRecordHash**.

## 7. Encolar

\`\`\`text
POST /api/v1/BillingRecords/{id}/submit
\`\`\`

La respuesta inmediata contiene un \`CorrelationId\` local. El CSV AEAT permanece \`null\` hasta una respuesta aceptada.

## 8. Flujo interno

\`\`\`text
BillingRecord
   ↓
validación XSD
   ↓
Transactional Outbox
   ↓
claim exclusivo SQL Server
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
\`\`\`

Nunca se marca el Outbox como procesado antes de persistir el resultado del intento.

## 9. Endpoint de pruebas

\`\`\`text
https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SistemaFacturacion/VerifactuSOAP
\`\`\`

## 10. Certificado no significa firma XML

En VERI*FACTU el certificado se usa para autenticación HTTPS/mTLS. No se añade XAdES al RegistroAlta; \`ds:Signature\` es opcional en el XSD oficial.
