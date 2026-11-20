USE [SistemaBDTransaccional];
GO

CREATE OR ALTER VIEW dbo.vw_OpinionesUnificadas
AS
    SELECT CAST('ReseniaWeb' AS VARCHAR(30)) AS TipoOpinion,
           r.IdReview AS IdRegistro, r.IdCliente, r.IdProducto, r.IdFuente,
           r.FechaResenia AS FechaOpinion, r.Comentario, r.Rating AS Puntaje,
           CAST(NULL AS VARCHAR(50)) AS Clasificacion
    FROM dbo.ReseniasWeb AS r
    UNION ALL
    SELECT CAST('EncuestaInterna' AS VARCHAR(30)), e.IdOpinion, e.IdCliente, e.IdProducto, e.IdFuente,
           e.FechaOpinion, e.Comentario, e.PuntajeSatisfaccion, c.Nombre
    FROM dbo.EncuestasInternas AS e
    LEFT JOIN dbo.Clasificacion AS c ON c.IdClasificacion=e.IdClasificacion
    UNION ALL
    SELECT CAST('ComentarioSocial' AS VARCHAR(30)), s.IdComment, s.IdCliente, s.IdProducto, s.IdFuente,
           s.FechaComentario, s.Comentario, CAST(NULL AS INT), CAST(NULL AS VARCHAR(50))
    FROM dbo.ComentariosSociales AS s;
GO

CREATE OR ALTER VIEW dbo.vw_ResumenOpinionesPorProducto
AS
    SELECT p.IdProducto, p.Nombre AS Producto, c.Nombre AS Categoria,
           COUNT_BIG(o.IdRegistro) AS TotalOpiniones,
           SUM(CASE WHEN o.TipoOpinion='ReseniaWeb' THEN CONVERT(BIGINT,1) ELSE CONVERT(BIGINT,0) END) AS ReseniasWeb,
           SUM(CASE WHEN o.TipoOpinion='EncuestaInterna' THEN CONVERT(BIGINT,1) ELSE CONVERT(BIGINT,0) END) AS EncuestasInternas,
           SUM(CASE WHEN o.TipoOpinion='ComentarioSocial' THEN CONVERT(BIGINT,1) ELSE CONVERT(BIGINT,0) END) AS ComentariosSociales,
           AVG(CONVERT(DECIMAL(10,2),o.Puntaje)) AS PuntajePromedio
    FROM dbo.Productos AS p
    LEFT JOIN dbo.Categoria AS c ON c.IdCategoria=p.IdCategoria
    LEFT JOIN dbo.vw_OpinionesUnificadas AS o ON o.IdProducto=p.IdProducto
    GROUP BY p.IdProducto,p.Nombre,c.Nombre;
GO

CREATE OR ALTER PROCEDURE dbo.sp_ResumenCargaETL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT 'Clientes' AS Tabla, COUNT_BIG(*) AS Registros FROM dbo.Clientes
    UNION ALL SELECT 'Productos', COUNT_BIG(*) FROM dbo.Productos
    UNION ALL SELECT 'FuenteDatos', COUNT_BIG(*) FROM dbo.FuenteDatos
    UNION ALL SELECT 'ComentariosSociales', COUNT_BIG(*) FROM dbo.ComentariosSociales
    UNION ALL SELECT 'EncuestasInternas', COUNT_BIG(*) FROM dbo.EncuestasInternas
    UNION ALL SELECT 'ReseniasWeb', COUNT_BIG(*) FROM dbo.ReseniasWeb;
END;
GO
