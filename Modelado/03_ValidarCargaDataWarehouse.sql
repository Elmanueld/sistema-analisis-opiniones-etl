USE SistemaBDAnalitica;
SET NOCOUNT ON;
SELECT 'DimFecha' Tabla, COUNT_BIG(*) Filas FROM DW.DimFecha
UNION ALL SELECT 'DimProducto',COUNT_BIG(*) FROM DW.DimProducto
UNION ALL SELECT 'DimCliente',COUNT_BIG(*) FROM DW.DimCliente
UNION ALL SELECT 'DimFuente',COUNT_BIG(*) FROM DW.DimFuente
UNION ALL SELECT 'DimSentimiento',COUNT_BIG(*) FROM DW.DimSentimiento
UNION ALL SELECT 'DimEvento',COUNT_BIG(*) FROM DW.DimEvento
UNION ALL SELECT 'FactOpiniones',COUNT_BIG(*) FROM DW.FactOpiniones;
SELECT d.Canal, d.TipoFuente, d.NombreFuente, COUNT(f.OpinionKey) Opiniones
FROM DW.DimFuente d LEFT JOIN DW.FactOpiniones f ON f.FuenteKey=d.FuenteKey
GROUP BY d.Canal,d.TipoFuente,d.NombreFuente ORDER BY d.Canal,d.TipoFuente,d.NombreFuente;
SELECT s.Nombre Sentimiento, COUNT(f.OpinionKey) Opiniones
FROM DW.DimSentimiento s LEFT JOIN DW.FactOpiniones f ON f.SentimientoKey=s.SentimientoKey
GROUP BY s.Nombre ORDER BY s.Nombre;
SELECT MIN(Fecha) FechaInicial, MAX(Fecha) FechaFinal, COUNT(*) DiasCargados FROM DW.DimFecha WHERE FechaKey<>0;
SELECT COUNT_BIG(*) HechosSinFecha FROM DW.FactOpiniones f LEFT JOIN DW.DimFecha d ON d.FechaKey=f.FechaKey WHERE d.FechaKey IS NULL;
SELECT COUNT_BIG(*) HechosSinProducto FROM DW.FactOpiniones f LEFT JOIN DW.DimProducto d ON d.ProductoKey=f.ProductoKey WHERE d.ProductoKey IS NULL;
SELECT COUNT_BIG(*) HechosSinCliente FROM DW.FactOpiniones f LEFT JOIN DW.DimCliente d ON d.ClienteKey=f.ClienteKey WHERE d.ClienteKey IS NULL;
SELECT COUNT_BIG(*) HechosSinFuente FROM DW.FactOpiniones f LEFT JOIN DW.DimFuente d ON d.FuenteKey=f.FuenteKey WHERE d.FuenteKey IS NULL;
SELECT COUNT_BIG(*) HechosDuplicados FROM (SELECT FuenteKey,IdOpinionOrigen,COUNT(*) n FROM DW.FactOpiniones GROUP BY FuenteKey,IdOpinionOrigen HAVING COUNT(*)>1) d;
GO

