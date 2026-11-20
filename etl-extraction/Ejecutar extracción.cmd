@echo off
setlocal
cd /d "%~dp0"
dotnet run --project Opiniones.ExtractionWorker.csproj
exit /b %errorlevel%
