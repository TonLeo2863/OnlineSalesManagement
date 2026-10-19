[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string]$UserId,
    [Parameter(Mandatory=$true)][string]$Email,
    [Parameter(Mandatory=$true)][string]$Server,
    [Parameter(Mandatory=$true)][string]$Database,
    [switch]$Preview,
    [switch]$Apply
)
$ErrorActionPreference = 'Stop'
if ($Apply -and $Preview) { throw 'Choose Preview or Apply.' }
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'OnlineSalesManagement\bin'
$output = Join-Path $bin 'ResetAdminPassword.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = @('OnlineSalesManagement.dll','EntityFramework.dll','Microsoft.AspNet.Identity.EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll','Microsoft.AspNet.Identity.Owin.dll','Microsoft.Owin.dll','Microsoft.Owin.Security.dll') | ForEach-Object { '/reference:' + (Join-Path $bin $_) }
& $compiler /nologo /target:exe "/out:$output" /reference:System.Data.dll /reference:System.Configuration.dll /reference:System.Web.dll @references (Join-Path $PSScriptRoot 'Reset-AdminPassword.cs')
if ($LASTEXITCODE -ne 0) { throw 'Reset tool compilation failed. Build the solution first.' }
# Keep the tool local. No endpoint, initializer, migration, or password argument.
Copy-Item -LiteralPath (Join-Path $root 'OnlineSalesManagement\Web.config') -Destination ($output + '.config')
$mode = 'preview'
if ($Apply) { $mode = 'apply' }
& $output $UserId $Email $Server $Database $mode
if ($LASTEXITCODE -ne 0) { throw 'Admin password reset failed. See the safe error code and stage printed above; do not share credentials.' }
