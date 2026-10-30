$ErrorActionPreference='Stop'
$tool=Join-Path (Split-Path $PSScriptRoot -Parent) 'tools\Apply-ApprovedAccountGovernance.ps1'
try { & $tool -Approved -Production:$false;throw 'Unsafe missing target accepted' }
catch {if($_.Exception.Message -notlike '*Select an explicit production or restored-test target*'){throw};'PASS: false Production switch cannot select real DB implicitly'}
try { & $tool -Approved -TestDatabase OnlineSalesDb;throw 'Unsafe test target accepted' }
catch {if($_.Exception.Message -notlike '*ValidatePattern*' -and $_.Exception.Message -notlike '*does not match*' -and $_.Exception.Message -notlike '*Cannot validate argument*'){throw};'PASS: test mode rejects real database name before connection'}
'TOTAL OFFLINE TARGET GUARDS PASS: 2'
