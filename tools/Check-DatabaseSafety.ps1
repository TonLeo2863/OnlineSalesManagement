$ErrorActionPreference='Stop'
[xml]$web=Get-Content -Encoding UTF8 (Join-Path $PSScriptRoot '..\OnlineSalesManagement\Web.config')
$connection=New-Object Data.SqlClient.SqlConnection($web.configuration.connectionStrings.add.connectionString)
try {
 $connection.Open()
 $cmd=$connection.CreateCommand()
 $cmd.CommandText="SELECT DB_NAME() AS DatabaseName, CAST(SERVERPROPERTY('ProductVersion') AS nvarchar(50)) AS ServerVersion; SELECT TOP(3) backup_finish_date, type, is_copy_only, has_backup_checksums FROM msdb.dbo.backupset WHERE database_name=DB_NAME() ORDER BY backup_finish_date DESC"
 $reader=$cmd.ExecuteReader();do { $table=New-Object Data.DataTable;$table.Load($reader);$table | Format-Table -AutoSize } while(!$reader.IsClosed)
} finally { $connection.Dispose() }
