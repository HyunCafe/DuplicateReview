param([string]$OutputName = 'DuplicateReview-0.1.2-dev.pext')
$ErrorActionPreference = 'Stop'
$project = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$parent = Split-Path -Parent $project
$outputDirectory = Join-Path $parent 'Releases/DuplicateReview'
$dll = Join-Path $project 'bin/Release/net462/DuplicateReview.dll'
$manifest = Join-Path $project 'extension.yaml'

if ($OutputName -notmatch '^DuplicateReview-[a-zA-Z0-9.\-_]+\.pext$' -or $OutputName.Contains('..')) {
    throw 'Invalid output name.'
}
if (-not (Test-Path -LiteralPath $dll -PathType Leaf)) { throw 'Build the project in Release mode first.' }
if (-not (Test-Path -LiteralPath $manifest -PathType Leaf)) { throw 'Missing extension.yaml' }
if (([Reflection.AssemblyName]::GetAssemblyName($dll).Version.ToString()) -ne '0.1.2.0') {
    throw 'Compiled DLL version is not 0.1.2.0.'
}
$manifestText = Get-Content -LiteralPath $manifest -Raw
if ($manifestText -notmatch '(?m)^Version:\s*0\.1\.2\s*$' -or
    $manifestText -notmatch '(?m)^Module:\s*DuplicateReview\.dll\s*$') {
    throw 'Manifest version or DLL does not match expected release.'
}
New-Item -ItemType Directory -Force -Path $outputDirectory | Out-Null
$dest = Join-Path $outputDirectory $OutputName
if (Test-Path -LiteralPath $dest) { throw "BLOCKED: Release already exists: $dest" }
$staging = Join-Path ([IO.Path]::GetTempPath()) ('DuplicateReview-pack-'+[Guid]::NewGuid().ToString('N'))
$temporaryZip = Join-Path $staging 'package.zip'
try {
    New-Item -ItemType Directory -Path $staging | Out-Null
    Copy-Item -LiteralPath $dll -Destination (Join-Path $staging 'DuplicateReview.dll')
    Copy-Item -LiteralPath $manifest -Destination (Join-Path $staging 'extension.yaml')
    Compress-Archive -LiteralPath (Join-Path $staging 'DuplicateReview.dll'),(Join-Path $staging 'extension.yaml') -DestinationPath $temporaryZip -CompressionLevel Optimal
    Add-Type -AssemblyName 'System.IO.Compression' -ErrorAction SilentlyContinue
    $archive = [IO.Compression.ZipFile]::OpenRead($temporaryZip)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName } | Sort-Object)
        if (($names -join ',') -ne 'DuplicateReview.dll,extension.yaml') {
            throw ('Unexpected packaged files: ' + ($names -join ', '))
        }
    } finally { $archive.Dispose() }
    Copy-Item -LiteralPath $temporaryZip -Destination $dest
} finally {
    if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
}
Write-Host "CREATED: $dest"
Get-FileHash -LiteralPath $dest -Algorithm SHA256 | Format-List
Write-Host 'Local development package ONLY. No Playnite installation or GitHub publishing performed.'
