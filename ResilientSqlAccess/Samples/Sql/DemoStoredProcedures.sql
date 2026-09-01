-- Demo schema + stored procedures used by the ResilientSqlAccess samples.
-- Run this against a scratch database before running Samples/NetFramework48 or Samples/Net8Plus.

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Customers')
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id          INT IDENTITY(1,1) PRIMARY KEY,
        Name        NVARCHAR(200)   NOT NULL,
        Email       NVARCHAR(320)   NOT NULL,
        CreatedUtc  DATETIME2       NOT NULL CONSTRAINT DF_Customers_CreatedUtc DEFAULT (SYSUTCDATETIME())
    );
END
GO

-- Plain scalar: used by ExecuteScalarAsync.
CREATE OR ALTER PROCEDURE dbo.usp_GetCustomerCount
AS
BEGIN
    SET NOCOUNT ON;
    SELECT COUNT(*) FROM dbo.Customers;
END
GO

-- Query with a result set: used by ExecuteQueryAsync.
CREATE OR ALTER PROCEDURE dbo.usp_GetCustomersByName
    @NamePattern NVARCHAR(200)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, Name, Email, CreatedUtc
    FROM dbo.Customers
    WHERE Name LIKE @NamePattern
    ORDER BY Name;
END
GO

-- Output parameter + explicit RETURN value: used by ExecuteNonQueryAsync.
-- @NewCustomerId is filled in by SQL Server; the RETURN value signals success (0) vs
-- "duplicate email" (1) without throwing an exception for an expected business case.
CREATE OR ALTER PROCEDURE dbo.usp_InsertCustomer
    @Name           NVARCHAR(200),
    @Email          NVARCHAR(320),
    @NewCustomerId  INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS (SELECT 1 FROM dbo.Customers WHERE Email = @Email)
    BEGIN
        SET @NewCustomerId = NULL;
        RETURN 1; -- duplicate email
    END

    INSERT INTO dbo.Customers (Name, Email) VALUES (@Name, @Email);
    SET @NewCustomerId = CAST(SCOPE_IDENTITY() AS INT);
    RETURN 0;
END
GO
