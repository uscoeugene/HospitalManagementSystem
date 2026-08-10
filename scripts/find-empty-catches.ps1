# Find empty catch blocks across the repository
# Usage: .\scripts\find-empty-catches.ps1
Get-ChildItem -Path . -Recurse -Include *.cs | ForEach-Object {
	$path = $_.FullName
	$content = Get-Content $path -Raw
	$regex = 'catch\s*\{\s*\}'
	if ($content -match $regex) {
		Write-Output "$path"
		Select-String -Path $path -Pattern 'catch\s*\{\s*\}' -SimpleMatch | ForEach-Object { Write-Output "  Line $($_.LineNumber): $($_.Line.Trim())" }
	}
}
