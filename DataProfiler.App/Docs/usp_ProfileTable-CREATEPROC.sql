USE IRIS_Dev;

CREATE OR ALTER PROCEDURE dbo.usp_ProfileTable
(
		@TableName sysname
		,@SamplePercent decimal(5,2) = 100
)
AS
BEGIN
    SET NOCOUNT ON;
	SET ANSI_WARNINGS OFF;

    BEGIN TRY

        DECLARE
              @SchemaName sysname
            , @ObjectName sysname
            , @ObjectID int
			,@SourceSQL nvarchar(max);

        -- Parse table name
        SELECT
              @SchemaName = ISNULL(PARSENAME(@TableName,2),'dbo')
            , @ObjectName = PARSENAME(@TableName,1);


        SET @ObjectID =
            OBJECT_ID
            (
                QUOTENAME(@SchemaName)
                + '.'
                + QUOTENAME(@ObjectName)
            );


        IF @ObjectID IS NULL
        BEGIN
            RAISERROR('Table not found.',16,1);
            RETURN;
        END;


        /*
            Final result container.

            One row per source column.
            Schema information comes first.
            Profile columns will be added later.
        */

        CREATE TABLE #Profile
        (
              OrdinalPosition int
            , ColumnName sysname
            , DataType sysname
            , MaxLength int
            , PrecisionValue int
            , ScaleValue int
            , IsNullable bit
            , IsIdentity bit
            , IsComputed bit
            , ColumnCollation sysname NULL

            -- Common profile statistics
            , RowsProfiled bigint NULL
            , NullCount bigint NULL
            , PercentNull decimal(9,4) NULL
            , DistinctCount bigint NULL
            , DistinctPercent decimal(9,4) NULL

			-- Numeric profile statistics
			, MinValue varchar(100) NULL
			, MaxValue varchar(100) NULL
			, AverageValue decimal(18,4) NULL
			, StdDeviation decimal(18,4) NULL

			 -- Character statistics
			 , MinLength int NULL
			, MaxLengthObserved int NULL
			, AverageLength decimal(18,4) NULL
			, EmptyStringCount bigint NULL

			-- Date/Time statistics
			, MinDateValue datetime2 NULL
			, MaxDateValue datetime2 NULL



			, ProfileNote varchar(200) NULL
        );


        INSERT INTO #Profile
        (
              OrdinalPosition
            , ColumnName
            , DataType
            , MaxLength
            , PrecisionValue
            , ScaleValue
            , IsNullable
            , IsIdentity
            , IsComputed
            , ColumnCollation
        )

        SELECT
              c.column_id
            , c.name
            , t.name
            , c.max_length
            , c.precision
            , c.scale
            , c.is_nullable
            , c.is_identity
            , c.is_computed
            , c.collation_name

        FROM sys.columns c

        INNER JOIN sys.types t
            ON c.user_type_id = t.user_type_id

        WHERE c.object_id = @ObjectID;


  /*
    Populate profile statistics here
*/

        DECLARE 
              @ColumnName sysname
            , @SQL nvarchar(max);


        DECLARE ColumnCursor CURSOR LOCAL FAST_FORWARD FOR
		SELECT ColumnName
		FROM #Profile
		WHERE DataType NOT IN
		(
			'xml',
			'text',
			'ntext',
			'image'
		)
		ORDER BY OrdinalPosition;

        OPEN ColumnCursor;

        FETCH NEXT FROM ColumnCursor INTO @ColumnName;


        WHILE @@FETCH_STATUS = 0
        BEGIN

            SET @SQL =
            N'
            UPDATE P
            SET
                  RowsProfiled = X.TotalRows
                , NullCount = X.NullCount
                , PercentNull =
                    CASE 
                        WHEN X.TotalRows = 0 THEN 0
                        ELSE X.NullCount * 100.0 / X.TotalRows
                    END
                , DistinctCount = X.DistinctCount
                , DistinctPercent =
                    CASE 
                        WHEN X.TotalRows = 0 THEN 0
                        ELSE X.DistinctCount * 100.0 / X.TotalRows
                    END
            FROM #Profile P
            CROSS APPLY
            (
                SELECT
                      TotalRows = COUNT_BIG(*)
                    , NullCount =
                        SUM
                        (
                            CASE
                                WHEN ' + QUOTENAME(@ColumnName) + ' IS NULL
                                THEN 1
                                ELSE 0
                            END
                        )
                    , DistinctCount =
                        COUNT_BIG(DISTINCT ' + QUOTENAME(@ColumnName) + ')
                FROM '
                + QUOTENAME(@SchemaName)
                + '.'
                + QUOTENAME(@ObjectName) +
            '
            ) X
            WHERE P.ColumnName = @ColName;';


            EXEC sys.sp_executesql
                  @SQL
                , N'@ColName sysname'
                , @ColName = @ColumnName;


            FETCH NEXT FROM ColumnCursor INTO @ColumnName;

        END;


        CLOSE ColumnCursor;
        DEALLOCATE ColumnCursor;

        /*
            Populate numeric statistics
        */

        DECLARE NumericCursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT ColumnName
            FROM #Profile
            WHERE DataType IN
            (
                'tinyint',
                'smallint',
                'int',
                'bigint',
                'decimal',
                'numeric',
                'money',
                'smallmoney',
                'float',
                'real'
            )
            ORDER BY OrdinalPosition;


        OPEN NumericCursor;

        FETCH NEXT FROM NumericCursor INTO @ColumnName;


        WHILE @@FETCH_STATUS = 0
        BEGIN

            SET @SQL =
            N'
            UPDATE P
            SET
                  MinValue = CONVERT(varchar(100), X.MinValue)
                , MaxValue = CONVERT(varchar(100), X.MaxValue)
                , AverageValue = X.AverageValue
                , StdDeviation = X.StdDeviation

            FROM #Profile P

            CROSS APPLY
            (
                SELECT
                      MinValue = MIN(' + QUOTENAME(@ColumnName) + ')
                    , MaxValue = MAX(' + QUOTENAME(@ColumnName) + ')
                    , AverageValue = AVG(CONVERT(decimal(18,4),' + QUOTENAME(@ColumnName) + '))
                    , StdDeviation = STDEV(CONVERT(float,' + QUOTENAME(@ColumnName) + '))

                FROM '
                + QUOTENAME(@SchemaName)
                + '.'
                + QUOTENAME(@ObjectName) +
            '
            ) X

            WHERE P.ColumnName = @ColName;';


            EXEC sys.sp_executesql
                  @SQL
                , N'@ColName sysname'
                , @ColName = @ColumnName;


            FETCH NEXT FROM NumericCursor INTO @ColumnName;

        END;


        CLOSE NumericCursor;
        DEALLOCATE NumericCursor;

		-- Character statistics will go here
		        /*
            Populate character statistics
        */

        DECLARE CharacterCursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT ColumnName
            FROM #Profile
            WHERE DataType IN
            (
                'char',
                'varchar',
                'nchar',
                'nvarchar'
            )
            ORDER BY OrdinalPosition;


        OPEN CharacterCursor;

        FETCH NEXT FROM CharacterCursor INTO @ColumnName;


        WHILE @@FETCH_STATUS = 0
        BEGIN

            SET @SQL =
            N'
            UPDATE P
            SET
                  MinLength = X.MinLength
                , MaxLengthObserved = X.MaxLengthObserved
                , AverageLength = X.AverageLength
                , EmptyStringCount = X.EmptyStringCount

            FROM #Profile P

            CROSS APPLY
            (
                SELECT
                      MinLength = MIN(LEN(' + QUOTENAME(@ColumnName) + '))
                    , MaxLengthObserved = MAX(LEN(' + QUOTENAME(@ColumnName) + '))
                    , AverageLength = AVG(CONVERT(decimal(18,4),LEN(' + QUOTENAME(@ColumnName) + ')))
                    , EmptyStringCount =
                        SUM
                        (
                            CASE
                                WHEN ' + QUOTENAME(@ColumnName) + ' = ''''
                                THEN 1
                                ELSE 0
                            END
                        )

                FROM '
                + QUOTENAME(@SchemaName)
                + '.'
                + QUOTENAME(@ObjectName) +
            '
            ) X

            WHERE P.ColumnName = @ColName;';


            EXEC sys.sp_executesql
                  @SQL
                , N'@ColName sysname'
                , @ColName = @ColumnName;


            FETCH NEXT FROM CharacterCursor INTO @ColumnName;

        END;


        CLOSE CharacterCursor;
        DEALLOCATE CharacterCursor;

        /*
            Populate date/time statistics
        */

        DECLARE DateCursor CURSOR LOCAL FAST_FORWARD FOR
            SELECT ColumnName
            FROM #Profile
            WHERE DataType IN
            (
                'date',
                'datetime',
                'datetime2',
                'smalldatetime',
                'datetimeoffset',
                'time'
            )
            ORDER BY OrdinalPosition;


        OPEN DateCursor;

        FETCH NEXT FROM DateCursor INTO @ColumnName;


        WHILE @@FETCH_STATUS = 0
        BEGIN

            SET @SQL =
            N'
            UPDATE P
            SET
                  MinDateValue = X.MinDateValue
                , MaxDateValue = X.MaxDateValue

            FROM #Profile P

            CROSS APPLY
            (
                SELECT
                      MinDateValue = MIN(' + QUOTENAME(@ColumnName) + ')
                    , MaxDateValue = MAX(' + QUOTENAME(@ColumnName) + ')

                FROM '
                + QUOTENAME(@SchemaName)
                + '.'
                + QUOTENAME(@ObjectName) +
            '
            ) X

            WHERE P.ColumnName = @ColName;';


            EXEC sys.sp_executesql
                  @SQL
                , N'@ColName sysname'
                , @ColName = @ColumnName;


            FETCH NEXT FROM DateCursor INTO @ColumnName;

        END;


        CLOSE DateCursor;
        DEALLOCATE DateCursor;
		SELECT @TableName AS TableName;
		 SELECT
			  -- Schema information (always first)
			  OrdinalPosition
			, ColumnName
			, DataType
			, MaxLength
			, PrecisionValue
			, ScaleValue
			, IsNullable
			, IsIdentity
			, IsComputed
			, ColumnCollation

			  -- Common profile statistics
			, RowsProfiled
			, NullCount
			, PercentNull
			, DistinctCount
			, DistinctPercent

			-- Numeric profile statistics
			, MinValue
			, MaxValue
			, AverageValue
			, StdDeviation

			 -- Character statistics
			, MinLength
			, MaxLengthObserved
			, AverageLength
			, EmptyStringCount

			-- Date/Time statistics
			, MinDateValue
			, MaxDateValue

			, ProfileNote
		FROM #Profile

ORDER BY OrdinalPosition;


    END TRY

    BEGIN CATCH
        THROW;
    END CATCH

END;
GO