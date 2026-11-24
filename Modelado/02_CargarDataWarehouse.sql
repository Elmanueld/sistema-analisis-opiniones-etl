/*
  Carga idempotente desde SistemaBDTransaccional hacia SistemaBDAnalitica.
  Inserta/actualiza dimensiones y agrega hechos nuevos sin borrar el warehouse.
  Requiere ejecutar primero 01_SistemaBDAnalitica.sql en la misma instancia.
*/
USE [SistemaBDAnalitica];
GO

CREATE OR ALTER PROCEDURE DW.usp_CargarOpinionesDesdeOLTP
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRY
        BEGIN TRANSACTION;

        /* Catálogos controlados y miembros desconocidos. */
        IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=0)
            INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(0,N'UNKNOWN',N'Sin clasificar');
        IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=1)
            INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(1,N'POS',N'Positivo');
        IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=2)
            INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(2,N'NEU',N'Neutral');
        IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=3)
            INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(3,N'NEG',N'Negativo');

        IF NOT EXISTS (SELECT 1 FROM DW.DimFecha WHERE FechaKey=0)
            INSERT DW.DimFecha(FechaKey,Fecha,Dia,NombreDia,SemanaAnio,Mes,NombreMes,Trimestre,Anio,EsFinSemana)
            VALUES(0,'19000101',0,N'Desconocido',0,0,N'Desconocido',0,0,0);
        IF NOT EXISTS (SELECT 1 FROM DW.DimProducto WHERE ProductoKey=0)
        BEGIN
            SET IDENTITY_INSERT DW.DimProducto ON;
            INSERT DW.DimProducto(ProductoKey,IdProductoOLTP,NombreProducto,Categoria)
            VALUES(0,NULL,N'Desconocido',NULL);
            SET IDENTITY_INSERT DW.DimProducto OFF;
        END;
        IF NOT EXISTS (SELECT 1 FROM DW.DimCliente WHERE ClienteKey=0)
        BEGIN
            SET IDENTITY_INSERT DW.DimCliente ON;
            INSERT DW.DimCliente(ClienteKey,IdClienteOLTP,Pais,GrupoEdad,TipoCliente,Segmento)
            VALUES(0,NULL,NULL,NULL,NULL,N'Desconocido');
            SET IDENTITY_INSERT DW.DimCliente OFF;
        END;
        IF NOT EXISTS (SELECT 1 FROM DW.DimFuente WHERE FuenteKey=0)
        BEGIN
            SET IDENTITY_INSERT DW.DimFuente ON;
            INSERT DW.DimFuente(FuenteKey,Canal,TipoFuente,NombreFuente)
            VALUES(0,N'Desconocido',NULL,NULL);
            SET IDENTITY_INSERT DW.DimFuente OFF;
        END;
        IF NOT EXISTS (SELECT 1 FROM DW.DimEvento WHERE EventoKey=0)
        BEGIN
            SET IDENTITY_INSERT DW.DimEvento ON;
            INSERT DW.DimEvento(EventoKey,IdEventoOrigen,NombreEvento,TipoEvento)
            VALUES(0,NULL,N'Sin evento especificado',NULL);
            SET IDENTITY_INSERT DW.DimEvento OFF;
        END;

        /* Producto: los atributos descriptivos se aplanan en la dimensión estrella. */
        UPDATE d SET NombreProducto=p.Nombre, Categoria=c.Nombre
        FROM DW.DimProducto d
        JOIN SistemaBDTransaccional.dbo.Productos p ON p.IdProducto=d.IdProductoOLTP
        LEFT JOIN SistemaBDTransaccional.dbo.Categoria c ON c.IdCategoria=p.IdCategoria
        WHERE d.ProductoKey<>0 AND (d.NombreProducto<>p.Nombre OR ISNULL(d.Categoria,N'')<>ISNULL(c.Nombre,N''));

        INSERT DW.DimProducto(IdProductoOLTP,NombreProducto,Categoria)
        SELECT p.IdProducto,p.Nombre,c.Nombre
        FROM SistemaBDTransaccional.dbo.Productos p
        LEFT JOIN SistemaBDTransaccional.dbo.Categoria c ON c.IdCategoria=p.IdCategoria
        WHERE NOT EXISTS (SELECT 1 FROM DW.DimProducto d WHERE d.IdProductoOLTP=p.IdProducto);

        /* Cliente: la fuente sólo aporta el identificador para análisis. */
        INSERT DW.DimCliente(IdClienteOLTP,Pais,GrupoEdad,TipoCliente,Segmento)
        SELECT c.IdCliente,NULL,NULL,NULL,NULL
        FROM SistemaBDTransaccional.dbo.Clientes c
        WHERE NOT EXISTS (SELECT 1 FROM DW.DimCliente d WHERE d.IdClienteOLTP=c.IdCliente);

        /* Una fila por canal/tipo/nombre; redes sociales quedan separadas por canal concreto. */
        ;WITH Fuentes AS
        (
            SELECT CAST(N'Encuesta' AS NVARCHAR(40)) Canal, CAST(N'CSV' AS NVARCHAR(40)) TipoFuente,
                   CAST(N'Encuesta interna' AS NVARCHAR(150)) NombreFuente
            WHERE EXISTS (SELECT 1 FROM SistemaBDTransaccional.dbo.EncuestasInternas WHERE Estado=1)
            UNION ALL
            SELECT N'Reseña web',N'Web',N'Reseñas web'
            WHERE EXISTS (SELECT 1 FROM SistemaBDTransaccional.dbo.ReseniasWeb WHERE Estado=1)
            UNION ALL
            SELECT DISTINCT CAST(s.RedSocial AS NVARCHAR(40)),N'API',CAST(s.RedSocial AS NVARCHAR(150))
            FROM SistemaBDTransaccional.dbo.ComentariosSociales s WHERE s.Estado=1
        )
        INSERT DW.DimFuente(Canal,TipoFuente,NombreFuente)
        SELECT f.Canal,f.TipoFuente,f.NombreFuente FROM Fuentes f
        WHERE NOT EXISTS (SELECT 1 FROM DW.DimFuente d WHERE d.Canal=f.Canal
                          AND ISNULL(d.TipoFuente,N'')=ISNULL(f.TipoFuente,N'')
                          AND ISNULL(d.NombreFuente,N'')=ISNULL(f.NombreFuente,N''));

        /* Fecha: genera sólo días con opiniones fuente. Los nombres no dependen del idioma de sesión. */
        ;WITH Fechas AS
        (
            SELECT CONVERT(DATE,FechaOpinion) Fecha FROM SistemaBDTransaccional.dbo.EncuestasInternas WHERE Estado=1 AND FechaOpinion IS NOT NULL
            UNION SELECT CONVERT(DATE,FechaResenia) FROM SistemaBDTransaccional.dbo.ReseniasWeb WHERE Estado=1 AND FechaResenia IS NOT NULL
            UNION SELECT CONVERT(DATE,FechaComentario) FROM SistemaBDTransaccional.dbo.ComentariosSociales WHERE Estado=1 AND FechaComentario IS NOT NULL
        )
        INSERT DW.DimFecha(FechaKey,Fecha,Dia,NombreDia,SemanaAnio,Mes,NombreMes,Trimestre,Anio,EsFinSemana)
        SELECT CONVERT(INT,CONVERT(CHAR(8),f.Fecha,112)), f.Fecha, DATEPART(DAY,f.Fecha),
               CASE ((DATEDIFF(DAY,CONVERT(DATE,'19000101'),f.Fecha)%7)+7)%7
                    WHEN 0 THEN N'Lunes' WHEN 1 THEN N'Martes' WHEN 2 THEN N'Miércoles'
                    WHEN 3 THEN N'Jueves' WHEN 4 THEN N'Viernes' WHEN 5 THEN N'Sábado' ELSE N'Domingo' END,
               DATEPART(ISO_WEEK,f.Fecha), DATEPART(MONTH,f.Fecha),
               CASE DATEPART(MONTH,f.Fecha) WHEN 1 THEN N'Enero' WHEN 2 THEN N'Febrero' WHEN 3 THEN N'Marzo'
                    WHEN 4 THEN N'Abril' WHEN 5 THEN N'Mayo' WHEN 6 THEN N'Junio' WHEN 7 THEN N'Julio'
                    WHEN 8 THEN N'Agosto' WHEN 9 THEN N'Septiembre' WHEN 10 THEN N'Octubre'
                    WHEN 11 THEN N'Noviembre' ELSE N'Diciembre' END,
               DATEPART(QUARTER,f.Fecha), DATEPART(YEAR,f.Fecha),
               CASE WHEN ((DATEDIFF(DAY,CONVERT(DATE,'19000101'),f.Fecha)%7)+7)%7 IN (5,6) THEN 1 ELSE 0 END
        FROM Fechas f
        WHERE NOT EXISTS (SELECT 1 FROM DW.DimFecha d WHERE d.Fecha=f.Fecha);

        /* Hechos: sentimiento existente para encuesta, derivado por rating para web, desconocido para social. */
        ;WITH Opiniones AS
        (
            SELECT N'Encuesta' Canal, N'CSV' TipoFuente, N'Encuesta interna' NombreFuente,
                   CONCAT(N'Encuesta:',e.IdOpinion) IdOpinionOrigen, e.IdCliente, e.IdProducto,
                   CONVERT(DATE,e.FechaOpinion) Fecha, e.PuntajeSatisfaccion Puntaje,
                   c.Nombre Sentimiento, CONVERT(INT,0) Likes, CONVERT(INT,0) Votos
            FROM SistemaBDTransaccional.dbo.EncuestasInternas e
            LEFT JOIN SistemaBDTransaccional.dbo.Clasificacion c ON c.IdClasificacion=e.IdClasificacion
            WHERE e.Estado=1
            UNION ALL
            SELECT N'Reseña web',N'Web',N'Reseñas web',CONCAT(N'ReseniaWeb:',r.IdReview),r.IdCliente,r.IdProducto,
                   CONVERT(DATE,r.FechaResenia),r.Rating,
                   CASE WHEN r.Rating<=2 THEN N'Negativo' WHEN r.Rating=3 THEN N'Neutral' ELSE N'Positivo' END,
                   CONVERT(INT,0),ISNULL(r.UtilPara,0)
            FROM SistemaBDTransaccional.dbo.ReseniasWeb r WHERE r.Estado=1
            UNION ALL
            SELECT CAST(s.RedSocial AS NVARCHAR(40)),N'API',CAST(s.RedSocial AS NVARCHAR(150)),
                   CONCAT(N'Social:',s.IdComment),s.IdCliente,s.IdProducto,CONVERT(DATE,s.FechaComentario),
                   CAST(NULL AS INT),CAST(NULL AS NVARCHAR(50)),ISNULL(s.LikesRetweets,0),CONVERT(INT,0)
            FROM SistemaBDTransaccional.dbo.ComentariosSociales s WHERE s.Estado=1
        )
        INSERT DW.FactOpiniones(FechaKey,ProductoKey,ClienteKey,FuenteKey,SentimientoKey,EventoKey,
                                IdOpinionOrigen,PuntajeSatisfaccion,CantidadOpiniones,LikesRetweets,VotosUtilidad)
        SELECT COALESCE(df.FechaKey,0),COALESCE(dp.ProductoKey,0),COALESCE(dc.ClienteKey,0),
               COALESCE(dsrc.FuenteKey,0),COALESCE(ds.SentimientoKey,0),0,
               o.IdOpinionOrigen,o.Puntaje,1,o.Likes,o.Votos
        FROM Opiniones o
        LEFT JOIN DW.DimFecha df ON df.Fecha=o.Fecha
        LEFT JOIN DW.DimProducto dp ON dp.IdProductoOLTP=o.IdProducto
        LEFT JOIN DW.DimCliente dc ON dc.IdClienteOLTP=o.IdCliente
        LEFT JOIN DW.DimFuente dsrc ON dsrc.Canal=o.Canal AND dsrc.TipoFuente=o.TipoFuente
                                     AND dsrc.NombreFuente=o.NombreFuente
        LEFT JOIN DW.DimSentimiento ds ON ds.Codigo=CASE
            WHEN LOWER(LTRIM(RTRIM(o.Sentimiento))) IN (N'positivo',N'pos',N'positive') THEN N'POS'
            WHEN LOWER(LTRIM(RTRIM(o.Sentimiento))) IN (N'neutral',N'neu') THEN N'NEU'
            WHEN LOWER(LTRIM(RTRIM(o.Sentimiento))) IN (N'negativo',N'neg',N'negative') THEN N'NEG'
            ELSE N'UNKNOWN' END
        WHERE NOT EXISTS
        (
            SELECT 1 FROM DW.FactOpiniones f
            WHERE f.FuenteKey=COALESCE(dsrc.FuenteKey,0) AND f.IdOpinionOrigen=o.IdOpinionOrigen
        );

        COMMIT TRANSACTION;

        SELECT 'DimFecha' Tabla, COUNT_BIG(*) Filas FROM DW.DimFecha
        UNION ALL SELECT 'DimProducto',COUNT_BIG(*) FROM DW.DimProducto
        UNION ALL SELECT 'DimCliente',COUNT_BIG(*) FROM DW.DimCliente
        UNION ALL SELECT 'DimFuente',COUNT_BIG(*) FROM DW.DimFuente
        UNION ALL SELECT 'DimSentimiento',COUNT_BIG(*) FROM DW.DimSentimiento
        UNION ALL SELECT 'DimEvento',COUNT_BIG(*) FROM DW.DimEvento
        UNION ALL SELECT 'FactOpiniones',COUNT_BIG(*) FROM DW.FactOpiniones;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH;
END;
GO

EXEC DW.usp_CargarOpinionesDesdeOLTP;
GO
