[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'OnlineSalesManagement\bin'
$output = Join-Path $bin 'ResetAdminPasswordTests.exe'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$references = @('OnlineSalesManagement.dll','EntityFramework.dll','Microsoft.AspNet.Identity.EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll','Microsoft.AspNet.Identity.Owin.dll','Microsoft.Owin.dll','Microsoft.Owin.Security.dll') | ForEach-Object { '/reference:' + (Join-Path $bin $_) }
& $compiler /nologo /target:exe /main:ResetAdminPasswordTests "/out:$output" /reference:System.Data.dll /reference:System.Configuration.dll /reference:System.ComponentModel.DataAnnotations.dll /reference:System.Web.dll @references (Join-Path $PSScriptRoot 'Reset-AdminPassword.cs') (Join-Path $PSScriptRoot 'Reset-AdminPassword.Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed' }
[xml]$source = Get-Content (Join-Path $root 'OnlineSalesManagement\Web.config')
$connection = $source.configuration.connectionStrings.SelectSingleNode("add[@name='DefaultConnection']")
# Do not inherit production connection settings or secrets. Shared memory forces a local
# SQL connection, avoiding network Kerberos/SPN negotiation that failed with SSPI.
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
$builder['Data Source'] = 'lpc:' + $env:COMPUTERNAME + '\SQLEXPRESS'
$builder['Integrated Security'] = $true
$builder['MultipleActiveResultSets'] = $true
$builder['Connect Timeout'] = 10
$builder['Initial Catalog'] = 'AdminReset_Test_' + [Guid]::NewGuid().ToString('N')
$doc = New-Object System.Xml.XmlDocument
$config = $doc.CreateElement('configuration'); [void]$doc.AppendChild($config)
foreach ($node in $source.configuration.ChildNodes) {
    [void]$config.AppendChild($doc.ImportNode($node, $true))
}
$connections = $config.SelectSingleNode('connectionStrings')
$connections.RemoveAll()
$testConnection = $doc.CreateElement('add')
$testConnection.SetAttribute('name', 'DefaultConnection')
$testConnection.SetAttribute('providerName', 'System.Data.SqlClient')
$testConnection.SetAttribute('connectionString', $builder.ConnectionString)
[void]$connections.AppendChild($testConnection)
# Match the production tool's copied Web.config, including CLR compatibility settings.
$doc.Save($output + '.config')
& $output
if ($LASTEXITCODE -ne 0) { throw 'Reset tests failed; isolated database retained' }
