-- =============================================
-- Improved Table Profiling Stored Procedure
-- Version: 2.0
-- Description: Comprehensive schema and data profiling
-- =============================================

CREATE OR ALTER PROCEDURE dbo.usp_ProfileTable
(
	@TableName sysname,
	@SamplePercent decimal(5,2) = 100,
	@IncludeFrequencyAnalysis bit = 1,
	@MaxFrequencyValues int = 1
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
			, @SourceSQL nvarchar(max)
			, @TotalRows bigint;

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
			-- Core Column Identity (always first - matches sys.columns order)
			  OrdinalPosition int
			, ColumnName sysname
			, DataType sysname

			-- Data Type Attributes (standard sys.columns metadata)
			, MaxLength int
			, PrecisionValue int
			, ScaleValue int
			, ColumnCollation sysname NULL

			-- Common Column Properties (standard flags)
			, IsNullable bit
			, DefaultValue nvarchar(max) NULL

			-- Special Column Types (identity, computed)
			, IsIdentity bit
			, IdentitySeed bigint NULL
			, IdentityIncrement bigint NULL
			, IsComputed bit
			, ComputedDefinition nvarchar(max) NULL

			-- Keys and Indexes (relational metadata)
			, IsPrimaryKey bit
			, IsIndexed bit
			, IsForeignKey bit

			-- Common Profile Statistics (applies to ALL columns)
			, RowsProfiled bigint NULL
			, NullCount bigint NULL
			, PercentNull decimal(9,4) NULL
			, DistinctCount bigint NULL
			, DistinctPercent decimal(9,4) NULL

			-- Frequency Analysis (common - applies to most columns)
			, MostFrequentValue nvarchar(max) NULL
			, MostFrequentCount bigint NULL
			, MostFrequentPercent decimal(9,4) NULL

			-- Numeric Profile Statistics (numeric data types only)
			, MinValue varchar(100) NULL
			, MaxValue varchar(100) NULL
			, AverageValue decimal(18,4) NULL
			, StdDeviation decimal(18,4) NULL

			-- Character Profile Statistics (string data types only)
			, MinLength int NULL
			, MaxLengthObserved int NULL
			, AverageLength decimal(18,4) NULL
			, EmptyStringCount bigint NULL
			, WhitespaceOnlyCount bigint NULL

			-- Date/Time Profile Statistics (date/time data types only)
			, MinDateValue datetime2 NULL
			, MaxDateValue datetime2 NULL
			, DateRangeDays int NULL

			-- Profile Metadata
			, ProfileNote varchar(200) NULL
		);

		/*
			Populate schema information with enhanced metadata
		*/

		INSERT INTO #Profile
		(
			-- Core Column Identity
			  OrdinalPosition
			, ColumnName
			, DataType

			-- Data Type Attributes
			, MaxLength
			, PrecisionValue
			, ScaleValue
			, ColumnCollation

			-- Common Column Properties
			, IsNullable
			, DefaultValue

			-- Special Column Types
			, IsIdentity
			, IdentitySeed
			, IdentityIncrement
			, IsComputed
			, ComputedDefinition

			-- Keys and Indexes
			, IsPrimaryKey
			, IsIndexed
			, IsForeignKey
		)
		SELECT
			-- Core Column Identity
			  c.column_id
			, c.name
			, t.name

			-- Data Type Attributes
			, c.max_length
			, c.precision
			, c.scale
			, c.collation_name

			-- Common Column Properties
			, c.is_nullable
			, dc.definition -- DefaultValue

			-- Special Column Types
			, c.is_identity
			, CONVERT(bigint, ic.seed_value) -- IdentitySeed
			, CONVERT(bigint, ic.increment_value) -- IdentityIncrement
			, c.is_computed
			, cc.definition -- ComputedDefinition

			-- Keys and Indexes
			, IsPrimaryKey = CAST(
				CASE WHEN EXISTS (
					SELECT 1 
					FROM sys.index_columns ic2
					INNER JOIN sys.indexes i 
						ON ic2.object_id = i.object_id 
						AND ic2.index_id = i.index_id
					WHERE ic2.object_id = c.object_id 
						AND ic2.column_id = c.column_id
						AND i.is_primary_key = 1
				) THEN 1 ELSE 0 END AS bit)

			, IsIndexed = CAST(
				CASE WHEN EXISTS (
					SELECT 1 
					FROM sys.index_columns ic2
					WHERE ic2.object_id = c.object_id 
						AND ic2.column_id = c.column_id
				) THEN 1 ELSE 0 END AS bit)

			, IsForeignKey = CAST(
				CASE WHEN EXISTS (
					SELECT 1 
					FROM sys.foreign_key_columns fkc
					WHERE fkc.parent_object_id = c.object_id 
						AND fkc.parent_column_id = c.column_id
				) THEN 1 ELSE 0 END AS bit)

		FROM sys.columns c
		INNER JOIN sys.types t
			ON c.user_type_id = t.user_type_id
		LEFT JOIN sys.default_constraints dc 
			ON dc.parent_object_id = c.object_id 
			AND dc.parent_column_id = c.column_id
		LEFT JOIN sys.identity_columns ic 
			ON ic.object_id = c.object_id 
			AND ic.column_id = c.column_id
		LEFT JOIN sys.computed_columns cc 
			ON cc.object_id = c.object_id 
			AND cc.column_id = c.column_id

		WHERE c.object_id = @ObjectID
		ORDER BY c.column_id;

		/*
			Get total row count for the table
		*/
		SET @SourceSQL = 
			N'SELECT @RowCount = COUNT_BIG(*) FROM ' 
			+ QUOTENAME(@SchemaName) + '.' + QUOTENAME(@ObjectName);

		EXEC sp_executesql 
			@SourceSQL, 
			N'@RowCount bigint OUTPUT', 
			@RowCount = @TotalRows OUTPUT;

		/*
			Populate common profile statistics (NULL, Distinct)
		*/

		DECLARE 
			  @ColumnName sysname
			, @SQL nvarchar(max)
			, @DataType sysname;

		DECLARE ColumnCursor CURSOR LOCAL FAST_FORWARD FOR
		SELECT ColumnName, DataType
		FROM #Profile
		WHERE DataType NOT IN ('xml', 'text', 'ntext', 'image', 'geography', 'geometry', 'hierarchyid')
		ORDER BY OrdinalPosition;

		OPEN ColumnCursor;
		FETCH NEXT FROM ColumnCursor INTO @ColumnName, @DataType;

		WHILE @@FETCH_STATUS = 0
		BEGIN

			SET @SQL =
			N'
			UPDATE P
			SET
				  RowsProfiled = @TotalRows
				, NullCount = X.NullCount
				, PercentNull =
					CASE 
						WHEN @TotalRows = 0 THEN 0
						ELSE X.NullCount * 100.0 / @TotalRows
					END
				, DistinctCount = X.DistinctCount
				, DistinctPercent =
					CASE 
						WHEN @TotalRows = 0 THEN 0
						ELSE X.DistinctCount * 100.0 / @TotalRows
					END
			FROM #Profile P
			CROSS APPLY
			(
				SELECT
					  NullCount =
						SUM
						(
							CASE
								WHEN ' + QUOTENAME(@ColumnName) + ' IS NULL
								THEN 1
								ELSE 0
							END
						)
					, DistinctCount =
						COUNT(DISTINCT ' + QUOTENAME(@ColumnName) + ')
				FROM '
				+ QUOTENAME(@SchemaName)
				+ '.'
				+ QUOTENAME(@ObjectName) + '
			) X
			WHERE P.ColumnName = @ColName;';

			EXEC sys.sp_executesql
				  @SQL
				, N'@ColName sysname, @TotalRows bigint'
				, @ColName = @ColumnName
				, @TotalRows = @TotalRows;

			FETCH NEXT FROM ColumnCursor INTO @ColumnName, @DataType;

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
				+ QUOTENAME(@ObjectName) + '
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

		/*
			Populate character/string statistics
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
				, WhitespaceOnlyCount = X.WhitespaceOnlyCount

			FROM #Profile P
			CROSS APPLY
			(
				SELECT
					  MinLength = MIN(LEN(' + QUOTENAME(@ColumnName) + '))
					, MaxLengthObserved = MAX(LEN(' + QUOTENAME(@ColumnName) + '))
					, AverageLength = AVG(CONVERT(decimal(18,4), LEN(' + QUOTENAME(@ColumnName) + ')))
					, EmptyStringCount = SUM(CASE WHEN ' + QUOTENAME(@ColumnName) + ' = '''' THEN 1 ELSE 0 END)
					, WhitespaceOnlyCount = SUM(
						CASE 
							WHEN ' + QUOTENAME(@ColumnName) + ' IS NOT NULL
								AND LEN(' + QUOTENAME(@ColumnName) + ') > 0
								AND LTRIM(RTRIM(' + QUOTENAME(@ColumnName) + ')) = ''''
							THEN 1 
							ELSE 0 
						END)
				FROM '
				+ QUOTENAME(@SchemaName)
				+ '.'
				+ QUOTENAME(@ObjectName) + '
				WHERE ' + QUOTENAME(@ColumnName) + ' IS NOT NULL
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
				, DateRangeDays = DATEDIFF(DAY, X.MinDateValue, X.MaxDateValue)

			FROM #Profile P
			CROSS APPLY
			(
				SELECT
					  MinDateValue = MIN(' + QUOTENAME(@ColumnName) + ')
					, MaxDateValue = MAX(' + QUOTENAME(@ColumnName) + ')
				FROM '
				+ QUOTENAME(@SchemaName)
				+ '.'
				+ QUOTENAME(@ObjectName) + '
				WHERE ' + QUOTENAME(@ColumnName) + ' IS NOT NULL
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

		/*
			Populate frequency analysis (most frequent values)
			Only if requested and for appropriate data types
		*/

		IF @IncludeFrequencyAnalysis = 1
		BEGIN

			DECLARE FrequencyCursor CURSOR LOCAL FAST_FORWARD FOR
				SELECT ColumnName, DataType
				FROM #Profile
				WHERE DataType NOT IN 
				(
					'xml', 'text', 'ntext', 'image', 
					'geography', 'geometry', 'hierarchyid',
					'varbinary', 'binary'
				)
				-- Skip very high cardinality columns (distinct % > 95%)
				AND (DistinctPercent IS NULL OR DistinctPercent < 95.0)
				ORDER BY OrdinalPosition;

			OPEN FrequencyCursor;
			FETCH NEXT FROM FrequencyCursor INTO @ColumnName, @DataType;

			WHILE @@FETCH_STATUS = 0
			BEGIN

				SET @SQL =
				N'
				;WITH FrequencyAnalysis AS (
					SELECT 
						[ColumnValue] = CONVERT(nvarchar(max), ' + QUOTENAME(@ColumnName) + '),
						[FreqCount] = COUNT_BIG(*)
					FROM ' + QUOTENAME(@SchemaName) + '.' + QUOTENAME(@ObjectName) + '
					WHERE ' + QUOTENAME(@ColumnName) + ' IS NOT NULL
					GROUP BY ' + QUOTENAME(@ColumnName) + '
				),
				RankedFrequency AS (
					SELECT TOP 1
						[ColumnValue],
						[FreqCount],
						[FreqPercent] = [FreqCount] * 100.0 / @TotalRows
					FROM FrequencyAnalysis
					ORDER BY [FreqCount] DESC, [ColumnValue]
				)
				UPDATE P
				SET 
					MostFrequentValue = R.ColumnValue,
					MostFrequentCount = R.FreqCount,
					MostFrequentPercent = R.FreqPercent
				FROM #Profile P
				CROSS JOIN RankedFrequency R
				WHERE P.ColumnName = @ColName;';

				EXEC sys.sp_executesql
					  @SQL
					, N'@ColName sysname, @TotalRows bigint'
					, @ColName = @ColumnName
					, @TotalRows = @TotalRows;

				FETCH NEXT FROM FrequencyCursor INTO @ColumnName, @DataType;

			END;

			CLOSE FrequencyCursor;
			DEALLOCATE FrequencyCursor;

		END;

		/*
			Return results
		*/

		-- Table metadata
		SELECT 
			  @SchemaName AS SchemaName
			, @ObjectName AS TableName
			, @TotalRows AS TotalRows
			, @SamplePercent AS SamplePercent;

		-- Column profiles
		 SELECT
			-- Core Column Identity
			  OrdinalPosition
			, ColumnName
			, DataType

			-- Data Type Attributes
			, MaxLength
			, PrecisionValue
			, ScaleValue
			, ColumnCollation

			-- Common Column Properties
			, IsNullable
			, DefaultValue

			-- Special Column Types
			, IsIdentity
			, IdentitySeed
			, IdentityIncrement
			, IsComputed
			, ComputedDefinition

			-- Keys and Indexes
			, IsPrimaryKey
			, IsIndexed
			, IsForeignKey

			-- Common Profile Statistics
			, RowsProfiled
			, NullCount
			, PercentNull
			, DistinctCount
			, DistinctPercent

			-- Frequency Analysis
			, MostFrequentValue
			, MostFrequentCount
			, MostFrequentPercent

			-- Numeric Profile Statistics
			, MinValue
			, MaxValue
			, AverageValue
			, StdDeviation

			-- Character Profile Statistics
			, MinLength
			, MaxLengthObserved
			, AverageLength
			, EmptyStringCount
			, WhitespaceOnlyCount

			-- Date/Time Profile Statistics
			, MinDateValue
			, MaxDateValue
			, DateRangeDays

			-- Profile Metadata
			, ProfileNote
		FROM #Profile
		ORDER BY OrdinalPosition;

	END TRY

	BEGIN CATCH

		-- Return error information
		SELECT 
			  ERROR_NUMBER() AS ErrorNumber
			, ERROR_MESSAGE() AS ErrorMessage
			, ERROR_LINE() AS ErrorLine;

		THROW;

	END CATCH

END;
GO

/*
	Example Usage:

	-- Basic profile
	EXEC dbo.usp_ProfileTable @TableName = 'Sales.SalesOrderDetail';

	-- Profile with sampling
	EXEC dbo.usp_ProfileTable 
		@TableName = 'Sales.SalesOrderDetail',
		@SamplePercent = 10.0;

	-- Profile without frequency analysis (faster)
	EXEC dbo.usp_ProfileTable 
		@TableName = 'Sales.SalesOrderDetail',
		@IncludeFrequencyAnalysis = 0;
*/
