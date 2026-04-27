DECLARE @tableName SYSNAME = N'dbo.[User]';
DECLARE @tableObjectId INT = OBJECT_ID(@tableName);

IF @tableObjectId IS NULL
BEGIN
    PRINT 'Table dbo.[User] was not found.';
    RETURN;
END;

DECLARE @dropConstraintSql NVARCHAR(MAX) = N'';
DECLARE @dropIndexSql NVARCHAR(MAX) = N'';

;WITH EmailUniqueConstraints AS
(
    SELECT kc.name AS ConstraintName
    FROM sys.key_constraints kc
    INNER JOIN sys.index_columns ic
        ON ic.object_id = kc.parent_object_id
        AND ic.index_id = kc.unique_index_id
    INNER JOIN sys.columns c
        ON c.object_id = ic.object_id
        AND c.column_id = ic.column_id
    WHERE kc.parent_object_id = @tableObjectId
      AND kc.[type] = 'UQ'
      AND c.name = 'Email'
)
SELECT @dropConstraintSql = STRING_AGG(
    N'ALTER TABLE ' + @tableName + N' DROP CONSTRAINT [' + ConstraintName + N'];',
    CHAR(10))
FROM EmailUniqueConstraints;

IF @dropConstraintSql IS NOT NULL AND LEN(@dropConstraintSql) > 0
BEGIN
    EXEC sp_executesql @dropConstraintSql;
    PRINT 'Dropped unique constraints on Email.';
END;

;WITH EmailUniqueIndexes AS
(
    SELECT i.name AS IndexName
    FROM sys.indexes i
    INNER JOIN sys.index_columns ic
        ON ic.object_id = i.object_id
        AND ic.index_id = i.index_id
    INNER JOIN sys.columns c
        ON c.object_id = ic.object_id
        AND c.column_id = ic.column_id
    WHERE i.object_id = @tableObjectId
      AND i.is_unique = 1
      AND i.is_primary_key = 0
      AND i.is_unique_constraint = 0
      AND c.name = 'Email'
)
SELECT @dropIndexSql = STRING_AGG(
    N'DROP INDEX [' + IndexName + N'] ON ' + @tableName + N';',
    CHAR(10))
FROM EmailUniqueIndexes;

IF @dropIndexSql IS NOT NULL AND LEN(@dropIndexSql) > 0
BEGIN
    EXEC sp_executesql @dropIndexSql;
    PRINT 'Dropped unique indexes on Email.';
END;

IF (@dropConstraintSql IS NULL OR LEN(@dropConstraintSql) = 0)
   AND (@dropIndexSql IS NULL OR LEN(@dropIndexSql) = 0)
BEGIN
    PRINT 'No unique Email constraints/indexes were found.';
END;
