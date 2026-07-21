# PowerShell script to remove tracked runtime files from Git
# Run this from the solution root directory

Write-Host "Removing runtime-generated files from Git tracking..." -ForegroundColor Yellow
Write-Host ""

# Remove job JSON files
Write-Host "Removing job files..." -ForegroundColor Cyan
git rm --cached DataProfiler.App/App_Data/TableReports/jobs/*.json 2>$null
if ($?) {
	Write-Host "  ✓ Removed job .json files from Git" -ForegroundColor Green
} else {
	Write-Host "  ℹ No job .json files to remove (or already untracked)" -ForegroundColor Gray
}

# Remove artifact XLSX files
Write-Host "Removing artifact files..." -ForegroundColor Cyan
git rm --cached DataProfiler.App/App_Data/TableReports/artifacts/*.xlsx 2>$null
if ($?) {
	Write-Host "  ✓ Removed artifact .xlsx files from Git" -ForegroundColor Green
} else {
	Write-Host "  ℹ No artifact .xlsx files to remove (or already untracked)" -ForegroundColor Gray
}

# Remove data protection keys
Write-Host "Removing data protection keys..." -ForegroundColor Cyan
git rm --cached DataProfiler.App/App_Data/DataProtectionKeys/*.xml 2>$null
if ($?) {
	Write-Host "  ✓ Removed data protection key files from Git" -ForegroundColor Green
} else {
	Write-Host "  ℹ No data protection keys to remove (or already untracked)" -ForegroundColor Gray
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Add the contents of GITIGNORE_ADDITIONS.txt to your .gitignore file" -ForegroundColor White
Write-Host "2. Run: git add .gitignore DataProfiler.App/App_Data/**/.gitkeep" -ForegroundColor White
Write-Host "3. Commit the changes" -ForegroundColor White
Write-Host ""
Write-Host "The runtime files will no longer be tracked in Git." -ForegroundColor Green
