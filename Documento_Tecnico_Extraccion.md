# Actividad 2 · Arquitectura y extracción ETL

## Alcance según la explicación del maestro

Esta práctica implementa únicamente la extracción: encuestas internas desde `DatosCSV/surveys_part1.csv`, reseñas web publicadas desde `SistemaBDTransaccional.dbo.ReseniasWeb` y comentarios sociales desde una API REST propia. La API consulta `SistemaBDTransaccional.dbo.ComentariosSociales`; el Worker consume su JSON. Las filas extraídas se guardan en staging JSONL. La transformación y carga al warehouse analítico no forman parte de esta actividad.

## Arquitectura y flujo

1. `Opiniones.CommentsApi` (.NET 8) lee comentarios activos de `ComentariosSociales` y los expone en `GET /api/comments`.
2. `Opiniones.ExtractionWorker` (.NET 8 Worker Service) ejecuta tres adaptadores: encuestas CSV, reseñas de SQL Server y comentarios mediante REST.
3. Cada adaptador escribe registros normalizados en un archivo JSON Lines propio. El archivo se publica al terminar correctamente; los archivos parciales no se consideran resultado válido.
4. La siguiente etapa podrá transformar esos archivos y cargar `SistemaBDAnalitica`; esa carga queda explícitamente fuera del alcance.

## Decisiones de diseño y calidad

- **Polimorfismo y Open/Closed:** `IExtractor` permite que el Worker recorra adaptadores concretos sin conocer su implementación; una nueva fuente se agrega como adaptador y registro de DI.
- **Responsabilidad única / segregación:** `CsvExtractor`, `DatabaseExtractor`, `ApiExtractor` y `JsonLinesStagingWriter` mantienen tareas separadas. La API separa la consulta de comentarios de la ejecución del Worker.
- **Rendimiento:** lecturas asíncronas; lectura CSV y SQL secuencial; respuesta HTTP procesada como stream; fuentes independientes ejecutadas concurrentemente y staging escrito por flujo.
- **Escalabilidad:** adaptadores desacoplados y JSON canónico permiten incorporar nuevos formatos sin modificar los existentes. La concurrencia queda acotada a tres fuentes.
- **Seguridad:** no se incluyen secretos en el repositorio; la cadena SQL va en User Secrets o variables protegidas. Se requiere HTTPS fuera de localhost; no se registran credenciales.
- **Mantenibilidad:** .NET 8, interfaces, adaptadores, opciones de configuración, DI e `ILogger` estructurado. El contrato API usa nombres JSON estables y puede versionarse.

## Ejecutar localmente

Necesitas .NET 8 y SQL Server con la base poblada mediante el proceso anterior. Configura la misma conexión en los dos proyectos, en sus respectivos User Secrets:

```powershell
cd etl-api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OperationalDb" "Server=.\SQLEXPRESS;Database=SistemaBDTransaccional;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet run --urls http://localhost:5080
```

En otra terminal, desde la raíz del proyecto:

```powershell
cd etl-extraction
dotnet user-secrets init
dotnet user-secrets set "Sources:Database:ConnectionString" "Server=.\SQLEXPRESS;Database=SistemaBDTransaccional;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet run
```

El Worker llama la API local en `http://localhost:5080/api/comments`; fuera de loopback debe configurarse HTTPS. CSV y SQL se habilitan por defecto; el endpoint REST local también queda preconfigurado. La ejecución crea los tres JSONL en `etl-extraction/staging`.

## Validación y límites

Los proyectos API y Worker compilan con .NET 8. La extracción de CSV no requiere SQL. Para completar las tres fuentes debe estar disponible SQL Server con reseñas/comentarios cargados y ejecutar la API antes del Worker. No se afirma validación de integración contra una instancia SQL real. Este entregable no implementa transformación ni carga OLAP.
