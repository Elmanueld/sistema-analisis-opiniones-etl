@echo off
setlocal
cd /d "%~dp0"
start "API Comentarios" /D "%~dp0etl-api" cmd /k "dotnet run --urls http://localhost:5080"
for /L %%i in (1,1,30) do (
  curl --silent --fail http://localhost:5080/health >nul
  if not errorlevel 1 goto api_ready
  timeout /t 1 /nobreak >nul
)
:api_ready
cd /d "%~dp0etl-extraction"
dotnet run --project Opiniones.ExtractionWorker.csproj
exit /b %errorlevel%
