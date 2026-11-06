-- Optional local login. Do not commit a real password.
-- From master, after you choose a local password:
--   sqlcmd -S localhost,1433 -U sa -v AppPassword="your-local-password" -i sql/004_login.sql
-- The password is supplied on the command line and is not stored in this file.
-- rpt_reader receives the procedures and views only. It is not granted lab tables.
-- dbo owns both schemas, so ownership chaining lets the procedure read lab rows.

-- CREATE LOGIN is server-scoped and can run from any database.
IF NOT EXISTS (SELECT 1 FROM sys.sql_logins WHERE name = N'rpt_app')
BEGIN
    CREATE LOGIN rpt_app WITH PASSWORD = N'$(AppPassword)', CHECK_POLICY = ON, CHECK_EXPIRATION = OFF;
END;
GO

USE LabReportDemo;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'rpt_app')
BEGIN
    CREATE USER rpt_app FOR LOGIN rpt_app;
END;
GO

ALTER ROLE rpt_reader ADD MEMBER rpt_app;
GO
