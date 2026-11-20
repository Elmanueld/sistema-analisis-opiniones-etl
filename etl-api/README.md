# API de comentarios sociales

API REST .NET 8 que expone comentarios activos de `SistemaBDTransaccional.dbo.ComentariosSociales` para el Worker. `GET /api/comments` devuelve `idComment`, `idClient`, `idProduct`, `channel`, `date`, `comment` y `engagement`; `GET /health` comprueba disponibilidad.

## Ejecutar localmente

Requiere .NET 8 y SQL Server con la base/datos cargados. En entorno Development, la ruta local no exige bearer token.

```powershell
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:OperationalDb" "Server=.\SQLEXPRESS;Database=SistemaBDTransaccional;Integrated Security=True;Encrypt=True;TrustServerCertificate=True"
dotnet run --urls http://localhost:5080
```

## Despliegue

Usa HTTPS, establece `ConnectionStrings__OperationalDb` y `Api__BearerToken` en el gestor de secretos del entorno, y configura el mismo token como `Sources:Api:BearerToken` en el Worker. En ambientes distintos de Development, la API exige bearer token. No guardes secretos en el repositorio.
