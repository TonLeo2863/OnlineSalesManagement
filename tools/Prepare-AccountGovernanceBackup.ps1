# Backup/restore preparation only. NEVER executes a migration or changes business rows.
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$stamp=[DateTime]::Now.ToString('yyyyMMdd_HHmmss')+'_'+[Guid]::NewGuid().ToString('N').Substring(0,8)
$test='Dacn05_Preflight_'+$stamp
$work=Join-Path $root ('artifacts\account-preflight\'+$stamp)
[void][IO.Directory]::CreateDirectory($work)
[xml]$web=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($web.configuration.connectionStrings.add.connectionString)
if(!$builder.IntegratedSecurity -or $builder.InitialCatalog -ne 'OnlineSalesDb' -or $builder.DataSource -ne 'DESKTOP-AUTD3A3\SQLEXPRESS'){throw 'Unexpected configured target'}
$connection=New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
function Quote([string]$s){"N'"+$s.Replace("'","''")+"'"}
function Query([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql;$a=New-Object Data.SqlClient.SqlDataAdapter($cmd);$t=New-Object Data.DataTable;[void]$a.Fill($t);return ,$t}
function Execute([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql;[void]$cmd.ExecuteNonQuery()}
function Hash([string]$text){$sha=[Security.Cryptography.SHA256]::Create();try{[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text))).Replace('-','')}finally{$sha.Dispose()}}
function Scalar([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql;[string]$cmd.ExecuteScalar()}
function Snapshot {
 $tables=Query "SELECT t.name FROM sys.tables t WHERE SCHEMA_NAME(t.schema_id)='dbo' AND t.is_ms_shipped=0 AND t.name<>'sysdiagrams' ORDER BY t.name"
 foreach($table in $tables.Rows){
  $name=[string]$table.name
  $cols=Query ("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") ORDER BY column_id")
  $pk=Query ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.is_primary_key=1 AND i.object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") ORDER BY ic.key_ordinal")
  if(!$pk.Rows.Count){throw ('No primary key for snapshot '+$name)}
  $fields=($cols.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ','
  $order=($pk.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ','
  $target='dbo.['+$name.Replace(']',']]')+']'
  [pscustomobject]@{Table=$name;Count=(Scalar ('SELECT COUNT_BIG(*) FROM '+$target));SHA256=(Hash (Scalar ('SELECT '+$fields+' FROM '+$target+' ORDER BY '+$order+' FOR XML RAW,BINARY BASE64,TYPE')))}
 }
}
function SchemaSnapshot {
 $parts=@(
 "SELECT t.name T,c.name C,ty.name Y,c.max_length L,c.precision P,c.scale S,c.is_nullable N,c.is_identity I,c.is_computed X,dc.definition D,cc.definition Computed FROM sys.tables t JOIN sys.columns c ON c.object_id=t.object_id JOIN sys.types ty ON ty.user_type_id=c.user_type_id LEFT JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id LEFT JOIN sys.computed_columns cc ON cc.object_id=c.object_id AND cc.column_id=c.column_id WHERE t.is_ms_shipped=0 ORDER BY t.name,c.column_id FOR XML RAW,TYPE",
 "SELECT t.name T,i.name I,i.type Y,i.is_unique U,i.is_primary_key P,i.is_unique_constraint C,i.is_disabled D,i.filter_definition F,ic.key_ordinal K,ic.is_descending_key DescendingKey,ic.is_included_column Included,c.name ColumnName FROM sys.tables t JOIN sys.indexes i ON i.object_id=t.object_id JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE t.is_ms_shipped=0 ORDER BY t.name,i.name,ic.index_column_id FOR XML RAW,TYPE",
 "SELECT OBJECT_NAME(f.parent_object_id) T,f.name F,OBJECT_NAME(f.referenced_object_id) R,COL_NAME(fc.parent_object_id,fc.parent_column_id) C,COL_NAME(fc.referenced_object_id,fc.referenced_column_id) RC,f.delete_referential_action D,f.update_referential_action U,f.is_disabled Disabled,f.is_not_trusted Untrusted FROM sys.foreign_keys f JOIN sys.foreign_key_columns fc ON fc.constraint_object_id=f.object_id ORDER BY T,F,fc.constraint_column_id FOR XML RAW,TYPE",
 "SELECT OBJECT_NAME(parent_object_id) T,name,definition,is_disabled,is_not_trusted FROM sys.check_constraints ORDER BY T,name FOR XML RAW,TYPE",
 "SELECT OBJECT_NAME(parent_id) T,name,is_disabled,OBJECT_DEFINITION(object_id) Definition FROM sys.triggers WHERE is_ms_shipped=0 ORDER BY T,name FOR XML RAW,TYPE",
 "SELECT OBJECT_NAME(object_id) T,name,seed_value,increment_value,last_value FROM sys.identity_columns ORDER BY T,name FOR XML RAW,TYPE"
 )
 for($i=0;$i -lt $parts.Count;$i++){[pscustomobject]@{Part=$i;SHA256=(Hash (Scalar $parts[$i]))}}
}
function CheckBaseline {
 if((Scalar "SELECT CAST(SERVERPROPERTY('ServerName') AS nvarchar(128))") -ne 'DESKTOP-AUTD3A3\SQLEXPRESS'){throw 'Wrong connected instance'}
 $ids=@((Query 'SELECT MigrationId,ContextKey FROM dbo.__MigrationHistory ORDER BY MigrationId').Rows)
 if($ids.Count -ne 2 -or $ids[0].MigrationId -ne '202610131032209_InitialCreate' -or $ids[1].MigrationId -ne '202610151148216_AddShiftManagementTables' -or @($ids | Where-Object {$_.ContextKey -ne 'OnlineSalesManagement.Migrations.Configuration'}).Count){throw 'Unexpected migration baseline'}
 if((Scalar "SELECT COUNT(*) FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AspNetUsers') AND name='IsAdministrativelySuspended'") -ne '0' -or (Scalar "SELECT COUNT(*) FROM sys.tables WHERE name='AccountAdministrationAudits'") -ne '0'){throw 'New or partial governance schema exists'}
 if((Scalar 'SELECT COUNT(*) FROM sys.foreign_keys WHERE is_disabled=1 OR is_not_trusted=1') -ne '0' -or (Scalar 'SELECT COUNT(*) FROM sys.check_constraints WHERE is_disabled=1 OR is_not_trusted=1') -ne '0'){throw 'Disabled/untrusted constraints found'}
}
$report=[ordered]@{Started=[DateTimeOffset]::Now.ToString('o');Server=$builder.DataSource;Production='OnlineSalesDb';TestDatabase=$test;Status='RUNNING';ProductionMigrationApplied=$false}
try{
 $connection.Open();CheckBaseline
 if((Scalar "SELECT state_desc FROM sys.databases WHERE name=DB_NAME()") -ne 'ONLINE'){throw 'Database not online'}
 'PASS: read-only real database/history/schema/constraint prechecks'
 $before=@(Snapshot);$schema=@(SchemaSnapshot)
 $before | ConvertTo-Json | Set-Content (Join-Path $work 'production-before.json') -Encoding UTF8
 $schema | ConvertTo-Json | Set-Content (Join-Path $work 'schema-before.json') -Encoding UTF8
 $dir=Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(4000))"
 if(!$dir){throw 'No configured backup directory'}
 $path=Join-Path $dir ('OnlineSalesDb_PreAccountGovernance_'+$stamp+'.bak');$report.BackupPath=$path
 $exists=Query ('EXEC master.dbo.xp_fileexist '+(Quote $path))
 if($exists.Rows[0][0] -ne 0){throw 'Backup filename already exists'}
 Execute ('BACKUP DATABASE [OnlineSalesDb] TO DISK='+(Quote $path)+" WITH COPY_ONLY,CHECKSUM,STOP_ON_ERROR,NOFORMAT,NOINIT,NAME=N'AccountGovernance preflight full backup'")
 $header=Query ('RESTORE HEADERONLY FROM DISK='+(Quote $path))
 if($header.Rows.Count -ne 1 -or $header.Rows[0].DatabaseName -ne 'OnlineSalesDb' -or $header.Rows[0].BackupType -ne 1 -or !$header.Rows[0].HasBackupChecksums -or !$header.Rows[0].IsCopyOnly -or $header.Rows[0].IsDamaged){throw 'Backup header failed validation'}
 $report.BackupSet=1;$report.BackupFinish=$header.Rows[0].BackupFinishDate.ToString('o')
 'PASS: new Full COPY_ONLY backup with CHECKSUM: '+$path
 Execute ('RESTORE VERIFYONLY FROM DISK='+(Quote $path)+' WITH FILE=1,CHECKSUM,STOP_ON_ERROR')
 $report.VerifyOnly='PASS';'PASS: RESTORE VERIFYONLY WITH CHECKSUM'
 $connection.ChangeDatabase('master')
 if((Scalar ('SELECT COUNT(*) FROM sys.databases WHERE name='+(Quote $test))) -ne '0'){throw 'Restore target exists'}
 $files=Query ('RESTORE FILELISTONLY FROM DISK='+(Quote $path)+' WITH FILE=1')
 $dataDir=Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))"
 $logDir=Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))"
 $moves=@();$i=0
 foreach($f in $files.Rows){$i++;if($f.Type -notin @('D','L')){throw 'Unsupported file type'};$dest=Join-Path $(if($f.Type -eq 'L'){$logDir}else{$dataDir}) ($test+'_'+$i+$(if($f.Type -eq 'L'){'.ldf'}else{'.mdf'}));$moves+='MOVE '+(Quote $f.LogicalName)+' TO '+(Quote $dest)}
 Execute ('RESTORE DATABASE ['+$test+'] FROM DISK='+(Quote $path)+' WITH FILE=1,CHECKSUM,STOP_ON_ERROR,'+($moves -join ',')+',RECOVERY')
 $report.Restore='PASS';'PASS: restored exact new backup to '+$test+' (no REPLACE)'
 $connection.ChangeDatabase($test);CheckBaseline
 $restored=@(Snapshot);$restoredSchema=@(SchemaSnapshot)
 $restored | ConvertTo-Json | Set-Content (Join-Path $work 'restored.json') -Encoding UTF8
 $restoredSchema | ConvertTo-Json | Set-Content (Join-Path $work 'schema-restored.json') -Encoding UTF8
 if(Compare-Object $before $restored -Property Table,Count,SHA256){throw 'Restore rows differ from pre-backup production snapshot; concurrent changes or mismatch'}
 if(Compare-Object $schema $restoredSchema -Property Part,SHA256){throw 'Restored schema/constraints/identity differ'}
 Execute ('DBCC CHECKDB(['+$test+']) WITH NO_INFOMSGS,ALL_ERRORMSGS')
 if((Query 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS,ALL_ERRORMSGS').Rows.Count){throw 'Constraint violations on restored database'}
 $mismatch=Scalar 'SELECT COUNT(*) FROM dbo.Products p LEFT JOIN (SELECT ProductId,SUM(CAST(QuantityChange AS bigint)) Q FROM dbo.InventoryTransactions GROUP BY ProductId) t ON t.ProductId=p.Id WHERE p.QuantityInStock<>ISNULL(t.Q,0)'
 if($mismatch -ne '0'){throw ('Existing inventory ledger mismatch: '+$mismatch)}
 $report.CheckDb='PASS';$report.Constraints='PASS';$report.Inventory='PASS'
 $connection.ChangeDatabase('OnlineSalesDb');CheckBaseline
 $after=@(Snapshot);$afterSchema=@(SchemaSnapshot)
 $after | ConvertTo-Json | Set-Content (Join-Path $work 'production-after.json') -Encoding UTF8
 if((Compare-Object $before $after -Property Table,Count,SHA256) -or (Compare-Object $schema $afterSchema -Property Part,SHA256)){throw 'Real database changed during preparation; require fresh preflight'}
 $report.DataComparison='PASS';$report.TableCount=$before.Count
 'PASS: exact row hashes/counts including Identity and MigrationHistory, schema/index/FK/check/trigger/identity match; live data unchanged during preparation'
 $raw=[IO.File]::ReadAllText((Join-Path $root 'docs\migrations\AccountGovernance.sql')).Trim()
 $reviewed=[IO.File]::ReadAllText((Join-Path $root 'docs\migrations\AccountGovernance.ReviewedApply.sql'))
 if(!$reviewed.Contains($raw) -or !$reviewed.Contains("DB_NAME() <> N'OnlineSalesDb'") -or !$reviewed.Contains('SET XACT_ABORT ON') -or !$reviewed.Contains('ROLLBACK TRANSACTION') -or !$reviewed.Contains('Migration already applied or partial schema exists')){throw 'Reviewed SQL content/guard mismatch'}
 [xml]$resource=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Migrations\202610292211500_AccountGovernance.resx')
 $model=[Convert]::FromBase64String([string]($resource.root.data | Where-Object {$_.name -eq 'Target'}).value)
 $hex=[regex]::Match($raw,'0x([0-9A-Fa-f]+)').Groups[1].Value
 $bytes=New-Object byte[] ($hex.Length/2);for($i=0;$i -lt $bytes.Length;$i++){$bytes[$i]=[Convert]::ToByte($hex.Substring(2*$i,2),16)}
 if([Convert]::ToBase64String($bytes) -ne [Convert]::ToBase64String($model)){throw 'SQL migration model metadata differs from EF resource'}
 $report.ReviewedSql='PASS';$report.ReviewedSqlSHA256=(Get-FileHash (Join-Path $root 'docs\migrations\AccountGovernance.ReviewedApply.sql') -Algorithm SHA256).Hash
 'PASS: ReviewedApply embeds exact preview and EF migration model bytes; guards reviewed, NOT executed'
 $report.Status='PASS_AWAITING_FINAL_APPROVAL'
 $before | Format-Table Table,Count
}catch{$report.Status='FAIL';$report.Error=$_.Exception.Message;throw}finally{
 $report.Finished=[DateTimeOffset]::Now.ToString('o');$report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $work 'result.json') -Encoding UTF8
 $connection.Dispose();'Evidence: '+$work
}
