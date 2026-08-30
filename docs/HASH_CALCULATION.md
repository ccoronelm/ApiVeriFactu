# Cálculo de Hash/Huella en VERI*FACTU

## Referencia oficial

- Documento: `/VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf`
- Tipo de huella: `01` (SHA-256)
- Codificación de entrada: UTF-8
- Salida: hexadecimal en mayúsculas, 64 caracteres

## Cadena de entrada para RegistroAlta

La huella se calcula sobre esta cadena, en este orden exacto:

```text
IDEmisorFactura=...&NumSerieFactura=...&FechaExpedicionFactura=...&TipoFactura=...&CuotaTotal=...&ImporteTotal=...&Huella=...&FechaHoraHusoGenRegistro=...
```

No se usa un formato separado por pipes ni se incluyen la descripción o el identificador del sistema informático en esta cadena.

Para el primer registro, `Huella=` se deja vacío. Para los siguientes registros contiene la huella del registro anterior.

## Formato de los valores

Los valores usados para calcular la huella deben coincidir con los que se envían en el XML:

- `IDEmisorFactura`: NIF del emisor.
- `NumSerieFactura`: concatenación de serie + número.
- `FechaExpedicionFactura`: `dd-MM-yyyy`.
- `TipoFactura`: por ejemplo `F1`.
- `CuotaTotal`: dos decimales con punto, por ejemplo `21.00`.
- `ImporteTotal`: dos decimales con punto, por ejemplo `121.00`.
- `Huella`: huella anterior o cadena vacía para el primer registro.
- `FechaHoraHusoGenRegistro`: exactamente el timestamp persistido y enviado en el XML, incluyendo el huso horario.

Es importante conservar los dos decimales en importes enteros. AEAT calcula la huella sobre la representación textual recibida; `21` y `21.00` no producen el mismo SHA-256.

## Ejemplo

```text
IDEmisorFactura=89890001K&NumSerieFactura=VF/000001&FechaExpedicionFactura=30-08-2026&TipoFactura=F1&CuotaTotal=21.00&ImporteTotal=121.00&Huella=&FechaHoraHusoGenRegistro=2026-08-30T12:32:26+02:00
```

Huella SHA-256:

```text
C8318DAF719A9A7E6508D0181111E88890DECA414E6175CA9B422006CB1783D7
```

## Implementación

- Interfaz: `gesFactu.Application.Common.Abstractions.IHashCalculator`
- Implementación: `gesFactu.Infrastructure.VeriFactu.Sha256HashCalculator`
- XML: `RegistroAltaXmlBuilder.FormatImporte` usa `0.00`; el cálculo de huella debe usar la misma representación.

## Encadenamiento

1. Primer registro: `Huella=`.
2. Siguiente registro: `Huella=<huella del registro anterior>`.
3. El valor de `FechaHoraHusoGenRegistro` se genera una vez, se persiste y se reutiliza tanto para el hash como para el XML.

## Validación

Los tests incluyen:

- Los vectores oficiales AEAT de los casos 6.1 y 6.2.
- Un caso de regresión con importes enteros para garantizar que se envían como `21.00` y `121.00`.
- Determinismo, normalización de espacios y equivalencia de valores decimal numéricamente iguales.
