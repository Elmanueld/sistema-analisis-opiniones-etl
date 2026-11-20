# Actividad 2 · Arquitectura y extracción ETL

## Alcance según la explicación del maestro

Esta práctica implementa únicamente la extracción: encuestas internas desde `DatosCSV/surveys_part1.csv`, reseñas web publicadas desde `SistemaBDTransaccional.dbo.ReseniasWeb` y comentarios sociales desde una API REST propia. La API consulta `SistemaBDTransaccional.dbo.ComentariosSociales`; el Worker consume su JSON. Las filas extraídas se guardan en staging JSONL. La transformación y carga al warehouse analítico no forman parte de esta actividad.

## Arquitectura y flujo

1. `etl-api` (.NET 8) lee comentarios activos de `ComentariosSociales` y los expone en `GET /api/comments`.
2. `etl-extraction` (.NET 8 Worker Service) ejecuta tres adaptadores: encuestas CSV, reseñas de SQL Server y comentarios mediante REST.
3. Cada adaptador escribe registros normalizados en su propio JSON Lines; el archivo parcial se publica al completar la fuente.
4. El Worker inicia un ciclo al arrancar y repite la extracción cada `Extraction:IntervalMinutes` (predeterminado: 1 minuto). Detén el proceso con Ctrl+C.
5. La siguiente etapa podrá transformar esos archivos y cargar `SistemaBDAnalitica`; esa carga queda fuera del alcance.

## Decisiones de diseño y calidad

- **Polimorfismo y Open/Closed:** `IExtractor` permite que el Worker recorra adaptadores concretos sin conocer su implementación; una nueva fuente se agrega como adaptador y registro de DI.
- **Responsabilidad única:** `CsvExtractor`, `DatabaseExtractor`, `ApiExtractor`, `JsonLinesStagingWriter` y el API mantienen tareas diferenciadas.
- **Rendimiento:** lecturas asíncronas; CSV y SQL procesados en flujo; respuesta HTTP procesada desde stream; fuentes independientes concurrentes; medición con `Stopwatch` y logs por fuente.
- **Escalabilidad:** adaptadores desacoplados y JSON canónico permiten sumar formatos; la concurrencia se limita a las tres fuentes configuradas.
- **Seguridad:** no se incluyen secretos en el repositorio; conexiones en User Secrets o variables protegidas; HTTPS y bearer token fuera de Development; no se registran credenciales.
- **Mantenibilidad:** .NET 8, interfaces, adaptadores, opciones de configuración, DI e `ILogger` estructurado.

## Evidencia del código

Fragmentos fieles a los archivos del repositorio; el PDF incluye estas evidencias junto con arquitectura y flujo.

`etl-extraction/Extraction/IExtractor.cs` define el contrato polimórfico:

```csharp
public interface IExtractor
{
    string SourceName { get; }
    bool IsEnabled { get; }
    IAsyncEnumerable<ExtractedOpinion> ExtractAsync(CancellationToken cancellationToken);
}
```

`etl-extraction/Program.cs` registra cada adaptador e `HttpClientFactory`:

```csharp
builder.Services.AddSingleton<IExtractor, CsvExtractor>();
builder.Services.AddSingleton<IExtractor, DatabaseExtractor>();
builder.Services.AddHttpClient<ApiExtractor>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(60);
});
```

`etl-extraction/ExtractionWorker.cs` ejecuta las fuentes habilitadas en paralelo, mide tiempo y repite por intervalo:

```csharp
var results = await Task.WhenAll(
    enabled.Select(x => ExtractOneAsync(x, runId, stoppingToken)));
logger.LogInformation("Fuente {Source}: {Count} filas en {ElapsedMs} ms",
    extractor.SourceName, count, timer.ElapsedMilliseconds);
await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
```

`etl-extraction/Extraction/DatabaseExtractor.cs` consulta SQL de forma asíncrona con `SqlDataReader`; `ApiExtractor.cs` usa `HttpClient` y deserializa JSON desde el stream HTTP; `Staging/JsonLinesStagingWriter.cs` escribe `.partial` y renombra al completar. `etl-api/Program.cs` implementa `GET /api/comments` consultando comentarios sociales activos.

## Ejecución local

Necesitas .NET 8 SDK y SQL Server con la base poblada mediante el proceso anterior. Configura conexiones separadas en User Secrets para ambos proyectos:

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

El Worker llama la API local en `http://localhost:5080/api/comments`; fuera de loopback requiere HTTPS y configuración de token. La configuración define `Extraction:IntervalMinutes` y `Extraction:StagingDirectory`.

## Validación y límites

Los proyectos API y Worker compilan con .NET 8. La extracción CSV puede ejecutarse sin SQL; para validar las tres fuentes se requiere SQL Server con reseñas y comentarios cargados. No se afirma una prueba de integración contra una instancia SQL real. El Worker genera archivos JSONL; no transforma ni carga el warehouse.
