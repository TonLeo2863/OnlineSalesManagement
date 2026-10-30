param(
 [switch]$Approved,
 [Parameter(Mandatory=$true,ParameterSetName='Production')][switch]$Production,
 [Parameter(Mandatory=$true,ParameterSetName='Test')][ValidatePattern('^Dacn05_Restore_[0-9_]+$')][string]$TestDatabase,
 [Parameter(ParameterSetName='Test')][switch]$InjectFailure
)
$ErrorActionPreference='Stop'
if(!$Approved){throw 'Explicit approval required. No action taken.'}
if(!$Production -and !$TestDatabase){throw 'Select an explicit production or restored-test target'}
$root=Split-Path $PSScriptRoot -Parent
$expected='F1C448BB78AF3F6B7E5FE695EDF3A68F58EDDE146EB363B67793F6965E159907'
$sqlPath=Join-Path $root 'docs\migrations\AccountGovernance.ReviewedApply.sql'
if((Get-FileHash $sqlPath -Algorithm SHA256).Hash -ne $expected){throw 'Reviewed SQL hash changed. Stop.'}
# Import only helper function definitions; NEVER run the backup script top-level here.
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'tools\Prepare-AccountGovernanceBackup.ps1'),[ref]$tokens,[ref]$errors)
if($errors.Count){throw 'Backup helper parser errors'}
foreach($f in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($f.Extent.Text))}
$work=Join-Path $root ('artifacts\account-apply\'+[DateTime]::Now.ToString('yyyyMMdd_HHmmss_fff'))
[void][IO.Directory]::CreateDirectory($work)
$baseline=(Get-ChildItem (Join-Path $root 'artifacts\account-preflight') -Directory | Where-Object {Test-Path (Join-Path $_.FullName 'result.json')} | Sort-Object Name -Descending | Select-Object -First 1).FullName
$report=[ordered]@{Started=[DateTimeOffset]::Now.ToString('o');ScriptSHA256=$expected;ApplyInvocations=0;Committed=$false;Status='STARTING'}
[xml]$web=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($web.configuration.connectionStrings.add.connectionString)
if($builder.DataSource -ne 'DESKTOP-AUTD3A3\SQLEXPRESS' -or $builder.InitialCatalog -ne 'OnlineSalesDb' -or !$builder.IntegratedSecurity){throw 'Wrong configured target'}
# Deployment uses ONE SqlConnection, with MARS disabled only here, not in website Web.config.
$builder['MultipleActiveResultSets']=$false
if($TestDatabase){$builder['Initial Catalog']=$TestDatabase}
$report.TargetDatabase=$builder.InitialCatalog;$report.MARS=$false;$report.FailureInjection=[bool]$InjectFailure
$connection=New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
function AssertQuiet {
 $iis=@(Get-CimInstance Win32_Process -Filter "name='iisexpress.exe'")
 if($Production -and $iis.Count){throw 'IIS Express is running; stop before continuing'}
 # Service Broker/background system sessions are not application writers. User sessions,
 # including idle sessions and sessions holding a transaction, must still block deployment.
 $others=Query "SELECT DISTINCT s.session_id FROM sys.dm_exec_sessions s LEFT JOIN sys.dm_exec_requests r ON r.session_id=s.session_id LEFT JOIN sys.dm_tran_session_transactions st ON st.session_id=s.session_id LEFT JOIN sys.dm_tran_database_transactions dt ON dt.transaction_id=st.transaction_id AND dt.database_id=DB_ID() WHERE s.is_user_process=1 AND s.session_id<>@@SPID AND (s.database_id=DB_ID() OR r.database_id=DB_ID() OR dt.database_id=DB_ID())"
 if($others.Rows.Count){throw 'Other SQL connections/transactions exist on target database. Stop; no session killed.'}
}
function OldDataSnapshot {
 foreach($map in $maps){
  $table='dbo.['+$map.Table.Replace(']',']]')+']'
  $where=if($map.Table -eq '__MigrationHistory'){" WHERE MigrationId<>N'202610292211500_AccountGovernance'"}else{''}
  [pscustomobject]@{Table=$map.Table;Count=(Scalar ('SELECT COUNT_BIG(*) FROM '+$table+$where));SHA256=(Hash (Scalar ('SELECT '+$map.Fields+' FROM '+$table+$where+' ORDER BY '+$map.Keys+' FOR XML RAW,BINARY BASE64,TYPE')))}
 }
}
try{
 $connection.Open();CheckBaseline;AssertQuiet
 if((Scalar 'SELECT DB_NAME()') -ne $builder.InitialCatalog){throw 'Wrong connected database'}
 'PASS: exact target, deployment MARS=False, no other DB sessions or transactions'
 $live=@(Snapshot);$liveSchema=@(SchemaSnapshot)
 $old=Get-Content (Join-Path $baseline 'production-before.json') -Raw | ConvertFrom-Json
 $oldSchema=Get-Content (Join-Path $baseline 'schema-before.json') -Raw | ConvertFrom-Json
 $changed=(Compare-Object $old $live -Property Table,Count,SHA256) -or (Compare-Object $oldSchema $liveSchema -Property Part,SHA256)
 if($changed){
  if($TestDatabase){throw 'Restored test data does not match verified backup baseline'}
  'Database changed since 06:56. Creating a new verified full backup/restore before any migration.'
  & (Join-Path $root 'tools\Prepare-AccountGovernanceBackup.ps1')
  $baseline=(Get-ChildItem (Join-Path $root 'artifacts\account-preflight') -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
 }else{'PASS: all data/schema/identity counters unchanged since verified 06:56 backup'}
 $preflight=Get-Content (Join-Path $baseline 'result.json') -Raw | ConvertFrom-Json
 if($preflight.Status -ne 'PASS_AWAITING_FINAL_APPROVAL' -or $preflight.ReviewedSqlSHA256 -ne $expected){throw 'Preflight not passed'}
 $report.BackupPath=$preflight.BackupPath;$report.Preflight=$baseline;$report.ChangedSince0656=[bool]$changed
 Execute ('RESTORE VERIFYONLY FROM DISK='+(Quote $preflight.BackupPath)+' WITH FILE=1,CHECKSUM,STOP_ON_ERROR')
 AssertQuiet;CheckBaseline
 # ReviewedApply is the ONLY transaction owner. No outer or cross-batch transaction.
 Execute 'SET LOCK_TIMEOUT 10000;'
 $tables=Query "SELECT name FROM sys.tables WHERE is_ms_shipped=0 AND name<>'sysdiagrams' ORDER BY name"
 AssertQuiet
 $before=@(Snapshot);$schemaBefore=@(SchemaSnapshot)
 $reference=Get-Content (Join-Path $baseline 'production-before.json') -Raw | ConvertFrom-Json
 $schemaReference=Get-Content (Join-Path $baseline 'schema-before.json') -Raw | ConvertFrom-Json
 if((Compare-Object $reference $before -Property Table,Count,SHA256) -or (Compare-Object $schemaReference $schemaBefore -Property Part,SHA256)){throw 'Data changed after backup; stop before migration'}
 $before | ConvertTo-Json | Set-Content (Join-Path $work 'before.json') -Encoding UTF8
 $maps=@(foreach($t in $tables.Rows){
  $name=[string]$t.name
  $cols=Query ("SELECT name FROM sys.columns WHERE object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") ORDER BY column_id")
  $keys=Query ("SELECT c.name FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id=i.object_id AND ic.index_id=i.index_id JOIN sys.columns c ON c.object_id=ic.object_id AND c.column_id=ic.column_id WHERE i.is_primary_key=1 AND i.object_id=OBJECT_ID("+(Quote ('dbo.'+$name))+") ORDER BY ic.key_ordinal")
  [pscustomobject]@{Table=$name;Fields=(($cols.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ',');Keys=(($keys.Rows | ForEach-Object {'['+([string]$_.name).Replace(']',']]')+']'}) -join ',')}
 })
 $sql=[IO.File]::ReadAllText($sqlPath)
 if([regex]::IsMatch($sql,'(?im)^\s*GO(?:\s|$)') -or ([regex]::Matches($sql,'(?i)\bBEGIN\s+TRANSACTION\b')).Count -ne 1 -or ([regex]::Matches($sql,'(?i)\bCOMMIT\s+TRANSACTION\b')).Count -ne 1 -or !$sql.Contains('ROLLBACK TRANSACTION')){throw 'Unexpected SQL batch/transaction structure'}
 if($TestDatabase){
  # Only target guard is adapted IN MEMORY for explicitly allowlisted restored test DBs.
  $sql=$sql.Replace("DB_NAME() <> N'OnlineSalesDb'",("DB_NAME() <> N'"+$TestDatabase+"'"))
  if($InjectFailure){
   # Fail AFTER DDL and metadata INSERT but BEFORE the script's only COMMIT.
   $sql=$sql.Replace(' COMMIT TRANSACTION;'," ;THROW 51999, 'Test-only failure after DDL and history insert, before commit.', 1;`r`n COMMIT TRANSACTION;")
  }
 }
 AssertQuiet
 if((Scalar 'SELECT @@TRANCOUNT') -ne '0'){throw 'Unexpected ambient transaction'}
 $report.ApplyInvocations=1;$report.Status='EXECUTING'
 $report | ConvertTo-Json | Set-Content (Join-Path $work 'result.json') -Encoding UTF8
 Execute $sql # ONE batch, ONE script-owned transaction; unchanged reviewed bytes in production
 $report.Committed=$true
 if((Scalar 'SELECT @@TRANCOUNT') -ne '0'){throw 'Script leaked a transaction'}
 $after=@(OldDataSnapshot)
 $after | ConvertTo-Json | Set-Content (Join-Path $work 'after-old-columns.json') -Encoding UTF8
 if(Compare-Object $before $after -Property Table,Count,SHA256){throw 'Post-commit data mismatch; STOP, do not retry or restore automatically'}
 if((Scalar "SELECT COUNT(*) FROM dbo.__MigrationHistory WHERE MigrationId=N'202610292211500_AccountGovernance' AND ContextKey=N'OnlineSalesManagement.Migrations.Configuration' AND ProductVersion=N'6.5.1'") -ne '1' -or (Scalar 'SELECT COUNT(*) FROM dbo.__MigrationHistory') -ne '3'){throw 'Migration history postcondition failed'}
 if((Scalar 'SELECT COUNT(*) FROM dbo.AspNetUsers WHERE IsAdministrativelySuspended<>0') -ne '0' -or (Scalar 'SELECT COUNT(*) FROM dbo.AccountAdministrationAudits') -ne '0'){throw 'Unexpected governance data'}
 $column=Query "SELECT c.is_nullable,t.name TypeName,dc.definition FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id JOIN sys.default_constraints dc ON dc.object_id=c.default_object_id WHERE c.object_id=OBJECT_ID('dbo.AspNetUsers') AND c.name='IsAdministrativelySuspended'"
 if($column.Rows.Count -ne 1 -or $column.Rows[0].is_nullable -or $column.Rows[0].TypeName -ne 'bit' -or $column.Rows[0].definition -notmatch '^\(+0\)+$'){throw 'Suspension column type/nullability/default incorrect'}
 $report.Committed=$true;$report.Status='PASS_COMMITTED';$report.DataPreserved='PASS';$report.BeforeTables=$before.Count
 'PASS: reviewed script executed ONCE and committed; all original rows/columns preserved, new history exactly once'
}catch{
 $report.Status='FAIL_STOPPED';$report.Error=$_.Exception.Message
 # Defensive cleanup only if the script left a transaction open; never restore/reset/delete.
 try{if($connection.State -eq 'Open'){Execute 'IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;'}}catch{}
 throw
}finally{
 $report.Finished=[DateTimeOffset]::Now.ToString('o');$report | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $work 'result.json') -Encoding UTF8
 $connection.Dispose();'Evidence: '+$work
}
