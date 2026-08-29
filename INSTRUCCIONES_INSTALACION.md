# Instalación de las instrucciones de GitHub Copilot en gesFactu

## Estructura esperada

Tu carpeta `backend` debe quedar así:

```text
backend/
├── .github/
│   ├── copilot-instructions.md
│   └── instructions/
│       ├── api.instructions.md
│       ├── application.instructions.md
│       ├── documentation.instructions.md
│       ├── domain.instructions.md
│       ├── infrastructure.instructions.md
│       ├── tests.instructions.md
│       └── verifactu.instructions.md
├── src/
│   ├── Api/
│   ├── Core/
│   └── Infrastructure/
└── VERIFACTU/
    ├── README.md
    └── ...
```

## Cómo instalar

1. Confirma que `backend` es la raíz del repositorio Git (o que lo será cuando ejecutes `git init`).
2. Copia la carpeta `.github` de este paquete directamente dentro de `backend`.
3. Copia `VERIFACTU/README.md` dentro de tu carpeta existente `backend/VERIFACTU`.
4. No borres ni reemplaces la documentación oficial que ya tienes dentro de `VERIFACTU`.
5. Abre la solución desde el repositorio/workspace correcto en Visual Studio.
6. Comprueba que GitHub Copilot tiene habilitadas las instrucciones personalizadas.
7. Prueba en Copilot Chat: `Resume las reglas de arquitectura e integración VERI*FACTU que debes seguir en este repositorio.`

La respuesta debería mencionar, como mínimo:

- Clean Architecture
- Domain / Application / Infrastructure / Api
- consulta obligatoria de `/VERIFACTU`
- aislamiento de AEAT mediante Anti-Corruption Layer
- hash único y determinista
- encadenamiento seguro ante concurrencia
- idempotencia
- Outbox
- no exponer tipos WSDL/SOAP fuera de Infrastructure

## Nota sobre Git

No es necesario haber subido todavía el repositorio a GitHub para crear estos archivos localmente.

Cuando publiques el repositorio, incluye `.github` en Git para que las instrucciones viajen con el proyecto.

La carpeta `/VERIFACTU` puede contener documentación grande. Antes de publicarla, revisa derechos de redistribución, tamaño y si quieres versionar todos los PDF/ejemplos o mantener parte de la documentación solo localmente.
