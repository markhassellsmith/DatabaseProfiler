# Quick Reference: Schema & Profile Fields

## Schema Fields (Ordered by standard SQL Server catalog convention)

### Core Column Identity
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **OrdinalPosition** | int | Column position (1-based) | 1 |
| **ColumnName** | sysname | Column name | CustomerID |
| **DataType** | sysname | SQL Server data type | int, varchar, datetime |

### Data Type Attributes
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **MaxLength** | int | Max bytes (sys.columns) | 100 |
| **PrecisionValue** | int | Numeric precision | 18 |
| **ScaleValue** | int | Numeric scale | 2 |
| **ColumnCollation** | sysname | Collation (char columns) | SQL_Latin1_General_CP1_CI_AS |

### Common Column Properties
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **IsNullable** | bit | Allows NULLs | 1 = Yes, 0 = No |
| **DefaultValue** | nvarchar | Default constraint | ((0)) |

### Special Column Types
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **IsIdentity** | bit | Auto-increment | 1 = Yes, 0 = No |
| **IdentitySeed** | bigint | Identity seed | 1 |
| **IdentityIncrement** | bigint | Identity increment | 1 |
| **IsComputed** | bit | Computed column | 1 = Yes, 0 = No |
| **ComputedDefinition** | nvarchar | Computed formula | ([Qty]*[Price]) |

### Keys and Indexes
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **IsPrimaryKey** | bit | Part of PK | 1 = Yes, 0 = No |
| **IsIndexed** | bit | Has any index | 1 = Yes, 0 = No |
| **IsForeignKey** | bit | Part of FK | 1 = Yes, 0 = No |

---

## Profile Fields (Ordered by applicability and importance)

### Common Profile Statistics (ALL columns)
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **RowsProfiled** | bigint | Total rows analyzed | 121317 |
| **NullCount** | bigint | Count of NULLs | 543 |
| **PercentNull** | decimal(9,4) | NULL percentage | 0.4477 |
| **DistinctCount** | bigint | Distinct non-NULL values | 450 |
| **DistinctPercent** | decimal(9,4) | Distinct as % of total | 37.09 |

### Frequency Analysis (Most columns)
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **MostFrequentValue** | nvarchar | Most common value | Active |
| **MostFrequentCount** | bigint | Occurrences of most frequent | 85432 |
| **MostFrequentPercent** | decimal(9,4) | Frequency % | 70.43 |

### Numeric Profile Statistics (Numeric types only)
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **MinValue** | varchar(100) | Minimum (numeric) | 0.00 |
| **MaxValue** | varchar(100) | Maximum (numeric) | 999999.99 |
| **AverageValue** | decimal(18,4) | Mean (numeric) | 1234.5678 |
| **StdDeviation** | decimal(18,4) | Standard deviation | 456.78 |

### Character Profile Statistics (String types only)
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **MinLength** | int | Min string length | 3 |
| **MaxLengthObserved** | int | Max actual string length | 45 |
| **AverageLength** | decimal(18,4) | Avg string length | 25.34 |
| **EmptyStringCount** | bigint | Count of '' | 12 |
| **WhitespaceOnlyCount** | bigint | Count of '   ' | 5 |

### Date/Time Profile Statistics (Date/time types only)
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **MinDateValue** | datetime2 | Earliest date | 2020-01-01 |
| **MaxDateValue** | datetime2 | Latest date | 2024-12-31 |
| **DateRangeDays** | int | Range in days | 1826 |

### Profile Metadata
| Field | Type | Description | Example |
|-------|------|-------------|---------|
| **ProfileNote** | varchar(200) | Warnings/notes | Sample only |

---

## Field Usage by Column Type

### Numeric Columns (int, decimal, money, float)
✅ All common fields (NULL, Distinct, Frequency)
✅ MinValue, MaxValue, AverageValue, StdDeviation
❌ Character fields
❌ Date fields

