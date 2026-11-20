# Worker de extracción de opiniones

Worker Service en .NET 8. Extrae únicamente las tres fuentes indicadas por la práctica: encuesta interna desde `DatosCSV/surveys_part1.csv`, reseñas web publicadas directamente desde `SistemaBDTransaccional.dbo.ReseniasWeb` y comentarios sociales desde la API propia del proyecto (`GET /api/comments`). La API lee `dbo.ComentariosSociales`.

## Requisitos y configuración

- .NET 8 SDK.
- SQL Server con `SistemaBDTransaccional` y las reseñas/comentarios cargados en la práctica anterior.
- Ejecutar también el proyecto `etl-api` para exponer los comentarios sociales.
- Guardar cadenas de conexión mediante User Secrets o variables protegidas. No incluir credenciales en `appsettings.json`.

La consulta relacional predeterminada filtra reseñas publicadas (`Estado = 1`). El Worker usa `http://localhost:5080/api/comments` en desarrollo. En despliegue configura HTTPS.

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

La API tiene `GET /health` y `GET /api/comments`. El Worker habilita las tres fuentes y escribe staging JSONL por fuente; no transforma ni carga dimensiones/hechos OLAP. Si SQL Server no está disponible, la fuente respectiva falla y se reporta en el log.

## Diseño

`IExtractor` abstrae el proceso de extracción. `CsvExtractor`, `DatabaseExtractor` y `ApiExtractor` aplican polimorfismo para sus fuentes; se registran por inyección de dependencias. `JsonLinesStagingWriter` mantiene separada la escritura. Para agregar una fuente se implementa y registra un adaptador sin cambiar la lógica central del Worker (Open/Closed).
