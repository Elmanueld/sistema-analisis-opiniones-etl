# Sistema de Análisis de Opiniones

Repositorio de entregas del proyecto académico: sistema OLTP, modelo analítico OLAP, extracción y carga del Data Warehouse.

## Carga del Data Warehouse

- `Modelado/01_SistemaBDAnalitica.sql`: crea la base, dimensiones, hecho y miembros desconocidos.
- `Modelado/02_CargarDataWarehouse.sql`: carga idempotente desde `SistemaBDTransaccional` a dimensiones y `DW.FactOpiniones`.
- `Modelado/03_ValidarCargaDataWarehouse.sql`: valida conteos, cobertura dimensional y duplicados.
- `Modelado/README_Carga_DataWarehouse.md`: reglas de mapeo y pasos para reproducir.
- `Evidencias/Resultado_Carga_DataWarehouse.txt`: resumen de la ejecución y consultas reales en SQL Server local.
- `Informe_Carga_DataWarehouse.pdf`: descripción del proceso, tablas cargadas, evidencias e hipervínculo al repositorio.

Para cargar y verificar con SQL Server local, desde la raíz ejecuta:

```powershell
sqlcmd -C -I -b -S .\SQLEXPRESS -E -i Modelado\01_SistemaBDAnalitica.sql
sqlcmd -C -I -b -S .\SQLEXPRESS -E -i Modelado\02_CargarDataWarehouse.sql
sqlcmd -C -I -b -S .\SQLEXPRESS -E -i Modelado\03_ValidarCargaDataWarehouse.sql
```

La carga observada insertó 406 hechos: 6 encuestas, 200 reseñas y 200 comentarios sociales. La segunda ejecución conservó los conteos; las comprobaciones de claves y duplicados dieron cero.

## Actividad de arquitectura y extracción

- `etl-extraction/`: Worker Service .NET 8 con adaptadores CSV, SQL Server y API REST, staging JSONL y ciclo periódico.
- `etl-api/`: API REST propia que expone en `GET /api/comments` los comentarios sociales almacenados en el OLTP.
- `Arquitectura_Extraccion/`: diagramas draw.io y exportaciones PNG/SVG.
- `Documento_Tecnico_Extraccion.pdf`: decisiones de diseño, atributos de calidad y evidencias de código.

## Proyecto transaccional y modelo

- `database/`, `DatosCSV/`, `etl/`: OLTP y su proceso de carga previo.
- `Modelado/`: modelo estrella, consultas y diagrama analítico.
- `Referencias/`: documentos entregados como material de referencia.

Las cadenas de conexión y tokens deben permanecer fuera del repositorio.
