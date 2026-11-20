USE SistemaBDAnalitica;
GO

-- 1. Total de opiniones que ya fueron cargadas en el warehouse.
SELECT SUM(CONVERT(BIGINT,CantidadOpiniones)) AS TotalOpinionesCargadas
FROM DW.FactOpiniones;

-- 2. Promedio general y porcentaje de satisfacción (solo opiniones con nota 1-5).
SELECT AVG(PuntajeSatisfaccion) AS PromedioSatisfaccion_1a5,
       AVG(PuntajeSatisfaccion)/5.0*100 AS PorcentajeSatisfaccion
FROM DW.FactOpiniones
WHERE PuntajeSatisfaccion IS NOT NULL;

-- 3. Cantidad y porcentaje de cada sentimiento respecto del total de comentarios.
-- Las opiniones UNKNOWN se muestran aparte para que los porcentajes no oculten faltantes.
SELECT SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalComentarios,
       SUM(CASE WHEN s.Codigo=N'POS' THEN CONVERT(BIGINT,f.CantidadOpiniones) ELSE 0 END) AS Positivas,
       100.0*SUM(CASE WHEN s.Codigo=N'POS' THEN f.CantidadOpiniones ELSE 0 END)
         / NULLIF(SUM(CONVERT(BIGINT,f.CantidadOpiniones)),0) AS PorcentajePositivasDelTotal,
       SUM(CASE WHEN s.Codigo=N'NEG' THEN CONVERT(BIGINT,f.CantidadOpiniones) ELSE 0 END) AS Negativas,
       100.0*SUM(CASE WHEN s.Codigo=N'NEG' THEN f.CantidadOpiniones ELSE 0 END)
         / NULLIF(SUM(CONVERT(BIGINT,f.CantidadOpiniones)),0) AS PorcentajeNegativasDelTotal,
       SUM(CASE WHEN s.Codigo=N'NEU' THEN CONVERT(BIGINT,f.CantidadOpiniones) ELSE 0 END) AS Neutrales,
       100.0*SUM(CASE WHEN s.Codigo=N'NEU' THEN f.CantidadOpiniones ELSE 0 END)
         / NULLIF(SUM(CONVERT(BIGINT,f.CantidadOpiniones)),0) AS PorcentajeNeutralesDelTotal,
       SUM(CASE WHEN s.Codigo=N'UNKNOWN' THEN CONVERT(BIGINT,f.CantidadOpiniones) ELSE 0 END) AS SinClasificar
FROM DW.FactOpiniones f
JOIN DW.DimSentimiento s ON s.SentimientoKey=f.SentimientoKey;

-- 4. Opiniones por canal/fuente y promedio de satisfacción con nota.
SELECT fu.Canal,fu.TipoFuente,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalOpiniones,
       AVG(f.PuntajeSatisfaccion) AS PromedioSatisfaccion
FROM DW.FactOpiniones f
JOIN DW.DimFuente fu ON fu.FuenteKey=f.FuenteKey
GROUP BY fu.Canal,fu.TipoFuente
ORDER BY TotalOpiniones DESC;

-- 5. Producto con mayor volumen de comentarios.
SELECT TOP (1) p.NombreProducto,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalOpiniones
FROM DW.FactOpiniones f
JOIN DW.DimProducto p ON p.ProductoKey=f.ProductoKey
GROUP BY p.NombreProducto
ORDER BY TotalOpiniones DESC;

-- 6. Productos ordenados por mejor promedio de calificación (solo notas disponibles).
SELECT p.NombreProducto,AVG(f.PuntajeSatisfaccion) AS PromedioCalificacion,
       AVG(f.PuntajeSatisfaccion)/5.0*100 AS PorcentajeSatisfaccion
FROM DW.FactOpiniones f
JOIN DW.DimProducto p ON p.ProductoKey=f.ProductoKey
WHERE f.PuntajeSatisfaccion IS NOT NULL
GROUP BY p.NombreProducto
ORDER BY PromedioCalificacion DESC;

-- 7. Productos con más opiniones negativas.
SELECT p.NombreProducto,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalNegativas
FROM DW.FactOpiniones f
JOIN DW.DimProducto p ON p.ProductoKey=f.ProductoKey
JOIN DW.DimSentimiento s ON s.SentimientoKey=f.SentimientoKey
WHERE s.Codigo=N'NEG'
GROUP BY p.NombreProducto
ORDER BY TotalNegativas DESC;

