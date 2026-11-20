# Worker de extracción de opiniones

Worker Service en .NET 8 para extraer únicamente las fuentes de esta práctica: encuesta interna desde `DatosCSV/surveys_part1.csv`, reseñas web publicadas directamente desde `SistemaBDTransaccional.dbo.ReseniasWeb` y comentarios sociales desde `GET /api/comments` de la API propia (`etl-api`). La API consulta `dbo.ComentariosSociales`.

## Requisitos y configuración

- .NET 8 SDK.
- SQL Server con `SistemaBDTransaccional` y reseñas/comentarios cargados.
- Ejecutar `etl-api` para exponer los comentarios sociales.
- Guardar cadenas de conexión mediante User Secrets o variables protegidas; no guardar credenciales en `appsettings.json`.

El Worker llama la API local en `http://localhost:5080/api/comments`; fuera de loopback usa HTTPS. `Extraction:IntervalMinutes` controla la espera entre ciclos (predeterminado: 1 minuto). El Worker sigue ejecutándose hasta detenerlo con Ctrl+C.

## Ejecutar

Primero, en una terminal:

```powershell
cd etl-api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OperationalDb" "Server=.\SQLEXPRESS;Database=SistemaBDTransaccional;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet run --urls http://localhost:5080
```

Después, en otra terminal:

```powershell
cd etl-extraction
dotnet user-secrets init
dotnet user-secrets set "Sources:Database:ConnectionString" "Server=.\SQLEXPRESS;Database=SistemaBDTransaccional;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet run
```

La configuración habilita las tres fuentes. Cada ciclo escribe JSONL independiente en staging. La carga de dimensiones y hechos OLAP queda para la etapa posterior.

## Diseño

`IExtractor` abstrae la extracción. `CsvExtractor`, `DatabaseExtractor` y `ApiExtractor` aplican polimorfismo y se registran con inyección de dependencias; se puede incorporar un adaptador sin cambiar el flujo central (Open/Closed). `JsonLinesStagingWriter` separa la publicación de archivos. `ILogger` registra resultados, tiempos y errores por fuente.
