/*
  Modelo analítico dimensional (OLAP) para el Sistema de Análisis de Opiniones.
  Idempotente: crea la base si falta y las tablas si faltan; no elimina datos.
  Ejecutar en SQL Server con permisos CREATE DATABASE / CREATE TABLE.
*/
USE master;
GO
IF DB_ID(N'SistemaBDAnalitica') IS NULL
    CREATE DATABASE SistemaBDAnalitica;
GO
USE SistemaBDAnalitica;
GO
IF SCHEMA_ID(N'DW') IS NULL EXEC(N'CREATE SCHEMA DW');
GO

IF OBJECT_ID(N'DW.DimFecha', N'U') IS NULL
BEGIN
    CREATE TABLE DW.DimFecha (
        FechaKey INT NOT NULL CONSTRAINT PK_DimFecha PRIMARY KEY, -- YYYYMMDD; 0 = desconocida
        Fecha DATE NOT NULL,
        Dia TINYINT NOT NULL,
        NombreDia NVARCHAR(15) NOT NULL,
        SemanaAnio TINYINT NOT NULL,
        Mes TINYINT NOT NULL,
        NombreMes NVARCHAR(15) NOT NULL,
        Trimestre TINYINT NOT NULL,
        Anio SMALLINT NOT NULL,
        EsFinSemana BIT NOT NULL,
        CONSTRAINT UQ_DimFecha_Fecha UNIQUE (Fecha),
        CONSTRAINT CHK_DimFecha_Trimestre CHECK (Trimestre BETWEEN 0 AND 4),
        CONSTRAINT CHK_DimFecha_Mes CHECK (Mes BETWEEN 0 AND 12)
    );
