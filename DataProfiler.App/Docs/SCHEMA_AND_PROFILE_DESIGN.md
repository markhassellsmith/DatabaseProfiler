# Schema and Profile Information Design
## Comprehensive Analysis and Recommendations

---

## Current State Analysis

### **Stored Procedure (usp_ProfileTable-CREATEPROC.sql) Provides:**

#### Schema Fields:
- ✅ OrdinalPosition
- ✅ ColumnName
- ✅ DataType
- ✅ MaxLength
- ✅ PrecisionValue
- ✅ ScaleValue
- ✅ IsNullable
- ✅ IsIdentity
- ✅ IsComputed
- ✅ ColumnCollation

#### Profile Statistics:
**Common:**
- ✅ RowsProfiled
- ✅ NullCount
- ✅ PercentNull
- ✅ DistinctCount
- ✅ DistinctPercent

**Numeric:**
- ✅ MinValue
- ✅ MaxValue
- ✅ AverageValue
- ✅ StdDeviation

**Character:**
- ✅ MinLength
- ✅ MaxLengthObserved
- ✅ AverageLength
- ✅ EmptyStringCount

**Date/Time:**
- ✅ MinDateValue
- ✅ MaxDateValue

### **Current Application Provides:**

#### Schema Fields (ColumnBrowser):
- ✅ Ordinal
- ✅ Name
- ✅ DataType
- ✅ Length (display only)
- ✅ Default
- ✅ IsNullable
- ✅ IsPrimaryKey
- ✅ IsIndexed
- ✅ IsForeignKey

#### Profile Statistics (Profiling page):
- ✅ Ordinal
- ✅ Name
- ✅ DataType
- ✅ NullCount
- ✅ NullPercent (displayed as "Null %")
- ✅ CountDistinct (displayed as "DistinctCount")
- ✅ MinValue
- ✅ MaxValue
- ✅ AverageValue
- ✅ StandardDeviation
- ✅ MostFrequentValue
- ✅ MostFrequentCount

---

## Gap Analysis

### **Missing in Stored Proc (Present in App):**
1. ❌ **Default constraint value** - Application shows this
2. ❌ **IsPrimaryKey** - Application shows this
3. ❌ **IsIndexed** - Application shows this
4. ❌ **IsForeignKey** - Application shows this
5. ❌ **MostFrequentValue** - Application calculates this separately
6. ❌ **MostFrequentCount** - Application calculates this separately

### **Missing in App (Present in Stored Proc):**
1. ❌ **RowsProfiled** - Total rows analyzed
2. ❌ **DistinctPercent** - Distinct values as % of total
3. ❌ **MaxLength** - Actual byte length (different from Length display)
4. ❌ **Precision** - For numeric types
5. ❌ **Scale** - For numeric types
6. ❌ **IsIdentity** - Identity column flag
7. ❌ **IsComputed** - Computed column flag
8. ❌ **Collation** - Character collation
9. ❌ **MinLength** - Minimum string length
10. ❌ **MaxLengthObserved** - Maximum actual string length
11. ❌ **AverageLength** - Average string length
12. ❌ **EmptyStringCount** - Count of empty strings
13. ❌ **MinDateValue** - Earliest date
14. ❌ **MaxDateValue** - Latest date

---

## Recommended Unified Design

### **Complete Schema Information (Metadata)**

```sql
-- Core Column Identity (always first - matches sys.columns order)
OrdinalPosition         int           -- Column position (1-based)
ColumnName              sysname       -- Column name
DataType                sysname       -- SQL Server data type name

-- Data Type Attributes (standard sys.columns metadata)
MaxLength               int           -- Max length in bytes (from sys.columns)
PrecisionValue          int           -- Numeric precision
ScaleValue              int           -- Numeric scale
ColumnCollation         sysname       -- Collation (for character types)

-- Common Column Properties (standard flags)
IsNullable              bit           -- Allows NULL values
DefaultValue            nvarchar(max) -- Default constraint definition

-- Special Column Types (identity, computed)
IsIdentity              bit           -- Is identity/auto-increment
IdentitySeed            bigint        -- Identity seed value (if IsIdentity = 1)
IdentityIncrement       bigint        -- Identity increment (if IsIdentity = 1)
IsComputed              bit           -- Is computed column
ComputedDefinition      nvarchar(max) -- Computed column formula (if IsComputed = 1)

-- Keys and Indexes (relational metadata)
IsPrimaryKey            bit           -- Part of primary key
IsIndexed               bit           -- Has index (clustered or non-clustered)
IsForeignKey            bit           -- Part of foreign key
```

### **Complete Profile Statistics**

```sql
-- Common Profile Statistics (applies to ALL columns - always calculated first)
RowsProfiled            bigint        -- Total rows analyzed
NullCount               bigint        -- Count of NULL values
PercentNull             decimal(9,4)  -- NULL percentage
DistinctCount           bigint        -- Count of distinct non-NULL values
DistinctPercent         decimal(9,4)  -- Distinct as % of total rows

-- Frequency Analysis (common - applies to most columns)
MostFrequentValue       nvarchar(max) -- Most common value
MostFrequentCount       bigint        -- Occurrences of most frequent value
MostFrequentPercent     decimal(9,4)  -- Most frequent as % of total

-- Numeric Profile Statistics (numeric data types only)
MinValue                varchar(100)  -- Minimum value (as string for display)
MaxValue                varchar(100)  -- Maximum value (as string for display)
AverageValue            decimal(18,4) -- Average (mean)
StdDeviation            decimal(18,4) -- Standard deviation

-- Character Profile Statistics (string data types only)
MinLength               int           -- Minimum string length
MaxLengthObserved       int           -- Maximum actual string length
AverageLength           decimal(18,4) -- Average string length
EmptyStringCount        bigint        -- Count of empty strings ('')
WhitespaceOnlyCount     bigint        -- Count of whitespace-only strings

-- Date/Time Profile Statistics (date/time data types only)
MinDateValue            datetime2     -- Earliest date/time
MaxDateValue            datetime2     -- Latest date/time
DateRangeDays           int           -- Range in days

-- Profile Metadata
ProfileNote             varchar(200)  -- Notes/warnings (e.g., "Sample only", "Skipped")
```

