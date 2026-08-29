/*
================================================================================
  LA CABRERA - Central de Informacion para Franquicias
  Script: 00_create_database.sql
  Descripcion: Crea la base de datos LaCabreraDB si no existe
  Autor: Claude Code
  Fecha: 2026-07-16

  INSTRUCCIONES:
  - Ejecutar con usuario con permisos de creacion de base de datos (sa o similar)
  - Este script es idempotente: puede ejecutarse multiples veces sin error
================================================================================
*/

USE [master]
GO

-- Verificar si la base de datos existe antes de crearla
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'LaCabreraDB')
BEGIN
    PRINT 'Creando base de datos LaCabreraDB...'

    CREATE DATABASE [LaCabreraDB]
    ON PRIMARY
    (
        NAME = N'LaCabreraDB',
        FILENAME = N'C:\SQLData\LaCabreraDB.mdf',  -- Ajustar ruta segun ambiente
        SIZE = 100MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 100MB
    )
    LOG ON
    (
        NAME = N'LaCabreraDB_log',
        FILENAME = N'C:\SQLData\LaCabreraDB_log.ldf',  -- Ajustar ruta segun ambiente
        SIZE = 50MB,
        MAXSIZE = 2048GB,
        FILEGROWTH = 50MB
    )

    PRINT 'Base de datos LaCabreraDB creada exitosamente.'
END
ELSE
BEGIN
    PRINT 'La base de datos LaCabreraDB ya existe. No se realizaron cambios.'
END
GO

-- Configuraciones basicas de la base de datos
USE [LaCabreraDB]
GO

-- Configurar el modo de recuperacion
ALTER DATABASE [LaCabreraDB] SET RECOVERY SIMPLE
GO

-- Habilitar snapshot isolation para evitar bloqueos en lecturas del dashboard
ALTER DATABASE [LaCabreraDB] SET ALLOW_SNAPSHOT_ISOLATION ON
GO

ALTER DATABASE [LaCabreraDB] SET READ_COMMITTED_SNAPSHOT ON
GO

PRINT 'Configuracion de base de datos completada.'
GO
