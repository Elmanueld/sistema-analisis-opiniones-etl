# Entrega del Sistema de Análisis de Opiniones

Este repositorio reúne el sistema transaccional, el modelo analítico y la práctica de arquitectura y extracción.

## Actividad 2: arquitectura y extracción

- `etl-extraction/`: Worker Service .NET 8 con adaptadores CSV, SQL Server y API REST, staging JSONL y ciclo periódico configurable. Intervalo por defecto: un minuto; detener con Ctrl+C.
- `etl-api/`: API REST propia .NET 8 que expone comentarios sociales activos en `GET /api/comments`.
- `Arquitectura_Extraccion/`: diagrama de arquitectura y flujo, en draw.io y PNG/SVG.
- `Documento_Tecnico_Extraccion.pdf`: decisiones, atributos de calidad y fragmentos de evidencia del código. La versión Markdown contiene instrucciones complementarias.
- `Ejecutar extraccion completa.cmd`: inicia API y Worker en consolas separadas.

La extracción lee encuestas desde `DatosCSV/surveys_part1.csv`, reseñas web publicadas desde `SistemaBDTransaccional.dbo.ReseniasWeb` y comentarios desde la API, que consulta `dbo.ComentariosSociales`. La transformación y carga al warehouse analítico quedan para una fase posterior.

Antes de ejecutar, configura conexiones de SQL Server en User Secrets de ambos proyectos. Para otros entornos que Development, configura HTTPS y el mismo bearer token en la API y en el Worker. No publiques secretos.

## Actividad del modelo analítico

`Modelado/` contiene el esquema OLAP estrella, sus consultas y diagrama. `database/`, `DatosCSV/` y `etl/` contienen la fuente OLTP y su proceso de carga anterior. `Referencias/` reúne documentos recibidos como referencia.

## Actividad de carga del Data Warehouse

La nueva actividad carga las seis dimensiones y `DW.FactOpiniones` desde el OLTP. Revisa `Modelado/README_Carga_DataWarehouse.md`. El script `Modelado/02_CargarDataWarehouse.sql` corre el procedimiento idempotente; `Modelado/03_ValidarCargaDataWarehouse.sql` repite las validaciones. La evidencia de la carga y conteos verificados está en `Evidencias/Resultado_Carga_DataWarehouse.txt` y `Informe_Carga_DataWarehouse.pdf`.

En la instancia usada para la ejecución, SQL Server reportó 203 fechas, 201 productos, 501 clientes, 6 fuentes, 4 sentimientos, 1 evento y 406 hechos. La carga se ejecutó dos veces sin duplicar; todas las validaciones referenciales y de duplicados dieron cero errores.
