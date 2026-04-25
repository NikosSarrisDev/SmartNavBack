IF OBJECT_ID(N'[dbo].[AdminActionLog]', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[AdminActionLog]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [AdminUserId] INT NOT NULL,
        [TargetUserId] INT NULL,
        [ActionType] NVARCHAR(80) NOT NULL,
        [Details] NVARCHAR(800) NULL,
        [CreatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_AdminActionLog_CreatedAt] DEFAULT (SYSUTCDATETIME())
    );

    CREATE INDEX [IX_AdminActionLog_AdminUserId_CreatedAt]
        ON [dbo].[AdminActionLog] ([AdminUserId], [CreatedAt] DESC);

    CREATE INDEX [IX_AdminActionLog_TargetUserId]
        ON [dbo].[AdminActionLog] ([TargetUserId]);
END;