-- 8. Evolución general mes a mes.
SELECT d.Anio,d.Mes,d.NombreMes,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalOpiniones,
       AVG(f.PuntajeSatisfaccion) AS PromedioSatisfaccion,
       AVG(f.PuntajeSatisfaccion)/5.0*100 AS PorcentajeSatisfaccion
FROM DW.FactOpiniones f
JOIN DW.DimFecha d ON d.FechaKey=f.FechaKey
WHERE d.FechaKey<>0
GROUP BY d.Anio,d.Mes,d.NombreMes
ORDER BY d.Anio,d.Mes;

-- 9. Evolución general trimestre a trimestre.
SELECT d.Anio,d.Trimestre,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalOpiniones,
       AVG(f.PuntajeSatisfaccion) AS PromedioSatisfaccion,
       AVG(f.PuntajeSatisfaccion)/5.0*100 AS PorcentajeSatisfaccion
FROM DW.FactOpiniones f
JOIN DW.DimFecha d ON d.FechaKey=f.FechaKey
WHERE d.FechaKey<>0
GROUP BY d.Anio,d.Trimestre
ORDER BY d.Anio,d.Trimestre;

-- 10. Evolución mensual por producto.
SELECT d.Anio,d.Mes,d.NombreMes,p.NombreProducto,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalOpiniones,
       AVG(f.PuntajeSatisfaccion) AS PromedioSatisfaccion
FROM DW.FactOpiniones f
JOIN DW.DimFecha d ON d.FechaKey=f.FechaKey
JOIN DW.DimProducto p ON p.ProductoKey=f.ProductoKey
WHERE d.FechaKey<>0
GROUP BY d.Anio,d.Mes,d.NombreMes,p.NombreProducto
ORDER BY d.Anio,d.Mes,p.NombreProducto;

-- 11. Clientes con más opiniones y sus segmentos (requiere esos atributos en origen).
SELECT c.ClienteKey,c.Pais,c.GrupoEdad,c.TipoCliente,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalOpiniones
FROM DW.FactOpiniones f
JOIN DW.DimCliente c ON c.ClienteKey=f.ClienteKey
WHERE c.ClienteKey<>0
GROUP BY c.ClienteKey,c.Pais,c.GrupoEdad,c.TipoCliente
ORDER BY TotalOpiniones DESC;

-- 12. Canales ordenados por proporción de opiniones negativas.
-- Se muestra tanto el porcentaje sobre todo el canal como sobre sentimientos clasificados.
WITH PorCanal AS (
    SELECT fu.Canal,fu.TipoFuente,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalCanal,
       SUM(CASE WHEN s.Codigo=N'NEG' THEN CONVERT(BIGINT,f.CantidadOpiniones) ELSE 0 END) AS Negativas,
       SUM(CASE WHEN s.Codigo<>N'UNKNOWN' THEN CONVERT(BIGINT,f.CantidadOpiniones) ELSE 0 END) AS TotalClasificado
    FROM DW.FactOpiniones f
    JOIN DW.DimFuente fu ON fu.FuenteKey=f.FuenteKey
    JOIN DW.DimSentimiento s ON s.SentimientoKey=f.SentimientoKey
    GROUP BY fu.Canal,fu.TipoFuente
)
SELECT Canal,TipoFuente,TotalCanal,Negativas,
       100.0*Negativas/NULLIF(TotalCanal,0) AS PorcentajeNegativoDelCanal,
       100.0*Negativas/NULLIF(TotalClasificado,0) AS PorcentajeNegativoClasificados
FROM PorCanal
ORDER BY PorcentajeNegativoDelCanal DESC;

-- 13. Eventos asociados a más comentarios (si se cargó una fuente de eventos).
SELECT e.NombreEvento,e.TipoEvento,
       SUM(CONVERT(BIGINT,f.CantidadOpiniones)) AS TotalOpiniones
FROM DW.FactOpiniones f
JOIN DW.DimEvento e ON e.EventoKey=f.EventoKey
WHERE f.EventoKey<>0
GROUP BY e.NombreEvento,e.TipoEvento
ORDER BY TotalOpiniones DESC;

-- El análisis de palabras clave es opcional. El modelo no guarda comentario libre;
-- requiere texto autorizado y una etapa NLP o una tabla de términos relacionada.
GO
