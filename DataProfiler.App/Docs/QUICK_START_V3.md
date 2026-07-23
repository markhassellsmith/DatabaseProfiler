# V3 Stored Procedure - Quick Start

## 🚀 Quick Deploy (SSMS)

```sql
-- 1. Connect to your database
USE YourDatabase;
GO

-- 2. Run the script
-- Execute: DataProfiler.App/Docs/usp_ProfileTable_v3_OPTIMIZED.sql

-- 3. Test it
EXEC dbo.usp_ProfileTable @TableName = 'dbo.YourTable';
```

## ✅ Verify It's Working

In the application, profile a table and look for:
- ✨ Precision/Scale columns (e.g., "18,2" for decimal)
- 🔢 Identity badges with seed/increment
- 📐 Character length stats (min/max/avg)
- 📅 Date range analysis
- 💡 Computed column definitions

## ⚙️ Application Settings

**Enable V3 (default):**
```json
"UseStoredProcedure": true
```

**Disable V3 (fallback to legacy):**
```json
"UseStoredProcedure": false
```

Location: `appsettings.json` → `TableReports` → `Profiling`

## 📊 Expected Performance

- **30-40% faster** than legacy approach
- Best gains on medium-to-large tables (100K+ rows)
- Frequency analysis benefits most

## 🛟 Automatic Fallback

If the stored procedure isn't found, the app **automatically** uses the legacy approach.  
No errors, no downtime.

## 📖 Full Documentation

- **Technical Details:** `V3_IMPLEMENTATION_NOTES.md`
- **Deployment Options:** `DEPLOYMENT_GUIDE.md`
- **Source Code:** `usp_ProfileTable_v3_OPTIMIZED.sql`

---

**TL;DR:** Run the SQL script in your database → Application settings already configured → Enjoy faster profiling with richer metadata! ✨
