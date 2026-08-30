# gesFactu.AeatE2ETests

Pruebas automáticas reales contra **AEAT TEST**.

## Seguridad

- Nunca ejecutan contra Producción.
- Requieren `VeriFactu:Environment=Test`.
- Requieren `VeriFactu:AllowProduction=false`.
- Usan el certificado instalado en `CurrentUser/My` por thumbprint.
- El P12/PFX y su contraseña no forman parte del repositorio.
- Por defecto los tests están desactivados y aparecen como `Skipped`.

El proyecto comparte el mismo `UserSecretsId` que `gesFactu.Api`, por lo que reutiliza
la configuración VERI*FACTU TEST ya existente.

## Configuración adicional una sola vez

Configure un destinatario válido para las pruebas AEAT TEST:

```powershell
dotnet user-secrets --project .\src\Api\gesFactu.Api\gesFactu.Api.csproj set "AeatE2E:RecipientNif" "<NIF_TEST>"
dotnet user-secrets --project .\src\Api\gesFactu.Api\gesFactu.Api.csproj set "AeatE2E:RecipientName" "<NOMBRE_TEST>"
```

## Ejecutar

```powershell
$env:GESFACTU_RUN_AEAT_E2E="true"
dotnet test .\src\AeatE2ETests\gesFactu.AeatE2ETests.csproj --filter "Category=AEAT-E2E"
Remove-Item Env:\GESFACTU_RUN_AEAT_E2E
```

Actualmente la batería real contiene un smoke test `RegistroAlta F1`. Los próximos
bloques (F2, rectificativas, anulación y consultas) se incorporarán aquí a medida que
se implemente cada capacidad.
