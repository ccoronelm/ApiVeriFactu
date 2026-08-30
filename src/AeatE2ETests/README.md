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

## Matriz E2E real

La batería cubre las dos operaciones SOAP oficiales del WSDL:

- `RegFactuSistemaFacturacion`
  - alta F1;
  - alta F2;
  - subsanación;
  - RegistroAnulacion;
  - rectificativas R1, R2, R3, R4 y R5;
  - rectificativa sustitutiva R1-S;
  - múltiples tipos de IVA;
  - operación exenta E1;
  - operación no sujeta N1;
  - recargo de equivalencia;
  - reenvío duplicado y respuesta AEAT 3000.
- `ConsultaFactuSistemaFacturacion`
  - consulta por identidad de factura;
  - respuesta SinDatos;
  - filtros por contraparte, rango de fechas y SistemaInformatico.

La paginación completa no se fuerza contra AEAT TEST porque requeriría generar más
registros que el tamaño máximo de página. La construcción y parsing de
`IndicadorPaginacion`/`ClavePaginacion` se cubren contra los XSD oficiales en
`Infrastructure.Tests`.

> `RegistroAnulacion` no es una tercera operación SOAP. Viaja dentro de
> `RegFactuSistemaFacturacion`, tal como define el WSDL oficial.

## Ejecutar toda la batería

```powershell
$env:GESFACTU_RUN_AEAT_E2E="true"
dotnet test .\src\AeatE2ETests\gesFactu.AeatE2ETests.csproj `
  --filter "Category=AEAT-E2E" `
  --logger "console;verbosity=normal"
Remove-Item Env:\GESFACTU_RUN_AEAT_E2E
```

Una ejecución válida para cierre de release debe finalizar sin tests `Failed` ni
`Skipped`.
