@echo off
setlocal
set "APP_DIR=%~dp0Distribucion"
if not exist "%APP_DIR%\ProgramaETL.UI.exe" (
  echo No se encontro la aplicacion. Revisa la carpeta Distribucion.
  pause
  exit /b 1
)
pushd "%APP_DIR%"
start "ETL de opiniones" "%APP_DIR%\ProgramaETL.UI.exe"
popd
endlocal
