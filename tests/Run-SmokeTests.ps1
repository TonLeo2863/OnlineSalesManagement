$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'OnlineSalesManagement\bin'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $bin 'Dacn05SmokeTests.exe'
$references = @('OnlineSalesManagement.dll','System.Web.Mvc.dll','EntityFramework.dll','Microsoft.AspNet.Identity.EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll') | ForEach-Object { '/reference:' + (Join-Path $bin $_) }
& $compiler /nologo /target:exe "/out:$output" /reference:System.Web.dll /reference:System.ComponentModel.DataAnnotations.dll @references (Join-Path $PSScriptRoot 'SmokeTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Smoke test compilation failed' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Smoke tests failed' }
