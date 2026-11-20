# Sistema de Análisis de Opiniones

Repositorio de entregas del proyecto académico: sistema OLTP, modelo analítico OLAP y Actividad 2 de arquitectura y extracción.

## Actividad 2: extracción

- `etl-extraction/`: Worker Service .NET 8, adaptadores `CsvExtractor`, `DatabaseExtractor` y `ApiExtractor`, e implementación de staging JSONL.
- `etl-api/`: API REST .NET 8. `GET /api/comments` expone comentarios activos de `dbo.ComentariosSociales`; el Worker los consume. `GET /health` comprueba disponibilidad.
- `Arquitectura_Extraccion/`: diagrama de arquitectura y flujo, editables en draw.io y exportados en PNG/SVG.
- `Documento_Tecnico_Extraccion.pdf` y `.md`: decisiones, atributos de calidad, pasos y límites.
- `Ejecutar extraccion completa.cmd`: inicia API y Worker en ventanas de consola.

El Worker extrae encuestas únicamente desde `DatosCSV/surveys_part1.csv`, reseñas web publicadas (`Estado = 1`) desde SQL Server y comentarios sociales mediante la API propia. Genera JSONL en staging; transformación y carga a OLAP quedan para una actividad posterior.

## Ejecutar extracción

Requisitos: .NET 8 SDK y SQL Server con `SistemaBDTransaccional` instalado y poblado. Configura las conexiones en User Secrets (por separado en ambos proyectos, con instrucciones detalladas en `Documento_Tecnico_Extraccion.md`) y ejecuta `Ejecutar extraccion completa.cmd`.

La API usa HTTP solo en localhost durante desarrollo. En otro ambiente requiere HTTPS y `Api__BearerToken`; el Worker debe tener el mismo token en `Sources:Api:BearerToken`. No guardes secretos en el repositorio.

## Estructura restante

- `database/`, `DatosCSV/`, `etl/`: base y aplicación transaccional de origen.
- `Modelado/`: scripts, consultas y diagrama del modelo analítico tipo estrella.
- `Referencias/`: documentos recibidos como material de referencia.

`config-conexion.example.txt` es una plantilla; copia y personaliza localmente, sin subir credenciales reales.
