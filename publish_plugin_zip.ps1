param(
	[string]$Source = 'C:\Program Files (x86)\AIMP\Plugins\aimp_DiscordPresence2',
	[string]$Output = 'C:\Users\ssash\Downloads\aimp_DiscordPresence2.zip',
	[string]$Folder = 'aimp_DiscordPresence2',
	[switch]$Flat
)

# .NET Framework writes "\" as the path separator inside zip entries, which is invalid per the
# zip spec and makes AIMP reject the package ("invalid file format"), so entries are written by
# hand with forward slashes here.

Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$files = @(
	'AIMP-Discord-Presence-2.dll',
	'AIMP.SDK.dll',
	'aimp_dotnet.dll',
	'DiscordRPC.dll',
	'Newtonsoft.Json.dll'
)

if (Test-Path $Output) { Remove-Item $Output -Force }

$stream = [System.IO.File]::Open($Output, [System.IO.FileMode]::CreateNew)
$archive = New-Object System.IO.Compression.ZipArchive($stream, [System.IO.Compression.ZipArchiveMode]::Create)

try {
	foreach ($file in $files) {
		$path = Join-Path $Source $file

		if (-not (Test-Path $path)) {
			Write-Host "skipped (missing): $file"
			continue
		}

		$name = if ($Flat) { $file } else { "$Folder/$file" }

		$entry = $archive.CreateEntry($name, [System.IO.Compression.CompressionLevel]::Optimal)
		$entry.LastWriteTime = (Get-Item $path).LastWriteTime

		$entryStream = $entry.Open()
		$fileStream = [System.IO.File]::OpenRead($path)

		try { $fileStream.CopyTo($entryStream) }
		finally { $fileStream.Dispose(); $entryStream.Dispose() }

		Write-Host "added $name"
	}
}
finally {
	$archive.Dispose()
	$stream.Dispose()
}

Write-Host ""
Write-Host "created $Output ($((Get-Item $Output).Length) bytes), layout: $(if ($Flat) { 'flat' } else { 'folder' })"

$check = [System.IO.Compression.ZipFile]::OpenRead($Output)
$check.Entries | ForEach-Object { "   $($_.FullName)" }
$check.Dispose()