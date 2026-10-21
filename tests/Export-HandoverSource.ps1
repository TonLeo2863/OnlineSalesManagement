[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$gitCommand = Get-Command git.exe -ErrorAction SilentlyContinue
if ($gitCommand) { $gitPath=$gitCommand.Source } else {
    $gitPath = Get-ChildItem 'C:\Program Files\Microsoft Visual Studio\2022\Community\Common7\IDE\CommonExtensions\Microsoft\TeamFoundation\Team Explorer\Git' -Filter git.exe -Recurse | Select-Object -First 1 -ExpandProperty FullName
}
if (!$gitPath) { throw 'Git unavailable' }
$top = & $gitPath -C $root rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0 -or ![string]::Equals([IO.Path]::GetFullPath($top),[IO.Path]::GetFullPath($root),[StringComparison]::OrdinalIgnoreCase)) { throw 'Run exporter only from the development worktree, not an extracted source package' }
$branch = & $gitPath -C $root branch --show-current
if ($LASTEXITCODE -ne 0 -or $branch -ne 'codex/dacn05-completion') { throw 'Refusing to export a different branch' }
$head = & $gitPath -C $root rev-parse HEAD
$tracked = @(& $gitPath -C $root ls-files)
if ($LASTEXITCODE -ne 0) { throw 'Git source enumeration failed' }
$extra = @(Get-ChildItem (Join-Path $root 'docs') -File | ForEach-Object { 'docs/' + $_.Name })
$extra += @('tests/Browser-Handover.mjs','tests/Export-HandoverSource.ps1')
$files = @($tracked + $extra | Sort-Object -Unique | Where-Object {
    $_ -notmatch '(^|/)([.]git|[.]vs|bin|obj|packages|artifacts|node_modules|[.]handover-worktree)(/|$)' -and
    $_ -notmatch '(^|/)[.]env|[.](user|suo|pfx|p12|bak|mdf|ldf)$' -and
    ($_ -match '^(OnlineSalesManagement/|tests/|docs/)' -or $_ -eq 'OnlineSalesManagement.sln' -or $_ -in @('.gitignore','.gitattributes'))
})
$delivery = Join-Path $root ('artifacts\delivery\source-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss') + '-' + [Guid]::NewGuid().ToString('N').Substring(0,8))
$source = Join-Path $delivery 'source'
[void][IO.Directory]::CreateDirectory($source)
$manifest = @()
foreach ($relative in $files) {
    $inputPath = Join-Path $root $relative
    if (!(Test-Path -LiteralPath $inputPath -PathType Leaf)) { throw ('Missing selected source file: '+$relative) }
    $destination = Join-Path $source $relative
    [void][IO.Directory]::CreateDirectory((Split-Path $destination -Parent))
    Copy-Item -LiteralPath $inputPath -Destination $destination
    if ($relative -eq 'OnlineSalesManagement/Web.config') {
        [xml]$web = Get-Content -LiteralPath $destination -Encoding UTF8
        $web.configuration.connectionStrings.add.connectionString = 'Data Source=.\SQLEXPRESS;Initial Catalog=OnlineSalesDb;Integrated Security=True;MultipleActiveResultSets=True'
        $web.Save($destination)
    }
    if ([IO.Path]::GetExtension($destination) -in @('.config','.json','.xml')) {
        $content = [IO.File]::ReadAllText($destination)
        if ($content -match '(?i)(password|pwd|clientSecret|apiKey|accessToken)\s*[=:]\s*["'']?[^\s"''<>;]{4,}') { throw ('Potential secret; review before export: '+$relative) }
    }
    $manifest += [pscustomobject]@{Path=$relative; WorkspaceSHA256=(Get-FileHash $inputPath -Algorithm SHA256).Hash; PackageSHA256=(Get-FileHash $destination -Algorithm SHA256).Hash}
}
$status = @(& $gitPath -C $root status --short)
[ordered]@{Branch=$branch;BaseCommit=$head;Utc=[DateTime]::UtcNow.ToString('o');Status='SOURCE DRAFT ONLY - backup and signed PDF missing';ConfigChange='Web.config uses portable Windows-authentication SQL template';GitStatus=$status;Files=$manifest} | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $delivery 'SOURCE_MANIFEST.json') -Encoding UTF8
$zip = Join-Path $delivery 'CNTT_DACN_05_BuiCongDanh_SOURCE_DRAFT.zip'
Compress-Archive -LiteralPath $source,(Join-Path $delivery 'SOURCE_MANIFEST.json') -DestinationPath $zip
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [IO.Compression.ZipFile]::OpenRead($zip)
try {
    if ($archive.Entries.FullName -match '(^|[/\\])([.]git|[.]vs|bin|obj|packages|artifacts|node_modules)([/\\]|$)|[.](bak|mdf|ldf|user|suo|pfx)$') { throw 'Forbidden archive entry' }
    foreach ($entry in $manifest) {
        $zipPath = 'source/' + $entry.Path.Replace('\','/')
        $item = $archive.Entries | Where-Object { $_.FullName.Replace('\','/') -eq $zipPath }
        if (@($item).Count -ne 1) { throw ('Missing or duplicate archive entry: '+$entry.Path) }
        $stream = $item.Open(); $sha = [Security.Cryptography.SHA256]::Create()
        try { $hash = [BitConverter]::ToString($sha.ComputeHash($stream)).Replace('-',''); if ($hash -ne $entry.PackageSHA256) { throw ('Archive hash mismatch: '+$entry.Path) } } finally { $stream.Dispose(); $sha.Dispose() }
    }
} finally { $archive.Dispose() }
Get-FileHash -LiteralPath $zip -Algorithm SHA256 | Format-List
Write-Output ('SOURCE DRAFT: '+$zip)
Write-Output ('Verified source files: '+$manifest.Count+'; no backup or signed PDF included.')
