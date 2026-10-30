$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bin=Join-Path $root 'OnlineSalesManagement\bin'
$output=Join-Path $bin 'ProductionAccountReadOnlyTests.exe'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$refs=@('OnlineSalesManagement.dll','EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll','Microsoft.AspNet.Identity.EntityFramework.dll') | ForEach-Object {'/reference:'+(Join-Path $bin $_)}
& $compiler /nologo /target:exe "/out:$output" /reference:System.Configuration.dll /reference:System.Data.dll /reference:System.ComponentModel.DataAnnotations.dll @refs (Join-Path $PSScriptRoot 'ProductionAccountReadOnlyTests.cs')
if($LASTEXITCODE -ne 0){throw 'Read-only test compilation failed'}
[xml]$source=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($source.configuration.connectionStrings.add.connectionString)
if($builder.DataSource -ne 'DESKTOP-AUTD3A3\SQLEXPRESS' -or $builder.InitialCatalog -ne 'OnlineSalesDb' -or !$builder.IntegratedSecurity){throw 'Wrong read-only test target'}
$config=New-Object Xml.XmlDocument;$configuration=$config.CreateElement('configuration');[void]$config.AppendChild($configuration)
foreach($name in @('configSections','connectionStrings','entityFramework','runtime')){$node=$source.configuration.SelectSingleNode($name);if($node){[void]$configuration.AppendChild($config.ImportNode($node,$true))}}
$config.Save($output+'.config')
& $output
if($LASTEXITCODE -ne 0){throw 'Read-only EF tests failed; do not run migration again'}
