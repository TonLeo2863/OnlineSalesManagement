[CmdletBinding()]
param([ValidateSet('Preview','Apply')][string]$Mode='Preview', [string]$Database, [switch]$ConfirmDemoApply)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bin=Join-Path $root 'OnlineSalesManagement\bin'
[xml]$source=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($source.configuration.connectionStrings.add.connectionString)
if (!$builder.IntegratedSecurity) { throw 'Only Windows authentication supported; credentials are never copied.' }
if ($Database) { $builder['Initial Catalog']=$Database }
if ($Mode -eq 'Apply' -and (!$ConfirmDemoApply -or !($builder.InitialCatalog.StartsWith('OnlineSalesDemo_') -or $builder.InitialCatalog.StartsWith('Dacn05_Test_')))) { throw 'Apply requires -ConfirmDemoApply and an existing isolated OnlineSalesDemo_* or Dacn05_Test_* DB. Real DB writes are blocked.' }
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output=Join-Path $bin 'DemoCatalogCommand.exe'
$references=@('OnlineSalesManagement.dll','EntityFramework.dll','Microsoft.AspNet.Identity.EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll') | ForEach-Object { '/reference:'+(Join-Path $bin $_) }
& $compiler /nologo /target:exe "/out:$output" /reference:System.ComponentModel.DataAnnotations.dll @references (Join-Path $PSScriptRoot 'DemoCatalogCommand.cs')
if($LASTEXITCODE -ne 0){throw 'Command compilation failed'}
$doc=New-Object Xml.XmlDocument
$config=$doc.CreateElement('configuration');[void]$doc.AppendChild($config)
foreach($name in @('configSections','connectionStrings','entityFramework','runtime')) { $node=$source.configuration.SelectSingleNode($name);if($node){[void]$config.AppendChild($doc.ImportNode($node,$true))} }
$config.connectionStrings.add.connectionString=$builder.ConnectionString
$doc.Save($output+'.config')
& $output $Mode
if($LASTEXITCODE -ne 0){throw 'Demo catalog command failed'}
