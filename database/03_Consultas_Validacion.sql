USE [SistemaBDTransaccional];
GO

/* 1. Conteo de filas de las tablas pobladas por el ETL. */
SELECT 'Clientes' AS Tabla, COUNT_BIG(*) AS Registros FROM dbo.Clientes
UNION ALL SELECT 'Productos', COUNT_BIG(*) FROM dbo.Productos
UNION ALL SELECT 'FuenteDatos', COUNT_BIG(*) FROM dbo.FuenteDatos
UNION ALL SELECT 'ComentariosSociales', COUNT_BIG(*) FROM dbo.ComentariosSociales
UNION ALL SELECT 'EncuestasInternas', COUNT_BIG(*) FROM dbo.EncuestasInternas
UNION ALL SELECT 'ReseniasWeb', COUNT_BIG(*) FROM dbo.ReseniasWeb;
GO

/* 2. Integridad referencial: todos los conteos deben ser cero. */
SELECT 'Productos -> Categoria' AS Relacion, COUNT_BIG(*) AS Huerfanos
FROM dbo.Productos p LEFT JOIN dbo.Categoria c ON c.IdCategoria=p.IdCategoria WHERE c.IdCategoria IS NULL
UNION ALL SELECT 'FuenteDatos -> TipoFuente', COUNT_BIG(*)
FROM dbo.FuenteDatos f LEFT JOIN dbo.TipoFuente t ON t.IdTipoFuente=f.IdTipoFuente WHERE t.IdTipoFuente IS NULL
UNION ALL SELECT 'ComentariosSociales -> Clientes', COUNT_BIG(*)
FROM dbo.ComentariosSociales o LEFT JOIN dbo.Clientes c ON c.IdCliente=o.IdCliente WHERE o.IdCliente IS NOT NULL AND c.IdCliente IS NULL
UNION ALL SELECT 'ComentariosSociales -> Productos', COUNT_BIG(*)
FROM dbo.ComentariosSociales o LEFT JOIN dbo.Productos p ON p.IdProducto=o.IdProducto WHERE p.IdProducto IS NULL
UNION ALL SELECT 'ComentariosSociales -> FuenteDatos', COUNT_BIG(*)
FROM dbo.ComentariosSociales o LEFT JOIN dbo.FuenteDatos f ON f.IdFuente=o.IdFuente WHERE f.IdFuente IS NULL
UNION ALL SELECT 'EncuestasInternas -> Clientes', COUNT_BIG(*)
FROM dbo.EncuestasInternas o LEFT JOIN dbo.Clientes c ON c.IdCliente=o.IdCliente WHERE c.IdCliente IS NULL
UNION ALL SELECT 'EncuestasInternas -> Productos', COUNT_BIG(*)
FROM dbo.EncuestasInternas o LEFT JOIN dbo.Productos p ON p.IdProducto=o.IdProducto WHERE p.IdProducto IS NULL
UNION ALL SELECT 'EncuestasInternas -> FuenteDatos', COUNT_BIG(*)
FROM dbo.EncuestasInternas o LEFT JOIN dbo.FuenteDatos f ON f.IdFuente=o.IdFuente WHERE f.IdFuente IS NULL
UNION ALL SELECT 'EncuestasInternas -> Clasificacion', COUNT_BIG(*)
FROM dbo.EncuestasInternas o LEFT JOIN dbo.Clasificacion c ON c.IdClasificacion=o.IdClasificacion WHERE o.IdClasificacion IS NOT NULL AND c.IdClasificacion IS NULL
UNION ALL SELECT 'ReseniasWeb -> Clientes', COUNT_BIG(*)
FROM dbo.ReseniasWeb o LEFT JOIN dbo.Clientes c ON c.IdCliente=o.IdCliente WHERE c.IdCliente IS NULL
UNION ALL SELECT 'ReseniasWeb -> Productos', COUNT_BIG(*)
FROM dbo.ReseniasWeb o LEFT JOIN dbo.Productos p ON p.IdProducto=o.IdProducto WHERE p.IdProducto IS NULL
UNION ALL SELECT 'ReseniasWeb -> FuenteDatos', COUNT_BIG(*)
FROM dbo.ReseniasWeb o LEFT JOIN dbo.FuenteDatos f ON f.IdFuente=o.IdFuente WHERE f.IdFuente IS NULL;
GO

/* 3. Tipos, rangos y campos obligatorios de las opiniones. Esta consulta debe devolver cero. */
SELECT 'Rating web fuera de 1..5' AS Validacion, COUNT_BIG(*) AS Errores
FROM dbo.ReseniasWeb WHERE Rating NOT BETWEEN 1 AND 5
UNION ALL SELECT 'Puntaje encuesta fuera de 1..5', COUNT_BIG(*)
FROM dbo.EncuestasInternas WHERE PuntajeSatisfaccion IS NOT NULL AND PuntajeSatisfaccion NOT BETWEEN 1 AND 5
UNION ALL SELECT 'Comentario social vacío', COUNT_BIG(*)
FROM dbo.ComentariosSociales WHERE NULLIF(LTRIM(RTRIM(Comentario)),'') IS NULL
UNION ALL SELECT 'Cliente sin email válido', COUNT_BIG(*)
FROM dbo.Clientes WHERE Email NOT LIKE '%@%.%';
GO

/* 4. Duplicados de claves naturales: las consultas deben devolver cero filas. */
SELECT 'Clientes.Email' AS Clave, Email AS Valor, COUNT_BIG(*) AS Repeticiones
FROM dbo.Clientes GROUP BY Email HAVING COUNT_BIG(*)>1
UNION ALL
SELECT 'Productos.Nombre+Categoria', CONCAT(p.Nombre,'|',p.IdCategoria), COUNT_BIG(*)
FROM dbo.Productos p GROUP BY p.Nombre,p.IdCategoria HAVING COUNT_BIG(*)>1;
GO

/* 5. Resumen consolidado por tipo de opinión y muestra de la vista analítica. */
SELECT TipoOpinion, COUNT_BIG(*) AS Total, AVG(CONVERT(DECIMAL(10,2),Puntaje)) AS PuntajePromedio
FROM dbo.vw_OpinionesUnificadas GROUP BY TipoOpinion ORDER BY TipoOpinion;
SELECT TOP (20) IdProducto,Producto,Categoria,TotalOpiniones,ReseniasWeb,EncuestasInternas,ComentariosSociales,PuntajePromedio
FROM dbo.vw_ResumenOpinionesPorProducto ORDER BY TotalOpiniones DESC,Producto;
GO

/* 6. Procedimiento solicitado para mostrar conteos por tabla. */
EXEC dbo.sp_ResumenCargaETL;
GO
