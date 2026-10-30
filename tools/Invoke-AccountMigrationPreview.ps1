param([ValidateSet('Scaffold','Preview')][string]$Mode='Preview')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$bin=Join-Path $root 'OnlineSalesManagement\bin'
$compiler=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output=Join-Path $bin 'AccountMigrationPreview.exe'
$references=@('OnlineSalesManagement.dll','EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll','Microsoft.AspNet.Identity.EntityFramework.dll') | ForEach-Object {'/reference:'+(Join-Path $bin $_)}
& $compiler /nologo /target:exe "/out:$output" /reference:System.Configuration.dll /reference:System.Windows.Forms.dll /reference:System.ComponentModel.DataAnnotations.dll @references (Join-Path $PSScriptRoot 'Scaffold-AccountGovernance.cs')
if($LASTEXITCODE -ne 0){throw 'Preview tooling compilation failed'}
[xml]$web=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($web.configuration.connectionStrings.add.connectionString)
if(!$builder.IntegratedSecurity){throw 'Only Windows authentication supported'}
$builder['Initial Catalog']='Dacn05_Test_MigrationPreview'
$doc=New-Object Xml.XmlDocument;$configuration=$doc.CreateElement('configuration');[void]$doc.AppendChild($configuration)
foreach($name in @('configSections','connectionStrings','entityFramework','runtime')){$node=$web.configuration.SelectSingleNode($name);if($node){[void]$configuration.AppendChild($doc.ImportNode($node,$true))}}
$configuration.connectionStrings.add.connectionString=$builder.ConnectionString;$doc.Save($output+'.config')
$destination=if($Mode -eq 'Scaffold'){Join-Path $root 'OnlineSalesManagement\Migrations'}else{Join-Path $root 'docs\migrations\AccountGovernance.sql'}
[void][IO.Directory]::CreateDirectory((Split-Path $destination -Parent))
& $output $Mode $destination
if($LASTEXITCODE -ne 0){throw 'Migration preview/scaffolding failed'}
