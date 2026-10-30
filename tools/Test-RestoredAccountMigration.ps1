param([string]$TestDatabase = ('Dacn05_Restore_' + [DateTime]::Now.ToString('yyyyMMdd_HHmmss')))
$ErrorActionPreference='Stop'
if($TestDatabase -notmatch '^Dacn05_Restore_[0-9_]+$'){throw 'Only a new Dacn05_Restore_* database is allowed'}
$root=Split-Path $PSScriptRoot -Parent
$work=Join-Path $root ('artifacts\restore-verification\'+$TestDatabase)
[void][IO.Directory]::CreateDirectory($work)
[xml]$web=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($web.configuration.connectionStrings.add.connectionString)
if(!$builder.IntegratedSecurity -or $builder.InitialCatalog -ne 'OnlineSalesDb'){throw 'Expected configured OnlineSalesDb with Windows authentication'}
$builder['Initial Catalog']='master'
$connection=New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
function Query([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql;$a=New-Object Data.SqlClient.SqlDataAdapter($cmd);$t=New-Object Data.DataTable;[void]$a.Fill($t);return ,$t}
function Execute([string]$sql){$cmd=$connection.CreateCommand();$cmd.CommandTimeout=300;$cmd.CommandText=$sql;[void]$cmd.ExecuteNonQuery()}
function Quote([string]$s){return "N'"+$s.Replace("'","''")+"'"}
function Snapshot {
 $tables=Query "SELECT t.name FROM sys.tables t WHERE SCHEMA_NAME(t.schema_id)='dbo' AND t.name NOT IN ('__MigrationHistory','AccountAdministrationAudits','sysdiagrams') ORDER BY t.name"
 foreach($table in $tables.Rows){
  $name=[string]$table.name
  $cols=Query ("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") AND name<>'IsAdministrativelySuspended' ORDER BY column_id")
  $fields=($cols.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ','
  $pk=Query ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON i.object_id=ic.object_id AND i.index_id=ic.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.is_primary_key=1 AND i.object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") ORDER BY ic.key_ordinal")
  if($pk.Rows.Count -eq 0){throw ('No primary key for snapshot '+$name)}
  $order=($pk.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ','
  $cmd=$connection.CreateCommand();$cmd.CommandText='SELECT '+$fields+' FROM dbo.['+$name.Replace(']',']]')+'] ORDER BY '+$order+' FOR XML RAW,BINARY BASE64,TYPE'
  $xml=[string]$cmd.ExecuteScalar();$sha=[Security.Cryptography.SHA256]::Create()
  try{$hash=[BitConverter]::ToString($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($xml))).Replace('-','')}finally{$sha.Dispose()}
  $count=Query ('SELECT COUNT_BIG(*) AS N FROM dbo.['+$name.Replace(']',']]')+']')
  [pscustomobject]@{Table=$name;Count=$count.Rows[0].N;SHA256=$hash}
 }
}
try{
 $connection.Open()
 if((Query ('SELECT name FROM sys.databases WHERE name='+(Quote $TestDatabase))).Rows.Count){throw 'Target already exists; refusing restore/reapply'}
 $backup=Query "SELECT TOP(1) m.physical_device_name,b.position FROM msdb.dbo.backupset b JOIN msdb.dbo.backupmediafamily m ON b.media_set_id=m.media_set_id WHERE b.database_name='OnlineSalesDb' AND b.type='D' AND m.physical_device_name LIKE '%OnlineSalesDb.bak' ORDER BY b.backup_finish_date DESC"
 if($backup.Rows.Count -ne 1){throw 'Backup not located in actual server metadata'}
 $path=[string]$backup.Rows[0].physical_device_name;$position=[int]$backup.Rows[0].position
 $header=Query ('RESTORE HEADERONLY FROM DISK='+(Quote $path))
 $selected=@($header.Rows | Where-Object {$_.Position -eq $position -and $_.DatabaseName -eq 'OnlineSalesDb' -and $_.BackupType -eq 1})
 if($selected.Count -ne 1){throw 'Backup header does not match metadata'}
 $selected | Select-Object Position,DatabaseName,BackupFinishDate,HasBackupChecksums | Format-Table
 Execute ('RESTORE VERIFYONLY FROM DISK='+(Quote $path)+' WITH FILE='+$position)
 'PASS: RESTORE VERIFYONLY (backup has no checksum; restore rehearsal still required)'
 $files=Query ('RESTORE FILELISTONLY FROM DISK='+(Quote $path)+' WITH FILE='+$position)
 $dirs=Query "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000)) D,CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000)) L"
 $moves=@();$index=0
 foreach($f in $files.Rows){$index++;if($f.Type -notin @('D','L')){throw 'Unsupported backup file type'};$dir=if($f.Type -eq 'L'){$dirs.Rows[0].L}else{$dirs.Rows[0].D};$extension=if($f.Type -eq 'L'){'.ldf'}else{'.mdf'};$destination=Join-Path $dir ($TestDatabase+'_'+$index+$extension);$moves+='MOVE '+(Quote $f.LogicalName)+' TO '+(Quote $destination)}
 Execute ('RESTORE DATABASE ['+$TestDatabase+'] FROM DISK='+(Quote $path)+' WITH FILE='+$position+','+($moves -join ',')+',RECOVERY')
 'PASS: restored separate database '+$TestDatabase+' on '+$connection.DataSource
 $connection.ChangeDatabase($TestDatabase)
 $before=@(Snapshot);$before | ConvertTo-Json | Set-Content (Join-Path $work 'before.json') -Encoding UTF8
 $history=Query 'SELECT MigrationId,ContextKey FROM dbo.__MigrationHistory ORDER BY MigrationId'
 $history | Format-Table
 if($history.Rows.Count -ne 2 -or $history.Rows[1].MigrationId -ne '202610151148216_AddShiftManagementTables'){throw 'Unexpected restored migration baseline'}
 if((Query "SELECT name FROM sys.tables WHERE name='AccountAdministrationAudits'").Rows.Count -or (Query "SELECT name FROM sys.columns WHERE object_id=OBJECT_ID('dbo.AspNetUsers') AND name='IsAdministrativelySuspended'").Rows.Count){throw 'Partial migration detected; refusing'}
 $script=[IO.File]::ReadAllText((Join-Path $root 'docs\migrations\AccountGovernance.sql'))
 Execute ("SET XACT_ABORT ON; BEGIN TRY BEGIN TRANSACTION; "+$script+"; COMMIT; END TRY BEGIN CATCH IF @@TRANCOUNT>0 ROLLBACK; THROW; END CATCH")
 'PASS: AccountGovernance SQL applied once, no EF Update-Database or Seed called'
 $after=@(Snapshot);$after | ConvertTo-Json | Set-Content (Join-Path $work 'after.json') -Encoding UTF8
 if(Compare-Object $before $after -Property Table,Count,SHA256){throw 'Data snapshot changed'}
 'PASS: all original columns/rows including Identity credentials and business data unchanged'
 $checks=Query "SELECT (SELECT COUNT(*) FROM dbo.__MigrationHistory WHERE MigrationId='202610292211500_AccountGovernance') MigrationCount,(SELECT COUNT(*) FROM dbo.AspNetUsers WHERE IsAdministrativelySuspended<>0) SuspendedUsers,(SELECT COUNT(*) FROM dbo.AccountAdministrationAudits) AuditRows"
 $checks | Format-Table
 if($checks.Rows[0].MigrationCount -ne 1 -or $checks.Rows[0].SuspendedUsers -ne 0 -or $checks.Rows[0].AuditRows -ne 0){throw 'Migration postconditions failed'}
 $builder['Initial Catalog']=$TestDatabase
 $builder.ConnectionString | Set-Content (Join-Path $work 'test-connection.txt') -Encoding UTF8
 'Evidence: '+$work
}finally{$connection.Dispose()}
