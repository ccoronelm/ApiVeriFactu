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

### Registro de alta / anulación / rectificativas
- `SuministroLR.xsd.xml`
- `SuministroInformacion.xsd.xml`
- `RespuestaSuministro.xsd.xml`
- `Validaciones_Errores_Veri-Factu.pdf`
- `errores.properties.txt`

### Huella / Hash / Encadenamiento
- `Veri-Factu_especificaciones_huella_hash_registros.pdf`
- `SuministroInformacion.xsd.xml`

### QR
- `DetalleEspecificacTecnCodigoQRfactura.pdf`

### Sistema informático / multiobligado
- `SuministroInformacion.xsd.xml`
- `Veri-Factu_Descripcion_SWeb.pdf`
- `SistemaFacturacion.wsdl.xml`

### Servicios web / SOAP / WSDL
- `SistemaFacturacion.wsdl.xml`
- Operaciones oficiales:
  - `RegFactuSistemaFacturacion`
  - `ConsultaFactuSistemaFacturacion`

### Consultas
- `ConsultaLR.xsd.xml`
- `RespuestaConsultaLR.xsd.xml`
- `SistemaFacturacion.wsdl.xml`

### Errores y validaciones AEAT
- `Validaciones_Errores_Veri-Factu.pdf`
- `errores.properties.txt`

### Firma electrónica
- `EspecTecGenerFirmaElectRfact.pdf`
- `AnexosEjemplosFirmaRegFact/`

> Los documentos de firma se conservan como documentación oficial, pero los registros
> en modalidad VERI*FACTU de gesFactu no se firman con XML/XAdES. La autenticación
> de la remisión se realiza mediante certificado electrónico/mTLS.

### Ejemplos oficiales
- `AnexosEjemplosFirmaRegFact/`
- `EjemplosDeclaracionResponsable(V0.5.1).pdf`
