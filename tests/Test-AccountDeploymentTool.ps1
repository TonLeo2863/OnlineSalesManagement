$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$stamp=[DateTime]::Now.ToString('yyyyMMdd_HHmmss')
$work=Join-Path $root ('artifacts\deployment-tool-tests\'+$stamp)
[void][IO.Directory]::CreateDirectory($work)
$tokens=$null;$errors=$null
$ast=[Management.Automation.Language.Parser]::ParseFile((Join-Path $root 'tools\Prepare-AccountGovernanceBackup.ps1'),[ref]$tokens,[ref]$errors)
foreach($f in $ast.FindAll({param($n) $n -is [Management.Automation.Language.FunctionDefinitionAst]},$false)){. ([scriptblock]::Create($f.Extent.Text))}
[xml]$web=Get-Content -Encoding UTF8 (Join-Path $root 'OnlineSalesManagement\Web.config')
$builder=New-Object Data.SqlClient.SqlConnectionStringBuilder($web.configuration.connectionStrings.add.connectionString)
if($builder.DataSource -ne 'DESKTOP-AUTD3A3\SQLEXPRESS' -or $builder.InitialCatalog -ne 'OnlineSalesDb' -or !$builder.IntegratedSecurity){throw 'Wrong source'}
$builder['MultipleActiveResultSets']=$false
$connection=New-Object Data.SqlClient.SqlConnection($builder.ConnectionString)
$count=0
function Check([bool]$ok,[string]$text){if(!$ok){throw ('FAIL: '+$text)};$script:count++;'PASS: '+$text}
function RestoreNew([string]$name){
 if($name -notmatch '^Dacn05_Restore_[0-9_]+$'){throw 'Unsafe test target'}
 $connection.ChangeDatabase('master')
 if((Scalar ('SELECT COUNT(*) FROM sys.databases WHERE name='+(Quote $name))) -ne '0'){throw 'Test target exists'}
 $files=Query ('RESTORE FILELISTONLY FROM DISK='+(Quote $preflight.BackupPath)+' WITH FILE=1')
 $dataDir=Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(4000))"
 $logDir=Scalar "SELECT CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS nvarchar(4000))"
 $moves=@();$i=0
 foreach($file in $files.Rows){$i++;if($file.Type -notin @('D','L')){throw 'Unsupported file'};$directory=if($file.Type -eq 'L'){$logDir}else{$dataDir};$extension=if($file.Type -eq 'L'){'.ldf'}else{'.mdf'};$destination=Join-Path $directory ($name+'_'+$i+$extension);$moves+='MOVE '+(Quote $file.LogicalName)+' TO '+(Quote $destination)}
 Execute ('RESTORE DATABASE ['+$name+'] FROM DISK='+(Quote $preflight.BackupPath)+' WITH FILE=1,CHECKSUM,STOP_ON_ERROR,RECOVERY,'+($moves -join ','))
 $connection.ChangeDatabase($name);CheckBaseline
 $snapshot=@(Snapshot);$schema=@(SchemaSnapshot)
 Check (!(Compare-Object $reference $snapshot -Property Table,Count,SHA256)) ($name+' fresh restore equals pre-migration backup including Identity')
 Check (!(Compare-Object $schemaReference $schema -Property Part,SHA256)) ($name+' pre-migration schema matches backup')
}
try{
 $connection.Open();CheckBaseline
 $sourceBefore=@(Snapshot);$sourceSchema=@(SchemaSnapshot)
 $baseline=(Get-ChildItem (Join-Path $root 'artifacts\account-preflight') -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
 $reference=Get-Content (Join-Path $baseline 'production-before.json') -Raw | ConvertFrom-Json
 $schemaReference=Get-Content (Join-Path $baseline 'schema-before.json') -Raw | ConvertFrom-Json
 if((Compare-Object $reference $sourceBefore -Property Table,Count,SHA256) -or (Compare-Object $schemaReference $sourceSchema -Property Part,SHA256)){
  & (Join-Path $root 'tools\Prepare-AccountGovernanceBackup.ps1')
  $baseline=(Get-ChildItem (Join-Path $root 'artifacts\account-preflight') -Directory | Sort-Object Name -Descending | Select-Object -First 1).FullName
  $reference=Get-Content (Join-Path $baseline 'production-before.json') -Raw | ConvertFrom-Json
  $schemaReference=Get-Content (Join-Path $baseline 'schema-before.json') -Raw | ConvertFrom-Json
 }
 $preflight=Get-Content (Join-Path $baseline 'result.json') -Raw | ConvertFrom-Json
 Check ($preflight.Status -eq 'PASS_AWAITING_FINAL_APPROVAL') 'latest preflight passed; real DB read-only, backup current'
 Execute ('RESTORE VERIFYONLY FROM DISK='+(Quote $preflight.BackupPath)+' WITH FILE=1,CHECKSUM,STOP_ON_ERROR')
 $success='Dacn05_Restore_'+$stamp+'_01';$failure='Dacn05_Restore_'+$stamp+'_02'
 RestoreNew $failure
 $busyBlocked=$false
 try{& (Join-Path $root 'tools\Apply-ApprovedAccountGovernance.ps1') -Approved -TestDatabase $failure *> (Join-Path $work 'user-session-blocked.log')}catch{$busyBlocked=$_.Exception.Message -like '*Other SQL connections/transactions exist*'}
 Check $busyBlocked 'idle user SQL connection still blocks deployment before migration'
 $connection.ChangeDatabase('master')
 $failed=$false
 try{& (Join-Path $root 'tools\Apply-ApprovedAccountGovernance.ps1') -Approved -TestDatabase $failure -InjectFailure *> (Join-Path $work 'injected-failure.log')}catch{$failed=$_.Exception.Message -like '*Test-only failure after DDL and history insert*'}
 Check $failed 'injected SQL THROW reached after DDL and history INSERT, before COMMIT'
 $connection.ChangeDatabase($failure);CheckBaseline
 $rollback=@(Snapshot);$rollbackSchema=@(SchemaSnapshot)
 Check (!(Compare-Object $reference $rollback -Property Table,Count,SHA256)) 'rollback preserved every original row and migration history'
 Check (!(Compare-Object $schemaReference $rollbackSchema -Property Part,SHA256)) 'rollback removed both new column/table and restored schema exactly'
 Check ((Scalar 'SELECT @@TRANCOUNT') -eq '0') 'failure leaves no open transaction on verification session'
 Execute ('DBCC CHECKDB(['+$failure+']) WITH NO_INFOMSGS,ALL_ERRORMSGS')
 RestoreNew $success
 $connection.ChangeDatabase('master')
 & (Join-Path $root 'tools\Apply-ApprovedAccountGovernance.ps1') -Approved -TestDatabase $success *> (Join-Path $work 'first-apply.log')
 $connection.ChangeDatabase($success)
 Check ((Scalar 'SELECT COUNT(*) FROM dbo.__MigrationHistory') -eq '3' -and (Scalar "SELECT COUNT(*) FROM dbo.__MigrationHistory WHERE MigrationId='202610292211500_AccountGovernance'") -eq '1') 'first Apply committed exactly one governance migration'
 Check ((Scalar 'SELECT COUNT(*) FROM dbo.AspNetUsers WHERE IsAdministrativelySuspended<>0') -eq '0') 'all original accounts default to not suspended'
 $columns=Query "SELECT c.name,t.name TypeName,c.max_length,c.is_nullable,c.is_identity FROM sys.columns c JOIN sys.types t ON t.user_type_id=c.user_type_id WHERE c.object_id=OBJECT_ID('dbo.AccountAdministrationAudits') ORDER BY c.column_id"
 Check ($columns.Rows.Count -eq 6 -and $columns.Rows[0].is_identity -and $columns.Rows[1].max_length -eq 256 -and $columns.Rows[2].max_length -eq 256 -and $columns.Rows[3].max_length -eq 40 -and $columns.Rows[4].max_length -eq 1000 -and $columns.Rows[5].TypeName -eq 'datetime' -and @($columns.Rows | Where-Object {$_.is_nullable}).Count -eq 0) 'audit table columns/types/lengths/nullability/identity match migration'
 Check ((Scalar "SELECT COUNT(*) FROM sys.indexes WHERE object_id=OBJECT_ID('dbo.AccountAdministrationAudits') AND is_primary_key=1") -eq '1') 'audit primary key exists'
 Check ((Scalar 'SELECT COUNT(*) FROM dbo.AccountAdministrationAudits') -eq '0') 'new audit table initially empty'
 $applied=@(Snapshot);$appliedSchema=@(SchemaSnapshot)
 $connection.ChangeDatabase('master');$blocked=$false
 try{& (Join-Path $root 'tools\Apply-ApprovedAccountGovernance.ps1') -Approved -TestDatabase $success *> (Join-Path $work 'second-apply-blocked.log')}catch{$blocked=$_.Exception.Message -like '*Unexpected migration baseline*'}
 Check $blocked 'second tool invocation blocked before executing migration'
 $connection.ChangeDatabase($success)
 $again=@(Snapshot);$againSchema=@(SchemaSnapshot)
 Check (!(Compare-Object $applied $again -Property Table,Count,SHA256) -and !(Compare-Object $appliedSchema $againSchema -Property Part,SHA256)) 'second invocation changes no rows/schema'
 # Independently exercise the approved SQL's own repeat guard; no file edit, test target guard only in memory.
 $sql=[IO.File]::ReadAllText((Join-Path $root 'docs\migrations\AccountGovernance.ReviewedApply.sql')).Replace("DB_NAME() <> N'OnlineSalesDb'",("DB_NAME() <> N'"+$success+"'"))
 $guarded=$false;try{Execute $sql}catch{$guarded=$_.Exception.Message -like '*Unexpected migration history*'}
 Check $guarded 'ReviewedApply repeat guard itself rejects already-upgraded database'
 Check ((Scalar 'SELECT @@TRANCOUNT') -eq '0') 'guard rejection rolls transaction back'
 $mismatch=Scalar 'SELECT COUNT(*) FROM dbo.Products p LEFT JOIN (SELECT ProductId,SUM(CAST(QuantityChange AS bigint)) Q FROM dbo.InventoryTransactions GROUP BY ProductId) t ON t.ProductId=p.Id WHERE p.QuantityInStock<>ISNULL(t.Q,0)'
 Check ($mismatch -eq '0') 'restored inventory agrees with InventoryTransactions'
 Execute ('DBCC CHECKDB(['+$success+']) WITH NO_INFOMSGS,ALL_ERRORMSGS')
 Check ((Query 'DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS,ALL_ERRORMSGS').Rows.Count -eq 0) 'CHECKDB and constraints pass both test databases'
 $restoreWork=Join-Path $root ('artifacts\restore-verification\'+$success);[void][IO.Directory]::CreateDirectory($restoreWork)
 $applied | ConvertTo-Json | Set-Content (Join-Path $restoreWork 'after.json') -Encoding UTF8
 $testBuilder=New-Object Data.SqlClient.SqlConnectionStringBuilder($builder.ConnectionString);$testBuilder['Initial Catalog']=$success
 $testBuilder.ConnectionString | Set-Content (Join-Path $restoreWork 'test-connection.txt') -Encoding UTF8
 $connection.ChangeDatabase('OnlineSalesDb');CheckBaseline
 $sourceAfter=@(Snapshot);$schemaAfter=@(SchemaSnapshot)
 Check (!(Compare-Object $sourceBefore $sourceAfter -Property Table,Count,SHA256) -and !(Compare-Object $sourceSchema $schemaAfter -Property Part,SHA256)) 'real OnlineSalesDb remains completely unchanged, migration not applied'
 [ordered]@{Status='PASS';Checks=$count;SuccessDatabase=$success;RollbackDatabase=$failure;Backup=$preflight.BackupPath;Preflight=$baseline;Finished=[DateTimeOffset]::Now.ToString('o')} | ConvertTo-Json | Set-Content (Join-Path $work 'result.json') -Encoding UTF8
 'TOTAL DEPLOYMENT TOOL PASS: '+$count
 'Evidence: '+$work
}finally{$connection.Dispose()}
