# Renaming Guide: DataProfiler → Database Profiler

**Created:** 2026  
**Purpose:** Guide for renaming solution from "DataProfiler" to "Database Profiler"  
**Status:** 🚧 Work in Progress - DELETE THIS FILE AFTER COMPLETION

---

## 📋 Naming Conventions

- **Technical/Code Name:** `DatabaseProfiler` (no space, PascalCase for namespaces, folders, files)
- **Display Name:** `Database Profiler` (with space, for UI and documentation)

---

## 🔧 PHASE 1: File System & Solution Structure (Manual in VS/File Explorer)

### ⚠️ **DO THESE STEPS FIRST - BEFORE ANY CODE CHANGES**

1. **Close Visual Studio completely**

2. **Rename root folder:**
   - From: `C:\Users\Mark\source\repos\DataProfiler-VS\`
   - To: `C:\Users\Mark\source\repos\DatabaseProfiler-VS\`

3. **Rename project folder:**
   - From: `DatabaseProfiler-VS\DataProfiler.App\`
   - To: `DatabaseProfiler-VS\DatabaseProfiler.App\`

4. **Rename solution file:**
   - From: `DataProfiler.slnx`
   - To: `DatabaseProfiler.slnx`

5. **Rename project file:**
   - From: `DatabaseProfiler.App\DataProfiler.App.csproj`
   - To: `DatabaseProfiler.App\DatabaseProfiler.App.csproj`

6. **Rename CSS file:**
   - From: `DatabaseProfiler.App\DataProfiler.App.styles.css`
   - To: `DatabaseProfiler.App\DatabaseProfiler.App.styles.css`

7. **Update solution file content:**
   - Open `DatabaseProfiler.slnx` in text editor (Notepad++)
   - Find: `DataProfiler.App\DataProfiler.App.csproj`
   - Replace with: `DatabaseProfiler.App\DatabaseProfiler.App.csproj`
   - Save and close

8. **Reopen solution in Visual Studio**
   - Open: `C:\Users\Mark\source\repos\DatabaseProfiler-VS\DatabaseProfiler.slnx`
   - Verify project loads correctly

---

## 🔧 PHASE 2: Code Changes (Can be assisted)

### A. Namespaces in C# Files (~50 files)

**Find and Replace in All Files:**

| Find | Replace |
|------|---------|
| `namespace DataProfiler.App` | `namespace DatabaseProfiler.App` |

**Files affected (all .cs files):**
- `Models/*.cs` (~12 files)
- `Pages/**/*.cs` (~15 files)
- `Services/**/*.cs` (~10 files)
- `ViewComponents/*.cs` (1 file)
- `Program.cs` (1 file)

### B. Using Statements

**Find and Replace in All Files:**

| Find | Replace |
|------|---------|
| `using DataProfiler.App` | `using DatabaseProfiler.App` |

**Additional patterns to find:**
- `@using DataProfiler.App` (in .cshtml files)
- `@namespace DataProfiler.App` (in .cshtml files)
- `@model DataProfiler.App` (in .cshtml files)

### C. Layout & UI Display Names

**File:** `Pages\Shared\_Layout.cshtml`

**Changes needed (3 locations):**

1. **Page Title (line ~17):**
   ```html
   <!-- FROM: -->
   <title>@ViewData["Title"] - DataProfiler.App</title>
   <!-- TO: -->
   <title>@ViewData["Title"] - Database Profiler</title>
   ```

2. **Navbar Brand (line ~58):**
   ```html
   <!-- FROM: -->
   <a class="navbar-brand" asp-area="" asp-page="/Index">DataProfiler.App</a>
   <!-- TO: -->
   <a class="navbar-brand" asp-area="" asp-page="/Index">Database Profiler</a>
   ```

3. **Footer (line ~156):**
   ```html
   <!-- FROM: -->
   &copy; 2026 - DataProfiler.App - <a asp-area="" asp-page="/Privacy">Privacy</a>
   <!-- TO: -->
   &copy; 2026 - Database Profiler - <a asp-area="" asp-page="/Privacy">Privacy</a>
   ```

4. **CSS Reference (line ~21):**
   ```html
   <!-- FROM: -->
   <link rel="stylesheet" href="~/DataProfiler.App.styles.css" asp-append-version="true" />
   <!-- TO: -->
   <link rel="stylesheet" href="~/DatabaseProfiler.App.styles.css" asp-append-version="true" />
   ```

### D. Session Keys & Constants (String Literals)

**File:** `Services\Connections\ConnectionSessionState.cs`

```csharp
// Line ~44 - ApplicationName
FROM: ApplicationName = "DataProfiler.App",
TO:   ApplicationName = "Database Profiler",

// Line ~68 - Session Key
FROM: private const string SessionKey = "DataProfiler.ConnectionSession";
TO:   private const string SessionKey = "DatabaseProfiler.ConnectionSession";

// Line ~69 - Report Job Session Key
FROM: private const string ActiveReportJobSessionKey = "DataProfiler.ActiveReportJobId";
TO:   private const string ActiveReportJobSessionKey = "DatabaseProfiler.ActiveReportJobId";
```

**File:** `Services\Theme\ThemeSessionState.cs`

```csharp
// Line ~14 - Session Key
FROM: private const string SessionKey = "DataProfiler.AppTheme";
TO:   private const string SessionKey = "DatabaseProfiler.AppTheme";

// Line ~15 - Cookie Key
FROM: private const string CookieKey = "DataProfiler.AppTheme";
TO:   private const string CookieKey = "DatabaseProfiler.AppTheme";
```

**File:** `Services\Reporting\TableReportJobStore.cs`

```csharp
// Line ~14 - Protection Purpose
FROM: private const string ProtectionsPurpose = "DataProfiler.App.Services.Reporting.TableReportJobStore/v1";
TO:   private const string ProtectionsPurpose = "DatabaseProfiler.App.Services.Reporting.TableReportJobStore/v1";
```

**File:** `Program.cs`

```csharp
// Line ~11 - Application Name
FROM: .SetApplicationName("DataProfiler.App");
TO:   .SetApplicationName("Database Profiler");
```

### E. View Components

**File:** `Pages\Shared\Components\ContextBreadcrumb\Default.cshtml`

```razor
<!-- Line ~1 - Model Reference -->
FROM: @model DataProfiler.App.ViewComponents.ContextBreadcrumbViewModel
TO:   @model DatabaseProfiler.App.ViewComponents.ContextBreadcrumbViewModel
```

---

## 📚 PHASE 3: Documentation Updates

### Primary Documentation Files

**File:** `README.md`

**Find and Replace:**

| Find | Replace |
|------|---------|
| `# DataProfiler` | `# Database Profiler` |
| `DataProfiler provides` | `Database Profiler provides` |
| `DataProfiler follows` | `Database Profiler follows` |
| `DataProfiler-VS` | `DatabaseProfiler-VS` |
| `DataProfiler.App` | `DatabaseProfiler.App` |
| `DataProfiler application` | `Database Profiler application` |

**Specific sections to update:**

1. **Title (line 1):**
   ```markdown
   # Database Profiler
   ```

2. **Clone command (~line 113):**
   ```bash
   git clone https://github.com/markhassellsmith/DatabaseProfiler.git
   cd DatabaseProfiler
   ```

3. **File paths (multiple locations):**
   - Update all references from `DataProfiler.App/Docs/` to `DatabaseProfiler.App/Docs/`

### Documentation in Docs Folder

**Files to update:**
- `Docs\ApplicationInterfaceModel.md`
- `Docs\DEPLOYMENT_GUIDE.md`
- `Docs\IMPLEMENTATION_SUMMARY.md`
- `Docs\V3_IMPLEMENTATION_COMPLETE.md`
- `Docs\QUICK_START_V3.md`
- `Docs\SCHEMA_AND_PROFILE_DESIGN.md`
- `Docs\FIELD_REFERENCE.md`
- All other `.md` files in Docs folder

**Find and Replace in all .md files:**

| Find | Replace |
|------|---------|
| `DataProfiler.App` | `DatabaseProfiler.App` |
| `DataProfiler application` | `Database Profiler application` |
| `DataProfiler-VS` | `DatabaseProfiler-VS` |

---

## 🗂️ PHASE 4: Configuration Files

### appsettings.json

**File:** `appsettings.json`

Review and update any application name references or connection string application names.

### launchSettings.json

**File:** `Properties\launchSettings.json`

```json
// Update applicationUrl or any profile names that reference DataProfiler
FROM: "DataProfiler.App"
TO:   "Database Profiler"
```

---

## 🌐 PHASE 5: Git Repository

### Update GitHub Repository

**Option A: Rename on GitHub**
1. Go to: https://github.com/markhassellsmith/DataProfiler
2. Settings → General → Repository name
3. Rename to: `DatabaseProfiler`
4. GitHub will automatically redirect old URLs

**Option B: Update Remote URL (if renamed manually)**
```powershell
cd C:\Users\Mark\source\repos\DatabaseProfiler-VS
git remote set-url origin https://github.com/markhassellsmith/DatabaseProfiler.git
git remote -v  # Verify
```

### Commit the Changes

```powershell
git add -A
git commit -m "Rename application from DataProfiler to Database Profiler"
git push origin master
```

---

## ✅ Verification Checklist

After all changes, verify:

- [ ] Solution opens without errors in Visual Studio
- [ ] All projects build successfully (`Build → Rebuild Solution`)
- [ ] Application runs (`F5` or `dotnet run`)
- [ ] Home page displays "Database Profiler" in title and navbar
- [ ] No console errors or warnings
- [ ] Session management works (connect to server)
- [ ] Navigate through all pages - no broken links
- [ ] Footer shows "Database Profiler"
- [ ] Page titles show "Database Profiler"
- [ ] No references to "DataProfiler" visible in UI

### Search for Remaining References

**In Visual Studio:**
1. Edit → Find and Replace → Find in Files (Ctrl+Shift+F)
2. Search for: `DataProfiler`
3. Review all matches
4. Update any remaining references

**Exclude these matches (should remain):**
- Git history/logs
- This file (`RENAMING_GUIDE.md`)

---

## 📊 Summary Statistics

| Category | Approximate Count |
|----------|------------------|
| C# namespace changes | ~50 files |
| Razor view model references | ~30 files |
| String literal updates | ~10 locations |
| Documentation files | ~15 files |
| Configuration files | ~2 files |
| Layout/UI display names | ~4 locations |
| **Total estimated changes** | **~110+ files** |

---

## ⚠️ Common Issues & Solutions

### Issue 1: Solution won't open
- **Cause:** Solution file still references old project path
- **Fix:** Edit `.slnx` file manually, update project path

### Issue 2: CSS not loading
- **Cause:** Old CSS filename reference in `_Layout.cshtml`
- **Fix:** Update `<link>` tag to reference `DatabaseProfiler.App.styles.css`

### Issue 3: Namespace errors after rename
- **Cause:** Cached build artifacts
- **Fix:** 
  ```powershell
  # Clean solution
  dotnet clean
  # Delete bin/obj folders
  Remove-Item -Recurse -Force .\bin, .\obj
  # Rebuild
  dotnet build
  ```

### Issue 4: Git still shows old remote
- **Cause:** Remote URL not updated
- **Fix:** See "Update Remote URL" section above

---

## 🎯 Recommended Approach

**Best Practice Order:**

1. ✅ Complete Phase 1 (file/folder renames) - **MANUAL**
2. ✅ Reopen solution and verify it loads
3. ✅ Use Find & Replace for namespaces - **ASSISTED/AUTOMATED**
4. ✅ Manually update string literals - **MANUAL/ASSISTED**
5. ✅ Update documentation - **ASSISTED/AUTOMATED**
6. ✅ Build and test
7. ✅ Update Git repository
8. ✅ Final verification
9. ✅ **DELETE THIS FILE** (`RENAMING_GUIDE.md`)

---

## 📝 Notes

- Session keys being changed will invalidate existing sessions (users will need to reconnect)
- No database schema changes needed
- No breaking changes to stored procedures
- Consider updating README screenshots if they show old name
- Update any external documentation or wikis

---

**🚀 Ready to begin? Start with Phase 1!**

**🗑️ Remember to delete this file when done!**
