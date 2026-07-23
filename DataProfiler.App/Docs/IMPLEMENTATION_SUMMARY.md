# Schema and Profile Information - Implementation Summary

## What I've Created for You

### 1. **Design Document** 
📄 `DataProfiler.App/Docs/SCHEMA_AND_PROFILE_DESIGN.md`

**Contains:**
- Complete analysis of current state (stored proc vs. application)
- Gap analysis showing what's missing where
- Recommended unified design for both schema and profile info
- Implementation priority (Phase 1, 2, 3)
- Questions for your consideration

### 2. **Improved Stored Procedure**
📄 `DataProfiler.App/Docs/usp_ProfileTable_v2_CREATEPROC.sql`

**Key Improvements:**

#### Schema Enhancements (NEW):
✅ **IsPrimaryKey** - Detects columns in primary key
✅ **IsIndexed** - Detects any indexed columns
✅ **IsForeignKey** - Detects foreign key columns
✅ **DefaultValue** - Captures default constraint definition
✅ **IdentitySeed** - Identity seed value
✅ **IdentityIncrement** - Identity increment value
✅ **ComputedDefinition** - Computed column formula

#### Profile Enhancements (NEW):
✅ **MostFrequentValue** - Most common non-NULL value
✅ **MostFrequentCount** - Occurrences of most frequent
✅ **MostFrequentPercent** - Frequency as % of total
✅ **WhitespaceOnlyCount** - Whitespace-only strings
✅ **DateRangeDays** - Date range in days

#### Other Improvements:
✅ **@IncludeFrequencyAnalysis** parameter - Can skip expensive frequency analysis
✅ **@MaxFrequencyValues** parameter - Future: support top N values
✅ **Better error handling** - Returns error details
✅ **Skips high-cardinality columns** - Avoids frequency analysis on columns with >95% distinct values
✅ **Two result sets** - Table metadata + Column profiles

---

## Complete Field List

### **Schema Metadata (22 fields)**

```
Core Identity:
- OrdinalPosition
- ColumnName

Data Type:
- DataType
- MaxLength
- PrecisionValue
- ScaleValue
- ColumnCollation

Properties:
- IsNullable
- IsIdentity
- IsComputed
- IsPrimaryKey       ← NEW
- IsIndexed          ← NEW
- IsForeignKey       ← NEW
- DefaultValue       ← NEW

Additional:
- IdentitySeed       ← NEW
- IdentityIncrement  ← NEW
- ComputedDefinition ← NEW
```

### **Profile Statistics (27 fields)**

```
Row Context:
- RowsProfiled

NULL Analysis:
- NullCount
- PercentNull

Uniqueness:
- DistinctCount
- DistinctPercent

Frequency:
- MostFrequentValue      ← NEW
- MostFrequentCount      ← NEW
- MostFrequentPercent    ← NEW

Numeric:
- MinValue
- MaxValue
- AverageValue
- StdDeviation

Character:
- MinLength
- MaxLengthObserved
- AverageLength
- EmptyStringCount
- WhitespaceOnlyCount    ← NEW

Date/Time:
- MinDateValue
- MaxDateValue
- DateRangeDays          ← NEW

Notes:
- ProfileNote
```

**Total: 49 fields** (22 schema + 27 profile)

---

## What's Next

### **Your Tasks:**

1. **Review the Design Document**
   - Read `SCHEMA_AND_PROFILE_DESIGN.md`
   - Decide on any additions/changes
   - Answer the questions at the end

2. **Test the Stored Procedure**
   - CREATE the proc: `usp_ProfileTable_v2_CREATEPROC.sql`
   - Test on small table: 
	 ```sql
	 EXEC dbo.usp_ProfileTable @TableName = 'YourSchema.SmallTable';
	 ```
   - Test on medium table (verify performance)
   - Test on large table (verify timeout fix works)
   - Verify all fields populate correctly

3. **Provide Feedback**
   - Are all fields useful?
   - Any missing fields?
   - Performance acceptable?
   - Should we implement sampling differently?

### **My Tasks (After Your Approval):**

4. **Update Application Models**
   - Add new fields to `SchemaColumnModel`
   - Add new fields to `ColumnProfileModel`
   - Update `TableReportModels` for Excel export

5. **Update Services**
   - Modify `SchemaDiscoveryService` to query new schema fields
   - Modify `TableProfilingService` to use stored proc or mirror logic
   - Update SQL queries to pull all new fields

6. **Update UI**
   - Add columns to ColumnBrowser table (schema)
   - Add columns to Profiling page table (statistics)
   - Format new fields appropriately
   - Add tooltips for new metrics
   - Update sort functionality

7. **Update Reporting**
   - Add new fields to Excel export
   - Update worksheet layout
   - Add explanations for new metrics

---

## Example Output

When you run:
```sql
EXEC dbo.usp_ProfileTable @TableName = 'Sales.SalesOrderDetail';
```

You'll get:

**Result Set 1: Table Metadata**
```
SchemaName  TableName           TotalRows  SamplePercent
Sales       SalesOrderDetail    121317     100.00
```

**Result Set 2: Column Profiles** (121,317 rows × 49 columns)
```
OrdinalPosition  ColumnName        DataType    MaxLength  PrecisionValue  ScaleValue  IsNullable  IsIdentity  IsPrimaryKey  IsIndexed  IsForeignKey  ...
1                SalesOrderID      int         4          10              0           0           0           1             1          1             ...
2                SalesOrderDetailID int        4          10              0           0           1           1             1          0             ...
3                CarrierTrackingNumber nvarchar 50         0               0           1           0           0             0          0             ...
...
```

---

## Performance Considerations

**Estimated Execution Time per Column (100K rows):**
- Basic stats (NULL, Distinct): ~0.5 - 1 second
- Numeric stats: ~0.5 second
- Character stats: ~1 - 2 seconds
- Date stats: ~0.5 second
- Frequency analysis: ~2 - 5 seconds (most expensive)

**For 11 columns on 121K row table:**
- Total estimated: 30 - 60 seconds
- With 300 second timeout: ✅ Safe

**Optimization Options:**
1. Set `@IncludeFrequencyAnalysis = 0` (saves 20-50% time)
2. Use `@SamplePercent = 10.0` for large tables (90% faster)
3. Skip high-cardinality columns (already implemented)

---

## Decision Points

Please decide on these before I implement:

### 1. **Frequency Analysis**
- ❓ Always include most frequent value?
- ❓ Make it optional in UI?
- ❓ Show top 3-5 values instead of just 1?

### 2. **Sampling**
- ❓ Allow users to choose sample percentage in UI?
- ❓ Auto-sample for tables over X rows?
- ❓ Show warning when sampled?

### 3. **Display Priorities**
- ❓ Which fields are "always show"?
- ❓ Which fields are "show on hover/expand"?
- ❓ Separate tabs for schema vs. profile?

### 4. **Performance vs. Completeness**
- ❓ Skip expensive stats for very large tables?
- ❓ Add progress indicator for long-running profiles?
- ❓ Allow background/async profiling?

---

## Files Ready for You

✅ `SCHEMA_AND_PROFILE_DESIGN.md` - Read this first
✅ `usp_ProfileTable_v2_CREATEPROC.sql` - Test this next

**After you approve, I'll implement in the application!**

