$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$bin = Join-Path $root 'OnlineSalesManagement\bin'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $bin 'Dacn05IntegrationTests.exe'
$references = @('OnlineSalesManagement.dll','System.Web.Mvc.dll','EntityFramework.dll','Microsoft.AspNet.Identity.EntityFramework.dll','Microsoft.AspNet.Identity.Core.dll') | ForEach-Object { '/reference:' + (Join-Path $bin $_) }
& $compiler /nologo /target:exe "/out:$output" /reference:System.Web.dll /reference:System.Configuration.dll /reference:System.ComponentModel.DataAnnotations.dll @references (Join-Path $PSScriptRoot 'IntegrationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed' }
[xml]$source = Get-Content (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder($source.configuration.connectionStrings.add.connectionString)
if (!$builder.IntegratedSecurity) { throw 'Only Windows authentication is supported; no secrets copied' }
$builder['Initial Catalog'] = 'Dacn05_Test_' + [Guid]::NewGuid().ToString('N')
$doc = New-Object System.Xml.XmlDocument
$config = $doc.CreateElement('configuration'); [void]$doc.AppendChild($config)
foreach ($name in @('configSections','connectionStrings','entityFramework','runtime')) {
    $node = $source.configuration.SelectSingleNode($name)
    if ($node) { [void]$config.AppendChild($doc.ImportNode($node, $true)) }
}
$config.connectionStrings.add.connectionString = $builder.ConnectionString
$doc.Save($output + '.config')
& $output
if ($LASTEXITCODE -ne 0) { throw 'Integration tests failed; isolated database retained for diagnosis' }
