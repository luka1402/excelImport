IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE TABLE [ConnectionConfig] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(100) NOT NULL,
        [DisplayName] nvarchar(200) NOT NULL,
        [ConfigKey] nvarchar(200) NOT NULL,
        CONSTRAINT [PK_ConnectionConfig] PRIMARY KEY ([Id])
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE TABLE [ImportTemplate] (
        [Id] int NOT NULL IDENTITY,
        [Name] nvarchar(200) NOT NULL,
        [ConnectionId] int NOT NULL,
        [SchemaName] nvarchar(128) NOT NULL,
        [TableName] nvarchar(128) NOT NULL,
        [SheetName] nvarchar(255) NULL,
        [HeaderRow] int NOT NULL,
        [ColumnsFingerprint] nvarchar(64) NOT NULL,
        [MappingJson] nvarchar(max) NOT NULL,
        [RulesJson] nvarchar(max) NOT NULL,
        [ConstantsJson] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2(3) NOT NULL,
        [UpdatedAt] datetime2(3) NOT NULL,
        CONSTRAINT [PK_ImportTemplate] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImportTemplate_ConnectionConfig_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [ConnectionConfig] ([Id]) ON DELETE NO ACTION
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE TABLE [ImportJob] (
        [Id] bigint NOT NULL IDENTITY,
        [StartedAt] datetime2(3) NOT NULL,
        [FinishedAt] datetime2(3) NULL,
        [FileName] nvarchar(260) NOT NULL,
        [FileSize] bigint NOT NULL,
        [ConnectionId] int NOT NULL,
        [TargetTable] nvarchar(300) NOT NULL,
        [TemplateId] int NULL,
        [Mode] nvarchar(30) NOT NULL,
        [Status] nvarchar(30) NOT NULL,
        [TotalRows] int NOT NULL,
        [ImportedRows] int NOT NULL,
        [SkippedRows] int NOT NULL,
        [ErrorRows] int NOT NULL,
        [ErrorReportPath] nvarchar(500) NULL,
        CONSTRAINT [PK_ImportJob] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImportJob_ConnectionConfig_ConnectionId] FOREIGN KEY ([ConnectionId]) REFERENCES [ConnectionConfig] ([Id]) ON DELETE NO ACTION,
        CONSTRAINT [FK_ImportJob_ImportTemplate_TemplateId] FOREIGN KEY ([TemplateId]) REFERENCES [ImportTemplate] ([Id]) ON DELETE SET NULL
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE TABLE [ImportError] (
        [Id] bigint NOT NULL IDENTITY,
        [JobId] bigint NOT NULL,
        [RowNumber] int NOT NULL,
        [ColumnName] nvarchar(200) NULL,
        [Reason] nvarchar(1000) NOT NULL,
        CONSTRAINT [PK_ImportError] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_ImportError_ImportJob_JobId] FOREIGN KEY ([JobId]) REFERENCES [ImportJob] ([Id]) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ConnectionConfig_Name] ON [ConnectionConfig] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportError_JobId] ON [ImportError] ([JobId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportJob_ConnectionId] ON [ImportJob] ([ConnectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportJob_StartedAt] ON [ImportJob] ([StartedAt] DESC);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportJob_TemplateId] ON [ImportJob] ([TemplateId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_ImportTemplate_ConnectionId] ON [ImportTemplate] ([ConnectionId]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_ImportTemplate_Name] ON [ImportTemplate] ([Name]);
END;

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20261020102416_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20261020102416_InitialCreate', N'10.0.13');
END;

COMMIT;
GO

