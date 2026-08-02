# Field Ordering - Final Structure

## ✅ Reorganized Following SQL Server Conventions

The fields have been reorganized to follow standard SQL Server system catalog ordering and logical grouping patterns, matching the approach in the original stored procedure.

---

## **Schema Fields (19 fields total)**

### **Group 1: Core Column Identity** (3 fields - ALWAYS FIRST)
```sql
OrdinalPosition         -- Column position (1-based)
ColumnName              -- Column name
DataType                -- SQL Server data type
```
*Matches sys.columns.column_id, name, type order*

### **Group 2: Data Type Attributes** (4 fields)
```sql
MaxLength               -- Max length in bytes
PrecisionValue          -- Numeric precision
ScaleValue              -- Numeric scale
ColumnCollation         -- Collation
```
*Standard sys.columns metadata for type details*

### **Group 3: Common Column Properties** (2 fields)
```sql
IsNullable              -- Allows NULLs
DefaultValue            -- Default constraint
```
*Most common properties - apply to all column types*

### **Group 4: Special Column Types** (7 fields)
```sql
IsIdentity              -- Is identity
IdentitySeed            -- Identity seed
IdentityIncrement       -- Identity increment
IsComputed              -- Is computed
ComputedDefinition      -- Computed formula
```
*Identity and computed columns grouped together*

### **Group 5: Keys and Indexes** (3 fields)
```sql
IsPrimaryKey            -- Part of primary key
IsIndexed               -- Has index
IsForeignKey            -- Part of foreign key
```
*Relational integrity metadata at the end*

---

## **Profile Fields (23 fields total)**

### **Group 1: Common Profile Statistics** (5 fields - ALWAYS FIRST)
```sql
RowsProfiled            -- Total rows analyzed
NullCount               -- Count of NULLs
PercentNull             -- NULL percentage
DistinctCount           -- Distinct count
DistinctPercent         -- Distinct percentage
```
*Applies to ALL columns - calculated first in stored proc*

### **Group 2: Frequency Analysis** (3 fields)
```sql
MostFrequentValue       -- Most common value
MostFrequentCount       -- Frequency count
MostFrequentPercent     -- Frequency percentage
```
*Applies to MOST columns (not binary/geo/xml)*

### **Group 3: Numeric Profile Statistics** (4 fields)
```sql
MinValue                -- Minimum value
MaxValue                -- Maximum value
AverageValue            -- Average (mean)
StdDeviation            -- Standard deviation
```
*Numeric types only: int, decimal, money, float, etc.*

### **Group 4: Character Profile Statistics** (5 fields)
```sql
MinLength               -- Minimum string length
MaxLengthObserved       -- Maximum actual string length
AverageLength           -- Average string length
EmptyStringCount        -- Count of ''
WhitespaceOnlyCount     -- Count of whitespace-only
```
*String types only: char, varchar, nchar, nvarchar*

### **Group 5: Date/Time Profile Statistics** (3 fields)
```sql
MinDateValue            -- Earliest date
MaxDateValue            -- Latest date
DateRangeDays           -- Range in days
```
*Date/time types only: date, datetime, datetime2, etc.*

### **Group 6: Profile Metadata** (1 field)
```sql
ProfileNote             -- Notes/warnings
```
*Metadata about the profile execution*

---

## **Rationale for This Ordering**

### Schema Fields:
1. **Core identity first** - Matches SQL Server's sys.columns base columns (column_id, name, type)
2. **Type attributes next** - All metadata about the data type grouped together
3. **Common properties** - Standard flags that apply to all columns
4. **Special types** - Identity and computed columns (special behaviors)
5. **Relationships last** - Keys and indexes (relational integrity)

### Profile Fields:
1. **Common stats first** - Statistics that apply to ALL columns (NULL, Distinct)
2. **Frequency next** - Applies to MOST columns
3. **Type-specific groups** - Grouped by data type category
4. **Metadata last** - Housekeeping information

---

## **Benefits of This Organization**

✅ **Intuitive** - Follows SQL Server conventions (sys.columns order)
✅ **Grouped by applicability** - Common fields first, specialized fields later
✅ **Predictable** - Easy to remember and navigate
✅ **Maintainable** - Clear structure for adding new fields
✅ **Query-friendly** - Matches actual execution order in stored proc

---

## **Display Order in UI**

The UI should follow the same ordering for consistency:

### Column Browser (Schema page):
```
[Always visible]
Ordinal | Name | DataType | MaxLength | Precision | Scale | IsNullable | IsPrimaryKey | IsIndexed | IsForeignKey

[Show on expand/hover]
DefaultValue | Collation | Identity details | Computed details
```

### Profiling Page (Statistics):
```
[Always visible]
Ordinal | Name | DataType | RowsProfiled | NullCount | PercentNull | DistinctCount

[Conditional - based on data type]
MinValue/MaxValue/Average/StdDev (numeric)
MinLength/MaxLength/AvgLength (string)
MinDate/MaxDate/DateRange (datetime)

[Optional]
MostFrequent | EmptyStrings | Whitespace
```

---

## **Files Updated**

✅ `SCHEMA_AND_PROFILE_DESIGN.md` - Design document updated
✅ `usp_ProfileTable_v2_CREATEPROC.sql` - Stored procedure updated with new order
✅ `FIELD_REFERENCE.md` - Quick reference guide updated with logical grouping

**All documentation now reflects the standard SQL Server catalog ordering!**

