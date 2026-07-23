# Excel Report v3 - Complete Field Reference

## Report Layout

The Excel report now includes **40 columns** when profiling is enabled, organized by category:

### Core Column Identity (3 columns)
1. **Ordinal** - Column position in table
2. **Column** - Column name
3. **Data type** - SQL Server data type

### Data Type Attributes (4 columns)
4. **Length** - Display length (e.g., "50" for varchar(50))
5. **Precision** - Numeric precision (e.g., 18 for decimal(18,2))
6. **Scale** - Numeric scale (e.g., 2 for decimal(18,2))
7. **Collation** - Character collation (e.g., SQL_Latin1_General_CP1_CI_AS)

### Common Column Properties (2 columns)
8. **Nullable** - Yes/No
9. **Default** - Default constraint definition

### Special Column Types (5 columns)
10. **Identity** - Yes/No
11. **Id Seed** - Identity seed value
12. **Id Increment** - Identity increment value
13. **Computed** - Yes/No
14. **Computed Def** - Computed column formula

### Keys and Indexes (3 columns)
15. **PK** - Primary key (Yes/No)
16. **FK** - Foreign key (Yes/No)
17. **Indexed** - Has index (Yes/No)

### Common Profile Statistics (5 columns)
18. **Rows Profiled** - Actual rows analyzed
19. **Null count** - Number of NULL values
20. **Null %** - Percentage of NULL values
21. **Distinct** - Count of distinct values
22. **Distinct %** - Percentage of distinct values

### Frequency Analysis (3 columns)
23. **Most frequent** - Most common value
24. **Freq Count** - Frequency count
25. **Freq %** - Frequency percentage

### Numeric Profile Statistics (4 columns)
26. **Min** - Minimum value
27. **Max** - Maximum value
28. **Average** - Average value
29. **Std dev** - Standard deviation

### Character Profile Statistics (5 columns)
30. **Min Len** - Shortest string length
31. **Max Len** - Longest string length
32. **Avg Len** - Average string length (2 decimals)
33. **Empty** - Empty string count
34. **Whitespace** - Whitespace-only count

### Date/Time Profile Statistics (3 columns)
35. **Min Date** - Earliest date/time (yyyy-MM-dd HH:mm:ss)
36. **Max Date** - Latest date/time (yyyy-MM-dd HH:mm:ss)
37. **Date Range** - Date range in days

### Profile Metadata (1 column)
38. **Note** - Profile warnings/notes

---

## Schema-Only Mode (9 columns)

When profiling is **not** included, the report shows only schema information:

1. Ordinal
2. Column
3. Data type
4. Length
5. Nullable
6. Default
7. PK
8. FK
9. Indexed

---

## Comparison: Old vs New

### Before (V2)
**18 columns:**
Ordinal | Column | Data type | Length | Nullable | Default | PK | FK | Indexed | Null count | Null % | Distinct | Min | Max | Average | Std dev | Most frequent | Frequency

### After (V3)
**40 columns:**
All 18 above **plus**:
- Precision, Scale, Collation
- Identity, Id Seed, Id Increment, Computed, Computed Def
- Rows Profiled, Distinct %, Freq %
- Min Len, Max Len, Avg Len, Empty, Whitespace
- Min Date, Max Date, Date Range
- Note

---

## Example Use Cases

### 1. Sizing Analysis
**Old:** Only had "Length" (schema max)  
**New:** Also shows Max Len (actual observed), Avg Len, helping right-size columns

```
Column: ProductName
Length: 100
Max Len: 47
Avg Len: 18.5
→ Could reduce to varchar(50)
```

### 2. Identity Gap Detection
**Old:** No identity information  
**New:** Shows seed, increment, and actual values

```
Column: ProductID
Identity: Yes
Id Seed: 1
Id Increment: 1
Distinct: 4,891
Min: 1
Max: 5,234
→ Gap: 343 missing IDs (5,234 - 4,891)
```

### 3. Data Quality Issues
**Old:** No whitespace detection  
**New:** Shows empty and whitespace-only counts

```
Column: Description
Empty: 0
Whitespace: 127
Note: Contains whitespace-only values
→ Action needed: Clean 127 records
```

### 4. Date Range Planning
**Old:** Only Min/Max as text  
**New:** Formatted dates + calculated range

```
Column: OrderDate
Min Date: 2020-01-01 08:30:00
Max Date: 2024-12-31 17:45:00
Date Range: 1,825 (days)
→ ~5 years, consider yearly partitions
```

### 5. Precision/Scale Validation
**Old:** No precision/scale info  
**New:** Shows exact numeric attributes

```
Column: Price
Data type: decimal
Precision: 18
Scale: 2
→ Can store up to 9,999,999,999,999,999.99
```

---

## Excel Features

- **Frozen panes:** Top 5 rows frozen (headers stay visible while scrolling)
- **Banded rows:** Alternating row colors for readability
- **Auto-width:** Columns sized appropriately
- **Summary sheet:** Database overview with table list
- **One sheet per table:** Detailed column breakdown

---

## When Are Fields Populated?

| Field | Populated When |
|-------|----------------|
| Precision/Scale | Numeric types (int, decimal, float, etc.) |
| Collation | Character types (varchar, nvarchar, etc.) |
| Identity/Seed/Increment | Identity columns |
| Computed/Def | Computed columns |
| Rows Profiled | Profiling enabled |
| Null/Distinct stats | Profiling enabled |
| Frequency stats | Profiling enabled + includeFrequencyAnalysis |
| Numeric stats | Profiling enabled + numeric types |
| Character stats | Profiling enabled + character types |
| Date stats | Profiling enabled + date/time types |
| Note | Profiling enabled + issues detected |

---

## Performance Impact

**File size:**
- Schema-only: ~50 KB per 100 columns
- With profiling (40 fields): ~150 KB per 100 columns

**Generation time:**
- Schema-only: <1 second per table
- With profiling: Depends on table size (uses stored procedure)

---

## Upgrading from V2

✅ **Backward compatible:** Existing reports continue to work  
✅ **Automatic:** New reports use expanded format  
✅ **Optional:** Disable profiling to use schema-only mode  

No action required - next report generation will use the new 40-column layout automatically.
