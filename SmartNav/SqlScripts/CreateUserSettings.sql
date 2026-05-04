IF OBJECT_ID(N'dbo.UserSettings', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[UserSettings]
    (
        [Id] INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        [UserID] INT NOT NULL,
        [AiAggressiveness] INT NOT NULL CONSTRAINT [DF_UserSettings_AiAggressiveness] DEFAULT (3),
        [AlwaysShowRouteExplanation] BIT NOT NULL CONSTRAINT [DF_UserSettings_AlwaysShowRouteExplanation] DEFAULT (1),
        [AlternativeRoutesCount] INT NOT NULL CONSTRAINT [DF_UserSettings_AlternativeRoutesCount] DEFAULT (2),
        [UseHistoryPersonalization] BIT NOT NULL CONSTRAINT [DF_UserSettings_UseHistoryPersonalization] DEFAULT (1),
        [Theme] NVARCHAR(20) NOT NULL CONSTRAINT [DF_UserSettings_Theme] DEFAULT (N'system'),
        [MapStyle] NVARCHAR(20) NOT NULL CONSTRAINT [DF_UserSettings_MapStyle] DEFAULT (N'standard'),
        [DistanceUnit] NVARCHAR(10) NOT NULL CONSTRAINT [DF_UserSettings_DistanceUnit] DEFAULT (N'km'),
        [TimeFormat] NVARCHAR(10) NOT NULL CONSTRAINT [DF_UserSettings_TimeFormat] DEFAULT (N'24h'),
        [ChipDensity] NVARCHAR(20) NOT NULL CONSTRAINT [DF_UserSettings_ChipDensity] DEFAULT (N'comfortable'),
        [LargeText] BIT NOT NULL CONSTRAINT [DF_UserSettings_LargeText] DEFAULT (0),
        [HighContrast] BIT NOT NULL CONSTRAINT [DF_UserSettings_HighContrast] DEFAULT (0),
        [StoreTrips] BIT NOT NULL CONSTRAINT [DF_UserSettings_StoreTrips] DEFAULT (1),
        [StoreRatings] BIT NOT NULL CONSTRAINT [DF_UserSettings_StoreRatings] DEFAULT (1),
        [StoreStations] BIT NOT NULL CONSTRAINT [DF_UserSettings_StoreStations] DEFAULT (1),
        [ConsentLocationHistory] BIT NOT NULL CONSTRAINT [DF_UserSettings_ConsentLocationHistory] DEFAULT (0),
        [ConsentAiTraining] BIT NOT NULL CONSTRAINT [DF_UserSettings_ConsentAiTraining] DEFAULT (0),
        [UpdatedAt] DATETIME2 NOT NULL CONSTRAINT [DF_UserSettings_UpdatedAt] DEFAULT (SYSUTCDATETIME()),
        CONSTRAINT [FK_UserSettings_User_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[User]([Id]) ON DELETE CASCADE,
        CONSTRAINT [UQ_UserSettings_UserID] UNIQUE ([UserID]),
        CONSTRAINT [CK_UserSettings_AiAggressiveness] CHECK ([AiAggressiveness] BETWEEN 1 AND 5),
        CONSTRAINT [CK_UserSettings_AlternativeRoutesCount] CHECK ([AlternativeRoutesCount] IN (1,2,3)),
        CONSTRAINT [CK_UserSettings_Theme] CHECK ([Theme] IN (N'light', N'dark', N'system')),
        CONSTRAINT [CK_UserSettings_MapStyle] CHECK ([MapStyle] IN (N'standard', N'satellite', N'terrain')),
        CONSTRAINT [CK_UserSettings_DistanceUnit] CHECK ([DistanceUnit] IN (N'km', N'mi')),
        CONSTRAINT [CK_UserSettings_TimeFormat] CHECK ([TimeFormat] IN (N'12h', N'24h')),
        CONSTRAINT [CK_UserSettings_ChipDensity] CHECK ([ChipDensity] IN (N'compact', N'comfortable'))
    );
END;
