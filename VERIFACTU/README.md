# VERI*FACTU — índice de documentación local

Esta carpeta contiene la documentación oficial de AEAT/Hacienda utilizada como fuente de verdad para el desarrollo de `gesFactu`.

> Este README es únicamente un índice de ayuda. Los documentos oficiales originales son la fuente autoritativa.

## Regla para desarrollo

Antes de implementar o modificar una regla fiscal VERI*FACTU:

1. localizar aquí el documento oficial relevante;
2. revisar, cuando proceda, XSD, WSDL y ejemplos oficiales;
3. identificar la regla exacta;
4. implementar en la capa correcta de Clean Architecture;
5. añadir o actualizar pruebas.

Si dos documentos oficiales parecen contradecirse, no se debe adivinar: hay que dejar constancia del conflicto y resolverlo antes de cambiar el comportamiento fiscal.

## Organización recomendada

No es obligatorio mover lo que ya existe. A medida que crezca la documentación, puede organizarse en carpetas como:

```text
VERIFACTU/
├── README.md
├── Normativa/
├── EspecificacionesTecnicas/
├── XSD/
├── WSDL/
├── Ejemplos/
├── AnexosEjemplosFirmaRegFact/
└── FAQ/
```

Mantener, cuando sea posible, los nombres originales de los archivos descargados de AEAT.

## Índice

Completar esta sección a medida que se incorporen documentos.

### Registro de alta
- Pendiente de indexar.

### Registro de anulación
- Pendiente de indexar.

### Huella / Hash
- Pendiente de indexar.

### Encadenamiento
- Pendiente de indexar.

### QR
- Pendiente de indexar.

### Identificación del sistema informático
- Pendiente de indexar.

### Servicios web / SOAP / WSDL
- Pendiente de indexar.

### Esquemas XSD
- Pendiente de indexar.

### Errores y validaciones AEAT
- Pendiente de indexar.

### Ejemplos oficiales
- `AnexosEjemplosFirmaRegFact/` — ejemplos/anexos ya presentes en el repositorio.