### Character Columns (varchar, nvarchar, char, nchar)
✅ All common fields (NULL, Distinct, Frequency)
✅ MinLength, MaxLengthObserved, AverageLength, EmptyStringCount, WhitespaceOnlyCount
❌ Numeric fields
❌ Date fields

### Date/Time Columns (date, datetime, datetime2)
✅ All common fields (NULL, Distinct, Frequency)
✅ MinDateValue, MaxDateValue, DateRangeDays
✅ MinValue, MaxValue (as formatted strings)
❌ Numeric statistics (Avg, StdDev)
❌ Character fields

### Binary/Complex Types (xml, geography, image)
✅ Only basic fields (Ordinal, Name, DataType)
❌ Most profiling skipped (expensive or not applicable)

---

## Display Recommendations

### Column Browser (Schema Page)
**Always Show:**
- Ordinal, Name, DataType, IsNullable, IsPrimaryKey, IsIndexed, IsForeignKey

**Show for Appropriate Types:**
- Precision/Scale (numeric)
- MaxLength (character)
- Collation (character)

**Show on Expand/Tooltip:**
- DefaultValue
- IsIdentity, IdentitySeed, IdentityIncrement
- IsComputed, ComputedDefinition

### Profiling Page (Statistics)
**Always Show:**
- Ordinal, Name, DataType
- RowsProfiled, NullCount, PercentNull
- DistinctCount, DistinctPercent

**Show for Appropriate Types:**
- Min/Max/Avg/StdDev (numeric)
- Min/Max dates (date/time)
- String length stats (character)
- MostFrequentValue/Count

**Optional (Performance):**
- EmptyStringCount, WhitespaceOnlyCount
- DateRangeDays

---

## UI Column Groups (Suggested)

### Schema Tab:
```
Group: Identity
  └─ Ordinal, Name

Group: Type
  └─ DataType, MaxLength, Precision, Scale, Collation

Group: Constraints
  └─ IsNullable, Default

Group: Keys & Indexes
  └─ PrimaryKey, Indexed, ForeignKey

Group: Special
  └─ Identity (Seed/Increment), Computed (Definition)
```

### Profile Tab:
```
Group: Overview
  └─ RowsProfiled

Group: Completeness
  └─ NullCount, NullPercent

Group: Uniqueness
  └─ DistinctCount, DistinctPercent

Group: Distribution
  └─ MostFrequentValue, MostFrequentCount, MostFrequentPercent

Group: Range (conditional)
  └─ Min, Max, Average, StdDev (numeric)
  └─ MinDate, MaxDate, DateRange (date)
  └─ MinLength, MaxLength, AvgLength (character)

Group: Quality (conditional)
  └─ EmptyStringCount, WhitespaceOnlyCount (character)
```

---

## Performance Impact

### Low Cost (<1 sec per column):
✅ OrdinalPosition, ColumnName, DataType
✅ MaxLength, Precision, Scale, Collation
✅ IsNullable, IsIdentity, IsComputed
✅ IsPrimaryKey, IsIndexed, IsForeignKey
✅ DefaultValue, Identity properties, Computed definition

### Medium Cost (1-2 sec per column):
🟡 RowsProfiled
🟡 NullCount, PercentNull
🟡 DistinctCount (can be expensive on high cardinality)
🟡 MinValue, MaxValue (numeric/date)
🟡 AverageValue, StdDeviation (numeric)

### High Cost (2-5 sec per column):
🔴 MostFrequentValue, MostFrequentCount (requires GROUP BY)
🔴 DistinctPercent (requires row count)
🔴 Character length statistics (LEN function on all rows)
🔴 EmptyStringCount, WhitespaceOnlyCount

### Optimization Tips:
- Skip frequency analysis on high-cardinality columns (>95% distinct)
- Use sampling for large tables
- Cache schema metadata (doesn't change often)
- Run profile statistics async/background for large tables

