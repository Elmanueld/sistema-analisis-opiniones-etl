# Carga del Data Warehouse - Sistema de Opiniones

## Descripción

Proceso ETL en T-SQL que carga `SistemaBDAnalitica` desde `SistemaBDTransaccional` en la instancia SQL Server. Actualiza/inserta dimensiones, después agrega hechos que no existan por clave natural. Se ejecuta en una transacción y es idempotente.

## Ejecución

En una terminal ubicada en la raíz del repositorio, con SQL Server disponible y permisos de creación de base/tablas:

```powershell
sqlcmd -C -I -b -S .\SQLEXPRESS -E -i Modelado\01_SistemaBDAnalitica.sql
sqlcmd -C -I -b -S .\SQLEXPRESS -E -i Modelado\02_CargarDataWarehouse.sql
sqlcmd -C -I -b -S .\SQLEXPRESS -E -i Modelado\03_ValidarCargaDataWarehouse.sql
```

`-E` usa autenticación integrada; cambia el servidor con `-S` si tu instancia tiene otro nombre. `-I` activa QUOTED_IDENTIFIER para índices filtrados. Los scripts pueden volver a ejecutarse sin duplicar registros.

## Reglas de transformación

- `DimProducto`: ID, nombre y categoría del OLTP.
- `DimCliente`: identificador OLTP; país, grupo de edad y tipo quedan vacíos porque el origen no los contiene.
- `DimFuente`: encuesta, reseña web y cada canal social.
- `DimFecha`: calendario de las fechas usadas por las opiniones.
- `DimSentimiento`: catálogo común positivo, neutral, negativo y sin clasificar. Para ratings web 1-2 = negativo, 3 = neutral, 4-5 = positivo. Encuestas usan su clasificación original; redes quedan sin clasificar porque no tienen etiqueta en el OLTP.
- `DimEvento`: solo tiene el miembro desconocido porque no hay catálogo de eventos/campañas.
- `FactOpiniones`: una fila por opinión activa de las tres fuentes, clave fuente/ID origen, puntaje si existe, likes y votos de utilidad.
- Claves faltantes se dirigen al miembro desconocido (`Key = 0`).

## Resultado verificado en SQL Server local

La carga insertó 406 opiniones: 6 encuestas, 200 reseñas web y 200 comentarios sociales (Facebook 68, Instagram 77 y Twitter 55). El warehouse contiene 203 fechas (incluyendo el desconocido), 201 productos, 501 clientes, 6 fuentes, 4 sentimientos, 1 evento y 406 filas de hechos. La segunda ejecución conservó los conteos. Las cuatro revisiones de FK y la detección de duplicados devolvieron cero.

Ver salida reproducible en `Evidencias/Resultado_Carga_DataWarehouse.txt` y el resumen de proceso, resultados y enlace GitHub en `Informe_Carga_DataWarehouse.pdf`.
