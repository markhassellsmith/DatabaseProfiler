# Downloads Folder Migration Guide

## Overview
This guide documents the changes needed to move report artifacts from `App_Data/TableReports/artifacts` to the user's Downloads folder at `%USERPROFILE%\Downloads\DataProfiler\Reports`.

## Changes Made

### 1. ✅ appsettings.json - COMPLETED
Added Storage configuration:
```json
"TableReports": {
  "Storage": {
	"UseDownloadsFolder": true,
	"CustomPath": null
  },
  ...
}
```

### 2. ✅ TableReportJobStoreOptions.cs - UPDATED FILE CREATED
See: `TableReportJobStoreOptions.Updated.cs`

**Key Changes:**
- Added `UseDownloadsFolder` property (default: true)
- Added `CustomPath` property for optional custom locations
- Added `GetResolvedArtifactsDirectory()` method that:
  - Returns CustomPath if specified
  - Returns `%USERPROFILE%\Downloads\DataProfiler\Reports` if UseDownloadsFolder is true
  - Falls back to App_Data location
  - Automatically creates directories
- Added `GetResolvedJobsDirectory()` method for job metadata

**Action Required:**
Replace the content of `TableReportJobStoreOptions.cs` with the content from `TableReportJobStoreOptions.Updated.cs`, then delete the .Updated.cs file.

### 3. ⚠️ Program.cs - NEEDS UPDATE
**Required Change:**
Update the TableReportJobStoreOptions configuration to bind the Storage section:

```csharp
builder.Services.Configure<TableReportJobStoreOptions>(options =>
{
	var storageSection = builder.Configuration.GetSection("TableReports:Storage");
	if (storageSection.Exists())
	{
		options.UseDownloadsFolder = storageSection.GetValue<bool>("UseDownloadsFolder", true);
		options.CustomPath = storageSection.GetValue<string?>("CustomPath");
	}

	// Keep existing directory configurations if any
	var jobsDir = builder.Configuration.GetValue<string?>("TableReports:JobsDirectory");
	if (!string.IsNullOrEmpty(jobsDir))
	{
		options.JobsDirectory = jobsDir;
	}

	var artifactsDir = builder.Configuration.GetValue<string?>("TableReports:ArtifactsDirectory");
	if (!string.IsNullOrEmpty(artifactsDir))
	{
		options.ArtifactsDirectory = artifactsDir;
	}
});
```

### 4. ⚠️ TableReportJobStore.cs - NEEDS UPDATE
**Required Changes:**
Update all references from direct property access to method calls:

**Find:**
```csharp
Path.Combine(_options.Value.ArtifactsDirectory, ...)
```

**Replace with:**
```csharp
Path.Combine(_options.Value.GetResolvedArtifactsDirectory(), ...)
```

**Find:**
```csharp
Path.Combine(_options.Value.JobsDirectory, ...)
```

**Replace with:**
```csharp
Path.Combine(_options.Value.GetResolvedJobsDirectory(), ...)
```

### 5. ⚠️ TableReportService.cs - NEEDS UPDATE
Same as TableReportJobStore.cs - update all property access to use the new methods.

### 6. ⚠️ TableReportBackgroundService.cs - NEEDS UPDATE
Same as above - update all property access to use the new methods.

## Testing Checklist
After making all changes:

1. ✅ Build the solution
2. ✅ Run the application
3. ✅ Generate a table report
4. ✅ Verify the .xlsx file appears in `%USERPROFILE%\Downloads\DataProfiler\Reports`
5. ✅ Verify the .json job file still goes to `App_Data/TableReports/jobs`

## Rollback (if needed)
Set in appsettings.json:
```json
"UseDownloadsFolder": false
```

Reports will revert to `App_Data/TableReports/artifacts`.
