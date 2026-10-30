$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'tools\Prepare-AccountGovernanceBackup.ps1'),[ref]$tokens,[ref]$errors)
foreach($f in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($f.Extent.Text))}
[xml]$web=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($web.configuration.connectionStrings.add.connectionString)
if($builder.DataSource -ne 'DESKTOP-AUTD3A3\SQLEXPRESS' -or $builder.InitialCatalog -ne 'OnlineSalesDb' -or !$builder.IntegratedSecurity){throw 'Wrong target'}
$connection=New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
$count=0
function Check([bool]$ok,[string]$text){if(!$ok){throw ('FAIL: '+$text)};$script:count++;'PASS: '+$text}
try{
 $connection.Open()
 Check ((Scalar "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(128))") -eq 'DESKTOP-AUTD3A3\SQLEXPRESS' -and (Scalar 'SELECT DB_NAME()') -eq 'OnlineSalesDb') 'exact live server/database'
 $baseline=Join-Path $root 'artifacts\account-apply\20261030_083639_057'
 $before=Get-Content (Join-Path $baseline 'before.json') -Raw | ConvertFrom-Json
 $now=@(foreach($row in $before){
  $name=[string]$row.Table;$table='dbo.['+$name.Replace(']',']]')+']'
  $cols=Query ("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") AND name<>'IsAdministrativelySuspended' ORDER BY column_id")
  $keys=Query ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.is_primary_key=1 AND i.object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") ORDER BY ic.key_ordinal")
  $fields=($cols.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ','
  $order=($keys.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ','
  $where=if($name -eq '__MigrationHistory'){" WHERE MigrationId<>N'202610292211500_AccountGovernance'"}else{''}
  [pscustomobject]@{Table=$name;Count=(Scalar ('SELECT COUNT_BIG(*) FROM '+$table+$where));SHA256=(Hash (Scalar ('SELECT '+$fields+' FROM '+$table+$where+' ORDER BY '+$order+' FOR XML RAW,BINARY BASE64,TYPE')))}
 })
 Check (!(Compare-Object $before $now -Property Table,Count,SHA256)) 'all 22 original tables/columns preserved including credential hashes, stamps, roles and business rows'
 $now | ConvertTo-Json | Set-Content (Join-Path $baseline 'post-verify-old-data.json') -Encoding UTF8
 Check ((Scalar 'SELECT COUNT(*) FROM dbo.__MigrationHistory') -eq '3' -and (Scalar "SELECT COUNT(*) FROM dbo.__MigrationHistory WHERE MigrationId='202610292211500_AccountGovernance' AND ContextKey='OnlineSalesManagement.Migrations.Configuration' AND ProductVersion='6.5.1'") -eq '1') 'history has precisely one new expected migration'
 [xml]$resx=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Migrations\202610292211500_AccountGovernance.resx')
 $model=Query "SELECT Model FROM dbo.__MigrationHistory WHERE MigrationId='202610292211500_AccountGovernance'"
 Check ([Convert]::ToBase64String($model.Rows[0].Model) -eq [string]($resx.root.data | Where-Object {$_.name -eq 'Target'}).value) 'production migration model bytes match EF resource exactly'
 $column=Query "SELECT t.name TypeName,c.is_nullable,d.definition FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id JOIN sys.default_constraints d ON d.object_id=c.default_object_id WHERE c.object_id=OBJECT_ID('dbo.AspNetUsers') AND c.name='IsAdministrativelySuspended'"
 Check ($column.Rows.Count -eq 1 -and $column.Rows[0].TypeName -eq 'bit' -and !$column.Rows[0].is_nullable -and $column.Rows[0].definition -match '^\(+0\)+$') 'suspension column bit NOT NULL DEFAULT 0'
 $columns=Query "SELECT c.name,t.name TypeName,c.max_length,c.is_nullable,c.is_identity FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID('dbo.AccountAdministrationAudits') ORDER BY c.column_id"
 Check ($columns.Rows.Count -eq 6 -and (($columns.Rows | ForEach-Object {$_.name}) -join ',') -eq 'Id,TargetUserId,ActorUserId,Action,Reason,OccurredUtc' -and $columns.Rows[0].TypeName -eq 'int' -and $columns.Rows[0].is_identity -and $columns.Rows[1].max_length -eq 256 -and $columns.Rows[2].max_length -eq 256 -and $columns.Rows[3].max_length -eq 40 -and $columns.Rows[4].max_length -eq 1000 -and $columns.Rows[5].TypeName -eq 'datetime' -and @($columns.Rows | Where-Object {$_.is_nullable}).Count -eq 0) 'audit schema matches approved migration'
 Check ((Scalar "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AccountAdministrationAudits') AND is_primary_key=1") -eq '1') 'audit primary key created'
 Check ((Scalar 'SELECT COUNT(*) FROM dbo.AccountAdministrationAudits') -eq '0' -and (Scalar 'SELECT COUNT(*) FROM dbo.AspNetUsers WHERE IsAdministrativelySuspended<>0') -eq '0') 'audit initially empty, no account suspended by upgrade'
 Check ((Scalar 'SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1') -eq '0' -and (Scalar 'SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1') -eq '0') 'existing constraints remain enabled/trusted'
 Check ((Query 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS,ALL_ERRORMSGS').Rows.Count -eq 0) 'read-only constraint validation passes'
 Check ((Scalar 'SELECT COUNT(*) FROM dbo.Products p LEFT JOIN (SELECT ProductId,SUM(CAST(QuantityChange AS bigint)) Q FROM dbo.InventoryTransactions GROUP BY ProductId) t ON t.ProductId=p.Id WHERE p.QuantityInStock<>ISNULL(t.Q,0)') -eq '0') 'all production inventory reconciles with transactions'
 'TOTAL PRODUCTION READ-ONLY SQL PASS: '+$count
}finally{$connection.Dispose()}
