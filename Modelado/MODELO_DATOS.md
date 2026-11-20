# Modelo OLAP del Sistema de Opiniones

La actividad pide diseñar una base analítica tipo estrella para recibir opiniones procesadas por ETL. El modelo propuesto conserva la base `SistemaBDTransaccional` como fuente OLTP y crea `SistemaBDAnalitica` para análisis; no modifica ni reemplaza el sistema operativo.

## Estructura del modelo

El grano de `DW.FactOpiniones` es una fila por opinión aceptada y cargada. La tabla guarda una unidad por opinión (`CantidadOpiniones = 1`), la nota de satisfacción cuando la fuente la provee, y métricas de interacción de redes y reseñas. Las seis dimensiones describen fecha, producto, cliente, fuente/canal, sentimiento y evento.

Se eligió una estrella en lugar de un copo de nieve porque la actividad prioriza consultas de agregación por periodo, producto y canal. Agrupar los atributos descriptivos directamente alrededor de los hechos reduce uniones. Por eso `DimProducto` almacena también la categoría; en OLTP, categoría está normalizada aparte. La separación normalizada del OLTP sigue siendo adecuada para insertar y mantener transacciones.

`DimSentimiento` permite comparar las tres fuentes bajo valores comunes positivo, neutral y negativo. La clave 0 representa «sin clasificar» o «desconocido» para no perder hechos cuando falta una clasificación o una dimensión. En reportes, las opiniones sin sentimiento se cuentan aparte.

| Tabla | Propósito |
|---|---|
| `DW.FactOpiniones` | Hechos: conteo, puntaje, likes y votos de utilidad; una fila por opinión. |
| `DW.DimFecha` | Día, semana, mes, trimestre y año para tendencias temporales. |
| `DW.DimProducto` | Producto y categoría para volumen, notas y sentimiento por producto. |
| `DW.DimCliente` | Identificador y atributos de segmento para agrupar opiniones por cliente. |
| `DW.DimFuente` | Encuesta, reseña web o red social y el tipo/nombre concreto de la fuente. |
| `DW.DimSentimiento` | Clasificación normalizada positiva, neutral, negativa o desconocida. |
| `DW.DimEvento` | Campaña o lanzamiento asociado; desconocido cuando la fuente no informa uno. |

Cada dimensión tiene relación 1:N con `FactOpiniones`, protegida por claves foráneas. Las claves sustitutas del warehouse aíslan las relaciones analíticas de los identificadores OLTP y facilitan mantener dimensiones desconocidas.

## Criterios de análisis

`CantidadOpiniones` se suma para contar opiniones cargadas. El promedio de satisfacción y su porcentaje equivalente (`promedio / 5 × 100`) usan solo registros con puntuación numérica; comentarios sociales sin nota no entran en ese promedio. Los porcentajes de sentimientos solicitados se calculan sobre el total de comentarios y se reporta también el total sin clasificar, de modo que los faltantes sean visibles. Para el canal con más negatividad se muestra el porcentaje negativo del total del canal y, como referencia, el porcentaje entre sentimientos clasificados.

La dimensión fecha permite cortes por mes y trimestre, tanto generales como por producto. El análisis de palabras clave es opcional; no se almacena texto libre en el modelo propuesto y para añadirlo se necesitaría retener el comentario y procesarlo con NLP o una tabla separada de términos.

## Mapeo de fuentes al warehouse

| Fuente OLTP | Destino | Carga propuesta |
|---|---|---|
| `EncuestasInternas` | `FactOpiniones` | `IdOpinion` como identificador de origen; puntaje y sentimiento existente. Canal encuesta. |
| `ReseniasWeb` | `FactOpiniones` | `IdReview` como origen; `Rating` como puntaje. Para sentimiento se puede acordar la regla 1–2 negativo, 3 neutral, 4–5 positivo. |
| `ComentariosSociales` | `FactOpiniones` | `IdComment` como origen; nota nula; likes/retweets. Asignar sentimiento mediante NLP o regla definida. |
| `Productos`, `Categoria` | `DimProducto` | ID, nombre y categoría del producto. |
| `Clientes` | `DimCliente` | ID del cliente; mapear atributos de segmento si existen en la fuente. |
| `FuenteDatos`, `TipoFuente` | `DimFuente` | Normalizar los registros como encuesta, reseña web o red social y guardar tipo/nombre. |
| Fechas de las opiniones | `DimFecha` | Generar las fechas de calendario para el rango de datos. |
| Catálogo de campañas/lanzamientos | `DimEvento` | Cargar eventos cuando se incorpore una fuente; usar desconocido en caso contrario. |

## Compatibilidad y límites de los datos actuales

Las tablas y fuentes principales del OLTP son compatibles con las dimensiones y la tabla de hechos. Sin embargo, el programa ETL actual solo carga `SistemaBDTransaccional`; todavía no tiene una etapa que cargue dimensiones y hechos en el warehouse. Por tanto, el modelo no se poblará automáticamente hasta añadir esa etapa.

El reporte de la última ejecución incluida registra 1,706 filas procesadas, 1,212 duplicados omitidos, 494 rechazos y 0 inserciones en esa ejecución. Los rechazos incluyen encuestas cuyos identificadores de cliente/producto no encuentran correspondencia en los CSV maestros (por ejemplo, el primer registro usa cliente 8537 mientras `clients.csv` muestra claves como 1). Si el warehouse se alimenta solo desde el OLTP, esas opiniones rechazadas no llegarán a los hechos. Para conservarlas habría que corregir la correspondencia de IDs o cargar desde una zona de preparación con claves desconocidas, reconociendo que se pierde análisis por cliente/producto para esos registros.

La base actual no ofrece país, edad o tipo de cliente ni un catálogo de eventos; esas columnas de dimensión quedan vacías hasta añadir esas fuentes. La clasificación de sentimiento existente se usa en encuestas; para comparar web y redes, la futura carga debe aplicar una regla de sentimiento a ratings y clasificar texto social. Estos límites son de disponibilidad de datos y de integración ETL, no de las claves o relaciones del modelo.
