IF OBJECT_ID(N'dbo.Gradovi', N'U') IS NULL
CREATE TABLE dbo.Gradovi
(
    Id       INT IDENTITY(1,1) CONSTRAINT PK_Gradovi PRIMARY KEY,
    Naziv    NVARCHAR(100) NOT NULL CONSTRAINT UQ_Gradovi_Naziv UNIQUE,
    Drzava   NVARCHAR(100) NOT NULL
);

IF OBJECT_ID(N'dbo.Kupci', N'U') IS NULL
CREATE TABLE dbo.Kupci
(
    Id      INT IDENTITY(1,1) CONSTRAINT PK_Kupci PRIMARY KEY,
    Ime     NVARCHAR(100) NOT NULL,
    Prezime NVARCHAR(100) NOT NULL,
    Email   NVARCHAR(200) NOT NULL CONSTRAINT UQ_Kupci_Email UNIQUE,
    GradId  INT NOT NULL CONSTRAINT FK_Kupci_Gradovi REFERENCES dbo.Gradovi(Id)
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Kupci_GradId' AND object_id = OBJECT_ID(N'dbo.Kupci'))
    CREATE INDEX IX_Kupci_GradId ON dbo.Kupci(GradId);

IF OBJECT_ID(N'dbo.Proizvodi', N'U') IS NULL
CREATE TABLE dbo.Proizvodi
(
    Id     INT IDENTITY(1,1) CONSTRAINT PK_Proizvodi PRIMARY KEY,
    Sifra  NVARCHAR(50)   NOT NULL CONSTRAINT UQ_Proizvodi_Sifra UNIQUE,
    Naziv  NVARCHAR(200)  NOT NULL,
    Cena   DECIMAL(18,2)  NOT NULL,
    Zaliha INT            NOT NULL CONSTRAINT DF_Proizvodi_Zaliha DEFAULT 0
);
