IF OBJECT_ID(N'dbo.FamiliarizationCheck', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FamiliarizationCheck
    (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        Competitor NVARCHAR(120) NOT NULL,
        Note NVARCHAR(200) NOT NULL,
        CreatedAt DATETIME2(0) NOT NULL CONSTRAINT DF_FamiliarizationCheck_CreatedAt DEFAULT SYSUTCDATETIME()
    );
END;
GO

IF NOT EXISTS (SELECT 1 FROM dbo.FamiliarizationCheck)
BEGIN
    INSERT INTO dbo.FamiliarizationCheck (Competitor, Note)
    VALUES
        (SUSER_SNAME(), N'Login that ran the familiarization script.'),
        (N'familiarization', N'Seed row written by sql/familiarization.sql.');
END;
GO

SELECT * FROM dbo.FamiliarizationCheck ORDER BY Id;
GO
