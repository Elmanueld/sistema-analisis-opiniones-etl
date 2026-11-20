/*
  Instalación idempotente del esquema de opiniones de clientes.
  No elimina ni trunca objetos existentes. Ejecutar en SQL Server con permisos DDL.
*/
IF DB_ID(N'SistemaBDTransaccional') IS NULL
    EXEC(N'CREATE DATABASE [SistemaBDTransaccional]');
GO

USE [SistemaBDTransaccional];
GO

IF OBJECT_ID(N'dbo.Categoria', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Categoria (
        IdCategoria INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Categoria PRIMARY KEY,
        Nombre VARCHAR(100) NOT NULL,
        Descripcion VARCHAR(500) NULL,
        Estado BIT NULL CONSTRAINT DF_Categoria_Estado DEFAULT (1),
        CONSTRAINT UQ_Categoria_Nombre UNIQUE (Nombre)
    );
END;
GO

IF OBJECT_ID(N'dbo.Clasificacion', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clasificacion (
        IdClasificacion INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clasificacion PRIMARY KEY,
        Nombre VARCHAR(50) NOT NULL,
        Descripcion VARCHAR(255) NULL,
        Estado BIT NULL CONSTRAINT DF_Clasificacion_Estado DEFAULT (1),
        CONSTRAINT UQ_Clasificacion_Nombre UNIQUE (Nombre),
        CONSTRAINT CHK_ClasificacionNombre CHECK (Nombre IN ('Negativo','Neutral','Positivo'))
    );
END;
GO

IF OBJECT_ID(N'dbo.Clientes', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clientes (
        IdCliente INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Clientes PRIMARY KEY,
        Nombre VARCHAR(150) NOT NULL,
        Email VARCHAR(120) NOT NULL,
        Telefono VARCHAR(20) NULL,
        Direccion VARCHAR(300) NULL,
        FechaRegistro DATETIME NULL,
        Estado BIT NULL CONSTRAINT DF_Clientes_Estado DEFAULT (1),
        CONSTRAINT UQ_Clientes_Email UNIQUE (Email),
        CONSTRAINT CHK_ClienteEmail CHECK (Email LIKE '%@%.%')
    );
END;
GO

IF OBJECT_ID(N'dbo.TipoFuente', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TipoFuente (
        IdTipoFuente INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_TipoFuente PRIMARY KEY,
        Nombre VARCHAR(50) NOT NULL,
        Descripcion VARCHAR(255) NULL,
        CONSTRAINT UQ_TipoFuente_Nombre UNIQUE (Nombre),
        CONSTRAINT CHK_TipoFuenteNombre CHECK (Nombre IN ('API','CSV','Web'))
    );
END;
GO

IF OBJECT_ID(N'dbo.FuenteDatos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FuenteDatos (
        IdFuente INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_FuenteDatos PRIMARY KEY,
        IdTipoFuente INT NOT NULL,
        FechaCarga DATETIME NULL,
        Estado VARCHAR(50) NULL,
        DescripcionCarga VARCHAR(500) NULL,
        CONSTRAINT FK_FuenteDatos_TipoFuente FOREIGN KEY (IdTipoFuente) REFERENCES dbo.TipoFuente(IdTipoFuente)
    );
END;
GO

IF OBJECT_ID(N'dbo.Productos', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Productos (
        IdProducto INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Productos PRIMARY KEY,
        Nombre VARCHAR(200) NOT NULL,
        Descripcion VARCHAR(1000) NULL,
        IdCategoria INT NOT NULL,
        Precio DECIMAL(10,2) NOT NULL,
        FechaCreacion DATETIME NULL CONSTRAINT DF_Productos_FechaCreacion DEFAULT (GETDATE()),
        Estado BIT NULL CONSTRAINT DF_Productos_Estado DEFAULT (1),
        CONSTRAINT FK_Productos_Categoria FOREIGN KEY (IdCategoria) REFERENCES dbo.Categoria(IdCategoria)
    );
END;
GO

IF OBJECT_ID(N'dbo.ComentariosSociales', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ComentariosSociales (
        IdComment INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ComentariosSociales PRIMARY KEY,
        IdCliente INT NULL,
        IdProducto INT NOT NULL,
        IdFuente INT NOT NULL,
        RedSocial VARCHAR(50) NOT NULL,
        Comentario VARCHAR(1000) NOT NULL,
        FechaComentario DATETIME NULL,
        LikesRetweets INT NULL,
        Estado BIT NULL CONSTRAINT DF_ComentariosSociales_Estado DEFAULT (1),
        CONSTRAINT FK_ComentariosSociales_Clientes FOREIGN KEY (IdCliente) REFERENCES dbo.Clientes(IdCliente),
        CONSTRAINT FK_ComentariosSociales_Productos FOREIGN KEY (IdProducto) REFERENCES dbo.Productos(IdProducto),
        CONSTRAINT FK_ComentariosSociales_FuenteDatos FOREIGN KEY (IdFuente) REFERENCES dbo.FuenteDatos(IdFuente)
    );
END;
GO

IF OBJECT_ID(N'dbo.EncuestasInternas', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EncuestasInternas (
        IdOpinion INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_EncuestasInternas PRIMARY KEY,
        IdCliente INT NOT NULL,
        IdProducto INT NOT NULL,
        IdFuente INT NOT NULL,
        IdClasificacion INT NULL,
        Comentario VARCHAR(1000) NULL,
        PuntajeSatisfaccion INT NULL,
        FechaOpinion DATETIME NULL,
        Estado BIT NULL CONSTRAINT DF_EncuestasInternas_Estado DEFAULT (1),
        CONSTRAINT FK_EncuestasInternas_Clientes FOREIGN KEY (IdCliente) REFERENCES dbo.Clientes(IdCliente),
        CONSTRAINT FK_EncuestasInternas_Productos FOREIGN KEY (IdProducto) REFERENCES dbo.Productos(IdProducto),
        CONSTRAINT FK_EncuestasInternas_FuenteDatos FOREIGN KEY (IdFuente) REFERENCES dbo.FuenteDatos(IdFuente),
        CONSTRAINT FK_EncuestasInternas_Clasificacion FOREIGN KEY (IdClasificacion) REFERENCES dbo.Clasificacion(IdClasificacion),
        CONSTRAINT CHK_PuntajeSatisfaccion CHECK (PuntajeSatisfaccion BETWEEN 1 AND 5)
    );
END;
GO

IF OBJECT_ID(N'dbo.ReseniasWeb', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReseniasWeb (
        IdReview INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_ReseniasWeb PRIMARY KEY,
        IdCliente INT NOT NULL,
        IdProducto INT NOT NULL,
        IdFuente INT NOT NULL,
        Comentario VARCHAR(1000) NULL,
        Rating INT NOT NULL,
        FechaResenia DATETIME NULL,
        UtilPara INT NULL CONSTRAINT DF_ReseniasWeb_UtilPara DEFAULT (0),
        Estado BIT NULL CONSTRAINT DF_ReseniasWeb_Estado DEFAULT (1),
        CONSTRAINT FK_ReseniasWeb_Clientes FOREIGN KEY (IdCliente) REFERENCES dbo.Clientes(IdCliente),
        CONSTRAINT FK_ReseniasWeb_Productos FOREIGN KEY (IdProducto) REFERENCES dbo.Productos(IdProducto),
        CONSTRAINT FK_ReseniasWeb_FuenteDatos FOREIGN KEY (IdFuente) REFERENCES dbo.FuenteDatos(IdFuente),
        CONSTRAINT CHK_Rating CHECK (Rating BETWEEN 1 AND 5)
    );
END;
GO

/* Catálogos requeridos por el modelo. Los existentes se conservan. */
IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre='Juguetes') INSERT dbo.Categoria(Nombre,Descripcion) VALUES('Juguetes','Productos de juguete');
IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre=CONVERT(VARCHAR(100),N'Electr'+NCHAR(243)+N'nica'))
    INSERT dbo.Categoria(Nombre,Descripcion)
    VALUES(CONVERT(VARCHAR(100),N'Electr'+NCHAR(243)+N'nica'),CONVERT(VARCHAR(500),N'Productos electr'+NCHAR(243)+N'nicos'));
IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre='Ropa') INSERT dbo.Categoria(Nombre,Descripcion) VALUES('Ropa','Prendas de vestir');
IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre='Hogar') INSERT dbo.Categoria(Nombre,Descripcion) VALUES('Hogar','Productos para el hogar');
IF NOT EXISTS (SELECT 1 FROM dbo.Categoria WHERE Nombre='Deportes') INSERT dbo.Categoria(Nombre,Descripcion) VALUES('Deportes','Artículos deportivos');

IF NOT EXISTS (SELECT 1 FROM dbo.Clasificacion WHERE Nombre='Positivo') INSERT dbo.Clasificacion(Nombre,Descripcion) VALUES('Positivo','Opinión positiva');
IF NOT EXISTS (SELECT 1 FROM dbo.Clasificacion WHERE Nombre='Neutral') INSERT dbo.Clasificacion(Nombre,Descripcion) VALUES('Neutral','Opinión neutral');
IF NOT EXISTS (SELECT 1 FROM dbo.Clasificacion WHERE Nombre='Negativo') INSERT dbo.Clasificacion(Nombre,Descripcion) VALUES('Negativo','Opinión negativa');

IF NOT EXISTS (SELECT 1 FROM dbo.TipoFuente WHERE Nombre='Web') INSERT dbo.TipoFuente(Nombre,Descripcion) VALUES('Web','Opiniones del portal web');
IF NOT EXISTS (SELECT 1 FROM dbo.TipoFuente WHERE Nombre='CSV') INSERT dbo.TipoFuente(Nombre,Descripcion) VALUES('CSV','Datos cargados por archivo CSV');
IF NOT EXISTS (SELECT 1 FROM dbo.TipoFuente WHERE Nombre='API') INSERT dbo.TipoFuente(Nombre,Descripcion) VALUES('API','Comentarios obtenidos por API de redes sociales');
GO
