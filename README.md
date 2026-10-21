# Excel Importer 

## Lokalna baza (DemoDb u Dockeru)

```
copy .env.example .env      # postavite MSSQL_SA_PASSWORD (i opciono DEMO_APP_PASSWORD)
docker compose up mssql db-init
```

SQL Server 2025 sluša na `localhost:14333` (promenljiva `MSSQL_PORT`). `db-init` kreira `DemoDb`
(Gradovi, Kupci sa FK GradId, Proizvodi) i primer podatke idempotentno; podaci su u volumenu
`mssql-data` i preživljavaju `docker compose down` (brisanje: `down -v`). Ako je `DEMO_APP_PASSWORD`
postavljen, kreira se login `DEMO_APP_LOGIN` samo sa `db_datareader` + `db_datawriter`.

## Baza aplikacije i migracije (EF Core, SQL Server)

Development connection string je u `src/ExcelImporter.Api/appsettings.Development.json` (LocalDB,
`ExcelImporterDb`). Migracije su u `src/ExcelImporter.Infrastructure/Persistence/Migrations`.

```
cd src/ExcelImporter.Infrastructure
dotnet ef database update
```

Komanda kreira bazu `ExcelImporterDb` na `(localdb)\MSSQLLocalDB` i primenjuje sve migracije.
Za drugu instancu postavite `EXCELIMPORTER_DESIGN_CONNECTION` (connection string se ne cuva u kodu).
Van Development okruzenja (Production) connection string mora da se postavi env varijablom `Database__ConnectionString`
(ili user-secrets kljucem `Database:ConnectionString`). Ako nedostaje, aplikacija ispisuje jasnu poruku i izlazi
sa kodom 1. `dotnet run` koristi launch profil sa `ASPNETCORE_ENVIRONMENT=Development` (LocalDB). `/health` vraca 503
kad baza nije dostupna, a proces ostaje ziv.

Nova migracija: `dotnet ef migrations add <Ime>`. Alat: `dotnet tool update --global dotnet-ef`.