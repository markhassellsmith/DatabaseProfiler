# PowerShell Script to Update DataProfiler for Downloads Folder Support
# Run this from the DataProfiler.App directory

Write-Host "DataProfiler Downloads Folder Migration Script" -ForegroundColor Green
Write-Host "================================================" -ForegroundColor Green
Write-Host ""

# Step 1: Backup current files
Write-Host "Step 1: Creating backups..." -ForegroundColor Yellow
$filesToBackup = @(
	"Services/Reporting/TableReportJobStoreOptions.cs",
	"Services/Reporting/TableReportJobStore.cs",
	"Services/Reporting/TableReportService.cs",
	"Services/Reporting/TableReportBackgroundService.cs",
	"Program.cs"
)

foreach ($file in $filesToBackup) {
	if (Test-Path $file) {
		Copy-Item $file "$file.backup" -Force
		Write-Host "  ✓ Backed up $file" -ForegroundColor Gray
	}
}

Write-Host ""
Write-Host "Step 2: Files that need manual updates:" -ForegroundColor Yellow
Write-Host ""

Write-Host "FILE: TableReportJobStoreOptions.cs" -ForegroundColor Cyan
Write-Host "  Add these methods to the class:" -ForegroundColor White
Write-Host @"
	public bool UseDownloadsFolder { get; set; } = true;
	public string? CustomPath { get; set; }

	public string GetResolvedArtifactsDirectory()
	{
		if (!string.IsNullOrEmpty(CustomPath))
		{
			Directory.CreateDirectory(CustomPath);
			return Path.GetFullPath(CustomPath);
		}

		if (UseDownloadsFolder)
		{
			var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			var downloadsPath = Path.Combine(userProfile, "Downloads", "DataProfiler", "Reports");
			Directory.CreateDirectory(downloadsPath);
			return downloadsPath;
		}

		var defaultPath = Path.GetFullPath(ArtifactsDirectory);
		Directory.CreateDirectory(defaultPath);
		return defaultPath;
	}

	public string GetResolvedJobsDirectory()
	{
		var jobsPath = Path.GetFullPath(JobsDirectory);
		Directory.CreateDirectory(jobsPath);
		return jobsPath;
	}
"@ -ForegroundColor Green

Write-Host ""
Write-Host "FILE: Program.cs" -ForegroundColor Cyan
Write-Host "  Find the TableReportJobStoreOptions configuration and add:" -ForegroundColor White
Write-Host @"
builder.Services.Configure<TableReportJobStoreOptions>(options =>
{
	var storageSection = builder.Configuration.GetSection("TableReports:Storage");
	if (storageSection.Exists())
	{
		options.UseDownloadsFolder = storageSection.GetValue<bool>("UseDownloadsFolder", true);
		options.CustomPath = storageSection.GetValue<string?>("CustomPath");
	}
});
"@ -ForegroundColor Green

Write-Host ""
Write-Host "FILES: TableReportJobStore.cs, TableReportService.cs, TableReportBackgroundService.cs" -ForegroundColor Cyan
Write-Host "  Replace all occurrences:" -ForegroundColor White
Write-Host "    _options.Value.ArtifactsDirectory → _options.Value.GetResolvedArtifactsDirectory()" -ForegroundColor Green
Write-Host "    _options.Value.JobsDirectory → _options.Value.GetResolvedJobsDirectory()" -ForegroundColor Green

Write-Host ""
Write-Host "Step 3: To test after changes:" -ForegroundColor Yellow
Write-Host "  1. Build the solution" -ForegroundColor White
Write-Host "  2. Run the application" -ForegroundColor White
Write-Host "  3. Generate a report" -ForegroundColor White
Write-Host "  4. Check: $env:USERPROFILE\Downloads\DataProfiler\Reports" -ForegroundColor White

Write-Host ""
Write-Host "To restore backups if needed:" -ForegroundColor Yellow
Write-Host "  foreach (`$f in Get-ChildItem *.backup -Recurse) { Copy-Item `$f.FullName (`$f.FullName -replace '.backup','') -Force }" -ForegroundColor Gray

Write-Host ""
Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown")
