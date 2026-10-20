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
