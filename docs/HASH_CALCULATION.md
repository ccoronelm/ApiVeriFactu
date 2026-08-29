# Cálculo de Hash/Huella en VERI*FACTU

## Referencia oficial

- Documento: `/VERIFACTU/Veri-Factu_especificaciones_huella_hash_registros.pdf`
- Tipo de huella: 01 (SHA256)
- Algoritmo: SHA256
- Formato: Hexadecimal en mayúsculas (64 caracteres)

## Implementación

**Interfaz:** `gesFactu.Application.Common.Abstractions.IHashCalculator`
**Implementación:** `gesFactu.Infrastructure.VeriFactu.Sha256HashCalculator`

## Propiedades del hash

- **Determinista**: Mismos datos siempre producen el mismo hash
- **Culture-independent**: Usa `CultureInfo.InvariantCulture` para formatos
- **Encadenamiento**: Incorpora hash anterior para cadena de registros
- **Validación**: Integral para cumplimiento VERI*FACTU

## Campos incluidos en el hash (en orden)

1. Hash del registro anterior (cadena vacía si es primero)
2. NIF/CIF del emisor (normalizado a mayúsculas)
3. Serie de factura
4. Número de factura
5. Fecha de expedición (formato: dd-MM-yyyy)
6. Tipo de factura (ej: "F1", "R3")
7. Importe total (formato: 0.00 con InvariantCulture)
8. Cuota total de impuesto (formato: 0.00 con InvariantCulture)
9. Descripción de la operación
10. Timestamp del registro (ISO 8601 con zona horaria)
11. Identificador del sistema informático

**Separador entre campos:** Pipe `|`

## Ejemplo de cálculo

### Datos de entrada

```json
{
  "previousHash": "",
  "issuerNif": "12345678A",
  "invoiceSeries": "A",
  "invoiceNumber": "001",
  "issueDate": "03-02-2025",
  "invoiceType": "F1",
  "totalAmount": 100.00,
  "totalTaxAmount": 21.00,
  "description": "Servicios",
  "registerTimestamp": "2025-02-03T14:30:00+01:00",
  "softwareId": "gesFactu-1.0"
}
```

### String de entrada para hash

```
|12345678A|A|001|03-02-2025|F1|100.00|21.00|Servicios|2025-02-03T14:30:00+01:00|gesFactu-1.0
```

### Hash resultante

```
FF954378B64ED331A9B2366AD317D86E9DEC1716B12DD0ACCB172A6DC4C105AA
```

## Encadenamiento

La cadena de registros es crítica. El hash de cada nuevo registro incluye el hash del anterior:

1. **Registro 1 (primero):**
   - `previousHash = ""`
   - Calcula su propio hash

2. **Registro 2:**
   - `previousHash = hash_del_registro_1`
   - Calcula su propio hash basado en el anterior

3. **Registro N:**
   - `previousHash = hash_del_registro_N-1`
   - Calcula su propio hash

## Validación de tests

Todos los cálculos de hash deben validarse contra:

1. **Tests unitarios deterministas** (`Sha256HashCalculatorTests`)
2. **Valores SHA256 conocidos** (test con "hello world")
3. **Ejemplos oficiales AEAT** (si están disponibles)
4. **Consistencia con encadenamiento** (hash anterior incluido correctamente)

## NOTAs importantes

- ?? El hash es **inmutable** una vez calculado
- ?? Cualquier cambio en los datos debe recalcular el hash
- ?? La cadena de hashes es crítica para auditoría fiscal
- ?? No usar cultura local para formateo de importes
- ?? UTF-8 sin BOM para codificación

## Integración

En `CreateBillingRecordCommandHandler`:

```csharp
var hashInput = new BillingRecordHashInput
{
    PreviousHash = command.PreviousRecordHash ?? string.Empty,
    IssuerNif = nif.Value,
    InvoiceSeries = series.Value,
    InvoiceNumber = number.Value,
    IssueDate = command.IssueDate,
    TotalAmount = totalAmount.Amount,
    TotalTaxAmount = totalTaxAmount.Amount,
    Description = command.Description,
    RegisterTimestamp = DateTime.UtcNow.ToString("o"),
    SoftwareId = "gesFactu-1.0"
};

var calculatedHash = _hashCalculator.CalculateChainHash(hashInput);
billingRecord.SetComputedHash(calculatedHash);
```

## TODOs

- [ ] Validar contra ejemplos oficiales AEAT si están disponibles
- [ ] Incluir tipo de factura en comando de creación
- [ ] Configurar SoftwareId desde application settings
- [ ] Crear tests de integración con ejemplos oficiales