---

## Proposed Improvements to Stored Procedure

### **Schema Enhancements:**

1. ✅ Add **Primary Key detection**:
```sql
, IsPrimaryKey = CAST(
	CASE WHEN EXISTS (
		SELECT 1 FROM sys.index_columns ic
		INNER JOIN sys.indexes i ON ic.object_id = i.object_id AND ic.index_id = i.index_id
		WHERE ic.object_id = c.object_id 
		AND ic.column_id = c.column_id
		AND i.is_primary_key = 1
	) THEN 1 ELSE 0 END AS bit)
```

2. ✅ Add **Index detection**:
```sql
, IsIndexed = CAST(
	CASE WHEN EXISTS (
		SELECT 1 FROM sys.index_columns ic
		WHERE ic.object_id = c.object_id 
		AND ic.column_id = c.column_id
	) THEN 1 ELSE 0 END AS bit)
```

3. ✅ Add **Foreign Key detection**:
```sql
, IsForeignKey = CAST(
	CASE WHEN EXISTS (
		SELECT 1 FROM sys.foreign_key_columns fkc
		WHERE fkc.parent_object_id = c.object_id 
		AND fkc.parent_column_id = c.column_id
	) THEN 1 ELSE 0 END AS bit)
```

4. ✅ Add **Default constraint**:
```sql
, DefaultValue = dc.definition
LEFT JOIN sys.default_constraints dc 
	ON dc.parent_object_id = c.object_id 
	AND dc.parent_column_id = c.column_id
```

5. ✅ Add **Identity properties** (seed/increment):
```sql
, IdentitySeed = CONVERT(bigint, ic.seed_value)
, IdentityIncrement = CONVERT(bigint, ic.increment_value)
LEFT JOIN sys.identity_columns ic 
	ON ic.object_id = c.object_id 
	AND ic.column_id = c.column_id
```

6. ✅ Add **Computed column definition**:
```sql
, ComputedDefinition = cc.definition
LEFT JOIN sys.computed_columns cc 
	ON cc.object_id = c.object_id 
	AND cc.column_id = c.column_id
```

### **Profile Enhancements:**

1. ✅ Add **Most Frequent Value** calculation:
```sql
-- Add to #Profile table
, MostFrequentValue nvarchar(max) NULL
, MostFrequentCount bigint NULL
, MostFrequentPercent decimal(9,4) NULL

-- Implementation (new cursor or CTE):
;WITH FrequencyAnalysis AS (
	SELECT 
		[ColumnValue] = CONVERT(nvarchar(max), [ColumnName]),
		[FreqCount] = COUNT_BIG(*)
	FROM [Schema].[Table]
	WHERE [ColumnName] IS NOT NULL
	GROUP BY [ColumnName]
),
RankedFrequency AS (
	SELECT TOP 1
		[ColumnValue],
		[FreqCount],
		[FreqPercent] = [FreqCount] * 100.0 / SUM([FreqCount]) OVER ()
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
WHERE P.ColumnName = @ColName;
```

2. 🟡 Add **Whitespace detection** for strings:
```sql
, WhitespaceCount = SUM(
	CASE WHEN [ColumnName] LIKE '%[^ ]%' THEN 0
		 WHEN [ColumnName] = '' THEN 0
		 ELSE 1 
	END)
```

3. 🟡 Add **Median calculation** (optional - expensive):
```sql
, MedianValue = (
	SELECT PERCENTILE_CONT(0.5) 
	WITHIN GROUP (ORDER BY [ColumnName])
	OVER ()
)
```

---

## Implementation Priority

### **Phase 1: Critical (Must Have)**
1. ✅ Add PK/Index/FK detection to stored proc
2. ✅ Add Default constraint to stored proc
3. ✅ Add Most Frequent Value/Count to stored proc
4. ✅ Add Precision/Scale to application schema display
5. ✅ Add IsIdentity/IsComputed to application schema display

### **Phase 2: Important (Should Have)**
6. ✅ Add RowsProfiled to application profiling display
7. ✅ Add DistinctPercent to application profiling display
8. ✅ Add EmptyStringCount to application profiling display
9. ✅ Add character length statistics (Min/Max/Avg)
10. ✅ Add date range statistics (Min/Max dates)

### **Phase 3: Nice to Have (Could Have)**
11. 🟡 Add MostFrequentPercent
12. 🟡 Add WhitespaceCount for strings
13. 🟡 Add IdentitySeed/IdentityIncrement display
14. 🟡 Add ComputedDefinition display
15. 🟡 Add Median calculation (very expensive)

---

## Next Steps

1. **Update Stored Procedure** with Phase 1 & 2 enhancements
2. **Test Stored Procedure** on sample tables (small, medium, large)
3. **Update Application Models** to match new schema
4. **Update UI** to display new fields appropriately
5. **Verify Performance** on large tables (with timeout fixes already applied)

---

## Questions for Consideration

1. **Sampling:** Should we always allow sampling for very large tables? (@SamplePercent parameter exists)
2. **Median:** Is median calculation worth the performance cost?
3. **Top N Frequencies:** Should we show top 3-5 most frequent values instead of just 1?
4. **Data Quality:** Should we add more data quality metrics (e.g., pattern analysis, outlier detection)?
5. **Historical Tracking:** Should profile results be stored for trend analysis over time?