END;
GO
IF OBJECT_ID(N'DW.DimProducto', N'U') IS NULL
BEGIN
    CREATE TABLE DW.DimProducto (
        ProductoKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimProducto PRIMARY KEY,
        IdProductoOLTP INT NULL,
        NombreProducto NVARCHAR(200) NOT NULL,
        Categoria NVARCHAR(100) NULL
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DW.DimProducto') AND name=N'UX_DimProducto_IdProductoOLTP')
    CREATE UNIQUE INDEX UX_DimProducto_IdProductoOLTP ON DW.DimProducto(IdProductoOLTP) WHERE IdProductoOLTP IS NOT NULL;
GO
IF OBJECT_ID(N'DW.DimCliente', N'U') IS NULL
BEGIN
    CREATE TABLE DW.DimCliente (
        ClienteKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimCliente PRIMARY KEY,
        IdClienteOLTP INT NULL,
        Pais NVARCHAR(100) NULL,
        GrupoEdad NVARCHAR(30) NULL,
        TipoCliente NVARCHAR(50) NULL,
        Segmento NVARCHAR(100) NULL
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DW.DimCliente') AND name=N'UX_DimCliente_IdClienteOLTP')
    CREATE UNIQUE INDEX UX_DimCliente_IdClienteOLTP ON DW.DimCliente(IdClienteOLTP) WHERE IdClienteOLTP IS NOT NULL;
GO
IF OBJECT_ID(N'DW.DimFuente', N'U') IS NULL
BEGIN
    CREATE TABLE DW.DimFuente (
        FuenteKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimFuente PRIMARY KEY,
        Canal NVARCHAR(40) NOT NULL,
        TipoFuente NVARCHAR(40) NULL,
        NombreFuente NVARCHAR(150) NULL,
        CONSTRAINT UQ_DimFuente UNIQUE (Canal, TipoFuente, NombreFuente)
    );
END;
GO
IF OBJECT_ID(N'DW.DimSentimiento', N'U') IS NULL
BEGIN
    CREATE TABLE DW.DimSentimiento (
        SentimientoKey TINYINT NOT NULL CONSTRAINT PK_DimSentimiento PRIMARY KEY,
        Codigo NVARCHAR(20) NOT NULL CONSTRAINT UQ_DimSentimiento_Codigo UNIQUE,
        Nombre NVARCHAR(40) NOT NULL,
        CONSTRAINT CHK_DimSentimiento_Codigo CHECK (Codigo IN (N'UNKNOWN',N'POS',N'NEU',N'NEG'))
    );
END;
GO
IF OBJECT_ID(N'DW.DimEvento', N'U') IS NULL
BEGIN
    CREATE TABLE DW.DimEvento (
        EventoKey INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_DimEvento PRIMARY KEY,
        IdEventoOrigen NVARCHAR(100) NULL,
        NombreEvento NVARCHAR(200) NOT NULL,
        TipoEvento NVARCHAR(80) NULL,
        FechaInicio DATE NULL,
        FechaFin DATE NULL,
        CONSTRAINT CHK_DimEvento_RangoFechas CHECK (FechaFin IS NULL OR FechaInicio IS NULL OR FechaFin >= FechaInicio)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DW.DimEvento') AND name=N'UX_DimEvento_IdEventoOrigen')
    CREATE UNIQUE INDEX UX_DimEvento_IdEventoOrigen ON DW.DimEvento(IdEventoOrigen) WHERE IdEventoOrigen IS NOT NULL;
GO
IF OBJECT_ID(N'DW.FactOpiniones', N'U') IS NULL
BEGIN
    CREATE TABLE DW.FactOpiniones (
        OpinionKey BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FactOpiniones PRIMARY KEY,
        FechaKey INT NOT NULL,
        ProductoKey INT NOT NULL,
        ClienteKey INT NOT NULL,
        FuenteKey INT NOT NULL,
        SentimientoKey TINYINT NOT NULL,
        EventoKey INT NOT NULL,
        IdOpinionOrigen NVARCHAR(100) NOT NULL,
        PuntajeSatisfaccion DECIMAL(4,2) NULL,
        CantidadOpiniones TINYINT NOT NULL CONSTRAINT DF_FactOpiniones_Cantidad DEFAULT (1),
        LikesRetweets INT NOT NULL CONSTRAINT DF_FactOpiniones_Likes DEFAULT (0),
        VotosUtilidad INT NOT NULL CONSTRAINT DF_FactOpiniones_Utilidad DEFAULT (0),
        FechaCargaDW DATETIME2(0) NOT NULL CONSTRAINT DF_FactOpiniones_Carga DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT UQ_FactOpiniones_Origen UNIQUE (FuenteKey, IdOpinionOrigen),
        CONSTRAINT FK_FactOpiniones_Fecha FOREIGN KEY (FechaKey) REFERENCES DW.DimFecha(FechaKey),
        CONSTRAINT FK_FactOpiniones_Producto FOREIGN KEY (ProductoKey) REFERENCES DW.DimProducto(ProductoKey),
        CONSTRAINT FK_FactOpiniones_Cliente FOREIGN KEY (ClienteKey) REFERENCES DW.DimCliente(ClienteKey),
        CONSTRAINT FK_FactOpiniones_Fuente FOREIGN KEY (FuenteKey) REFERENCES DW.DimFuente(FuenteKey),
        CONSTRAINT FK_FactOpiniones_Sentimiento FOREIGN KEY (SentimientoKey) REFERENCES DW.DimSentimiento(SentimientoKey),
        CONSTRAINT FK_FactOpiniones_Evento FOREIGN KEY (EventoKey) REFERENCES DW.DimEvento(EventoKey),
        CONSTRAINT CHK_FactOpiniones_Puntaje CHECK (PuntajeSatisfaccion IS NULL OR PuntajeSatisfaccion BETWEEN 1 AND 5),
        CONSTRAINT CHK_FactOpiniones_Cantidad CHECK (CantidadOpiniones = 1),
        CONSTRAINT CHK_FactOpiniones_Engagement CHECK (LikesRetweets >= 0 AND VotosUtilidad >= 0)
    );
    CREATE INDEX IX_FactOpiniones_FechaProducto ON DW.FactOpiniones(FechaKey, ProductoKey);
    CREATE INDEX IX_FactOpiniones_FuenteSentimiento ON DW.FactOpiniones(FuenteKey, SentimientoKey);
END;
GO

/* Miembros desconocidos para conservar integridad ante valores faltantes. */
IF NOT EXISTS (SELECT 1 FROM DW.DimFecha WHERE FechaKey=0)
    INSERT DW.DimFecha(FechaKey,Fecha,Dia,NombreDia,SemanaAnio,Mes,NombreMes,Trimestre,Anio,EsFinSemana)
    VALUES(0,'19000101',0,N'Desconocido',0,0,N'Desconocido',0,0,0);
IF NOT EXISTS (SELECT 1 FROM DW.DimProducto WHERE ProductoKey=0)
    SET IDENTITY_INSERT DW.DimProducto ON;
IF NOT EXISTS (SELECT 1 FROM DW.DimProducto WHERE ProductoKey=0)
    INSERT DW.DimProducto(ProductoKey,IdProductoOLTP,NombreProducto,Categoria) VALUES(0,NULL,N'Desconocido',NULL);
IF EXISTS (SELECT 1 FROM DW.DimProducto WHERE ProductoKey=0)
    SET IDENTITY_INSERT DW.DimProducto OFF;
IF NOT EXISTS (SELECT 1 FROM DW.DimCliente WHERE ClienteKey=0)
    SET IDENTITY_INSERT DW.DimCliente ON;
IF NOT EXISTS (SELECT 1 FROM DW.DimCliente WHERE ClienteKey=0)
    INSERT DW.DimCliente(ClienteKey,IdClienteOLTP,Pais,GrupoEdad,TipoCliente,Segmento) VALUES(0,NULL,NULL,NULL,NULL,N'Desconocido');
IF EXISTS (SELECT 1 FROM DW.DimCliente WHERE ClienteKey=0)
    SET IDENTITY_INSERT DW.DimCliente OFF;
IF NOT EXISTS (SELECT 1 FROM DW.DimFuente WHERE FuenteKey=0)
    SET IDENTITY_INSERT DW.DimFuente ON;
IF NOT EXISTS (SELECT 1 FROM DW.DimFuente WHERE FuenteKey=0)
    INSERT DW.DimFuente(FuenteKey,Canal,TipoFuente,NombreFuente) VALUES(0,N'Desconocido',NULL,NULL);
IF EXISTS (SELECT 1 FROM DW.DimFuente WHERE FuenteKey=0)
    SET IDENTITY_INSERT DW.DimFuente OFF;
IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=0)
    INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(0,N'UNKNOWN',N'Sin clasificar');
IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=1)
    INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(1,N'POS',N'Positivo');
IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=2)
    INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(2,N'NEU',N'Neutral');
IF NOT EXISTS (SELECT 1 FROM DW.DimSentimiento WHERE SentimientoKey=3)
    INSERT DW.DimSentimiento(SentimientoKey,Codigo,Nombre) VALUES(3,N'NEG',N'Negativo');
IF NOT EXISTS (SELECT 1 FROM DW.DimEvento WHERE EventoKey=0)
    SET IDENTITY_INSERT DW.DimEvento ON;
IF NOT EXISTS (SELECT 1 FROM DW.DimEvento WHERE EventoKey=0)
    INSERT DW.DimEvento(EventoKey,IdEventoOrigen,NombreEvento,TipoEvento) VALUES(0,NULL,N'Sin evento especificado',NULL);
IF EXISTS (SELECT 1 FROM DW.DimEvento WHERE EventoKey=0)
    SET IDENTITY_INSERT DW.DimEvento OFF;
GO


