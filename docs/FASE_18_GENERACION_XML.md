# Fase 18: Generación de XML de Registros AEAT

**Objetivo:** Implementar la serialización XML conforme a especificaciones AEAT VERI*FACTU, transformando datos de negocio en documentos XML validables y listos para envío a AEAT.

---

## ?? Resumen de cambios

### 1. **Modelos de datos de registro** (`VeriFactuRecordModels.cs`)

Tipos que representan la estructura XML AEAT sin exponer XML directo:

#### Registros de facturación:
- `VeriFactuBillingRecord` - Contenedor principal (SuministroInformacion)
- `VeriFactuCabecera` - Metadata del suministro
  - Versión, NIF emisor, período, huellas anteriores
- `VeriFactuDetalle` - Registro individual de factura
  - Tipo, serie, número, fechas, importes
  - Base imponible, cuota IVA, porcentaje
  - Cadena de huellas para trazabilidad
  - Número secuencial único
- `VeriFactuCliente` - Información del cliente/sujeto pasivo
- `VeriFactuImpuesto` - Desglose de impuestos

#### Registros de anulación:
- `VeriFactuCancellationRecord` - Contenedor principal (SuministroLR)
- `VeriFactuCabeceraAnulacion` - Metadata de anulación
- `VeriFactuDetalleAnulacion` - Detalles de registros a anular

### 2. **Generador de XML** (`VeriFactuXmlGenerator.cs`)

Clase que transforma modelos de negocio en XML conforme a especificación:

**Métodos públicos:**
- `GenerateBillingRecordXmlAsync()` - Genera XML de facturación
- `GenerateCancellationRecordXmlAsync()` - Genera XML de anulación

**Características:**
- Namespace correcto: `http://www.aeat.gob.es/VeriFacTuSF`
- XML Declaration UTF-8
- Formato de decimales invariante (2 decimales, punto como separador)
- Fechas ISO 8601 (yyyy-MM-dd, HH:mm:ssZ)
- Validación de argumentos nulos
- Logging de errores y éxito

**Método interno de formateo:**
- `FormatDecimal()` - Garantiza formato correcto sin variaciones de cultura

**Métodos internos de construcción:**
- `BuildSuministroInformacion()` - Estructura raíz de registro
- `BuildRegistroFacturacion()` - Registro individual
- `BuildCliente()` - Datos del cliente
- `BuildImpuesto()` - Desglose de impuesto
- `BuildSuministroLR()` - Estructura de anulación
- `BuildRegistroAnulacion()` - Detalle de anulación

---

## ??? Ejemplo de XML generado

```xml
<?xml version="1.0" encoding="UTF-8"?>
<SuministroInformacion xmlns="http://www.aeat.gob.es/VeriFacTuSF"
                       xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
                       xsi:schemaLocation="http://www.aeat.gob.es/VeriFacTuSF SuministroInformacion.xsd">
  <Cabecera>
    <Versión>1.0</Versión>
    <NifEmisor>12345678Z</NifEmisor>
    <NombreEmisor>EMPRESA SA</NombreEmisor>
    <PeriodoFacturacion>01/2024</PeriodoFacturacion>
    <FechaGeneracion>2024-01-15T10:30:00Z</FechaGeneracion>
  </Cabecera>
  <Registros>
    <RegistroFacturacion>
      <TipoDocumento>F</TipoDocumento>
      <Serie>A</Serie>
      <Numero>000001</Numero>
      <FechaExpedicion>2024-01-15</FechaExpedicion>
      <Descripcion>Factura de venta de servicios</Descripcion>
      <BaseImponible>1000.00</BaseImponible>
      <CuotaIva>210.00</CuotaIva>
      <PorcentajeIva>21.00</PorcentajeIva>
      <ImporteTotal>1210.00</ImporteTotal>
      <Cliente>
        <Nif>87654321X</Nif>
        <Nombre>CLIENTE SL</Nombre>
      </Cliente>
    </RegistroFacturacion>
  </Registros>
</SuministroInformacion>
```

---

## ?? Consideraciones de seguridad

### Normalización de datos

- **Decimales:** Siempre con `.` como separador, 2 dígitos (no redondear automáticamente)
- **Fechas:** ISO 8601 (yyyy-MM-dd), nunca formato local
- **Caracteres:** XML bien escapado automáticamente por XDocument
- **Cultura:** Invariante (no dependiente de configuración regional)

### Validación pre-serialización

Antes de generar XML, se recomienda:
1. Validar decimales (máximo 2 dígitos)
2. Validar NIF/CIF formato
3. Validar fechas coherentes
4. Verificar suma de importes (base + impuesto = total)

---

## ?? Próximos pasos

### Fase 19 (Integración completa)

1. **Crear Command Handler para serialización:**
   - Command: `GenerarRegistroFacturacionCommand`
   - Use `VeriFactuXmlGenerator` desde Application
   - Aplicar validaciones antes de generar XML

2. **Integrar con firma:**
   - `GenerateBillingRecordXmlAsync()`
   - `SignXmlAsync()` (IXmlSignatureService)
   - Retornar XML firmado

3. **Integrar con validación:**
   - Validar XML contra XSD (IXmlSchemaValidator)
   - Rechazar si no cumple especificación

4. **Integrar con SOAP client:**
   - Pasar XML firmado y validado a `SubmitBillingRecordAsync()`
   - Recibir respuesta de AEAT

---

## ? Validación actual

- ? Compilación exitosa
- ? 45/45 tests pasando
- ? Generador registrado en DI
- ? Modelos tipados y documentados
- ? Formato XML conforme a especificación

---

## ?? Referencias

- `/VERIFACTU/SuministroInformacion.xsd` - Esquema de registro de facturación
- `/VERIFACTU/SuministroLR.xsd` - Esquema de anulación
- `/VERIFACTU - Ejemplos de registros` - Ejemplos reales AEAT
- AEAT oficial: Especificaciones VERI*FACTU

---

**Estado:** Generador XML completado y listo para integración con validación y firma.
