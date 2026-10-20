-- Login sa minimalnim pravima za ciljne baze (citanje + upis, bez DDL-a).
-- Ime i lozinka dolaze iz okruzenja (sqlcmd -v), nikad hardkodovano.
DECLARE @login SYSNAME = N'$(AppLogin)';
DECLARE @pwd   NVARCHAR(128) = N'$(AppPassword)';
DECLARE @sql   NVARCHAR(MAX);

IF SUSER_ID(@login) IS NULL
BEGIN
    SET @sql = N'CREATE LOGIN ' + QUOTENAME(@login) + N' WITH PASSWORD = ' + QUOTENAME(@pwd, '''') + N', CHECK_POLICY = ON;';
    EXEC (@sql);
END

IF USER_ID(@login) IS NULL
BEGIN
    SET @sql = N'CREATE USER ' + QUOTENAME(@login) + N' FOR LOGIN ' + QUOTENAME(@login) + N';';
    EXEC (@sql);
END

SET @sql = N'ALTER ROLE db_datareader ADD MEMBER ' + QUOTENAME(@login) + N'; ALTER ROLE db_datawriter ADD MEMBER ' + QUOTENAME(@login) + N';';
EXEC (@sql);
