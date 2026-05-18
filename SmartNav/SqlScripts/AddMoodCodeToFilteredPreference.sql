IF COL_LENGTH('dbo.FilteredPreference', 'MoodCode') IS NULL
BEGIN
    ALTER TABLE dbo.FilteredPreference
    ADD MoodCode NVARCHAR(60) NULL;
END
GO
