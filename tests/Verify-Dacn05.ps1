[CmdletBinding()]
param([switch]$IncludeIntegration)
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path $PSScriptRoot -Parent
$solutionPath = Join-Path $repositoryRoot 'OnlineSalesManagement.sln'
$resultDirectory = Join-Path $repositoryRoot 'artifacts\verification'
[void][IO.Directory]::CreateDirectory($resultDirectory)
$resultPath = Join-Path $resultDirectory ('verify-' + [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss-fff') + '.json')
$result = [ordered]@{ StartedUtc = [DateTime]::UtcNow.ToString('o'); Status = 'Running'; Build = 'NotRun'; Smoke = 'NotRun'; Integration = 'NotRun'; SourceStable = $false }
$stage = 'LocateMSBuild'

function Get-SourceSnapshot {
    $files = foreach ($directory in @('OnlineSalesManagement', 'tests')) {
        Get-ChildItem -LiteralPath (Join-Path $repositoryRoot $directory) -File -Recurse |
            Where-Object { $_.FullName.Replace([char]92, [char]47) -notmatch '/(bin|obj|packages|[.]vs)/' -and $_.Extension -in @('.cs','.cshtml','.csproj','.config','.ps1','.mjs') }
    }
    @($files | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{ Path = $_.FullName.Substring($repositoryRoot.Length + 1); SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash }
    })
}

Push-Location -LiteralPath $repositoryRoot
try {
    $buildCommand = Get-Command MSBuild.exe -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($buildCommand) { $msbuildPath = $buildCommand.Source } else {
        $vswherePath = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Microsoft Visual Studio\Installer\vswhere.exe'
        if (!(Test-Path -LiteralPath $vswherePath)) { throw 'MSBuild and vswhere are unavailable' }
        $candidates = @(& $vswherePath -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe')
        if ($LASTEXITCODE -ne 0 -or $candidates.Count -eq 0) { throw 'MSBuild discovery failed' }
        $msbuildPath = $candidates[0]
    }
    $before = Get-SourceSnapshot
    $result['SourceSnapshot'] = $before
    $stage = 'Build'
    & $msbuildPath $solutionPath /t:Rebuild /p:Configuration=Debug /p:MvcBuildViews=true /verbosity:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; tests will not run against an old DLL' }
    $result.Build = 'Passed'
    $stage = 'Smoke'
    & (Join-Path $PSScriptRoot 'Run-SmokeTests.ps1')
    $result.Smoke = 'Passed'
    if ($IncludeIntegration) {
        $stage = 'Integration'
        & (Join-Path $PSScriptRoot 'Run-IntegrationTests.ps1')
        $result.Integration = 'Passed'
    }
    $stage = 'SourceStability'
    $after = Get-SourceSnapshot
    $difference = Compare-Object -ReferenceObject $before -DifferenceObject $after -Property Path,SHA256
    if ($difference) { throw 'Source changed during verification; results do not certify the current working tree' }
    $result.SourceStable = $true
    $result.Status = 'Passed'
} catch {
    $result.Status = 'Failed'
    $result['FailureStage'] = $stage
    if ($stage -in @('Build','Smoke','Integration')) { $result[$stage] = 'Failed' }
    throw
} finally {
    $result['FinishedUtc'] = [DateTime]::UtcNow.ToString('o')
    $result | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $resultPath -Encoding UTF8
    Pop-Location
    Write-Output ("Verification record: " + $resultPath)
}
