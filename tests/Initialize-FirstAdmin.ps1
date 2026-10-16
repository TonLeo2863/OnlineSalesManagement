[CmdletBinding()]
param([Parameter(Mandatory=$true)][string]$UserId, [Parameter(Mandatory=$true)][string]$Email, [switch]$Apply)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'OnlineSalesManagement\bin'
$output = Join-Path $bin 'InitializeFirstAdmin.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = @('OnlineSalesManagement.dll','EntityFramework.dll','Microsoft.AspNet.Identity.EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll') | ForEach-Object { '/reference:' + (Join-Path $bin $_) }
& $compiler /nologo /target:exe "/out:$output" @references (Join-Path $PSScriptRoot 'Initialize-FirstAdmin.cs')
if ($LASTEXITCODE -ne 0) { throw 'Admin tool compilation failed' }
# Local operator tool: no web route, no new user, no password, no migration.
Copy-Item -LiteralPath (Join-Path $root 'OnlineSalesManagement\Web.config') -Destination ($output + '.config')
if ($Apply) { & $output $UserId $Email '--apply' } else { & $output $UserId $Email }
if ($LASTEXITCODE -ne 0) { throw 'First Admin initialization failed' }
