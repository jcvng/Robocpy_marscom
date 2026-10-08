$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '../src/MachineBackup/MachineBackup.csproj'
dotnet build $project --no-restore
if ($LASTEXITCODE -ne 0) { throw 'Build failed' }
$app = Join-Path $PSScriptRoot '../src/MachineBackup/bin/Debug/net8.0/MachineBackup.dll'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('MachineBackup-test-' + [guid]::NewGuid())
# Keep fixtures for inspection; all copying/deletion by the app stays inside this unique directory.
$source = Join-Path $testRoot 'Source'
$source2 = Join-Path $testRoot 'Other'
$destination = Join-Path $testRoot 'Backup'
New-Item -ItemType Directory -Path $source, $source2, (Join-Path $destination 'Source') -Force | Out-Null
Set-Content (Join-Path $source 'new.txt') 'new content'
Set-Content (Join-Path $source2 'second.txt') 'second source'
New-Item -ItemType Directory -Path (Join-Path $source 'Empty folder') | Out-Null
$extra = Join-Path $destination 'Source/extra.txt'
Set-Content $extra 'keep in additive mode'
$settings = Join-Path $testRoot 'settings.json'
@{ Machines = @{ TestPc = @{ SourcePaths = @($source, $source2); DestinationPath = $destination } } } | ConvertTo-Json -Depth 5 | Set-Content $settings
function Invoke-Backup([string[]] $Options, [int] $ExpectedExit = 0) {
    $output = dotnet $app --settings $settings @Options
    if ($LASTEXITCODE -ne $ExpectedExit) { throw "Expected exit $ExpectedExit, got $LASTEXITCODE" }
    $script:lastOutput = $output -join "`n"
    $output | Write-Host
}
function Assert-Output([string] $Text) {
    if (!$script:lastOutput.Contains($Text)) { throw "Output missing '$Text': $script:lastOutput" }
}
Invoke-Backup -Options @('--list-machines')
Invoke-Backup -Options @('--machine', 'missing') -ExpectedExit 1
Invoke-Backup -Options @('testpc', '--dry-run')
Assert-Output 'Machine: TestPc'
Assert-Output '| Would copy'
Invoke-Backup -Options @('testpc', '--machine', 'TestPc') -ExpectedExit 1
Invoke-Backup -Options @('--machine', 'TestPc', 'testpc') -ExpectedExit 1
Invoke-Backup -Options @('--unknown') -ExpectedExit 1
Invoke-Backup -Options @('--machine', 'testpc', '--dry-run')
Assert-Output '| Would copy'
Assert-Output '| Would create'
Assert-Output '| Preserved'
Assert-Output 'Destination path'
if ($script:lastOutput.Contains('ROBOCOPY')) { throw 'Raw Robocopy output leaked into the table' }
if (Test-Path (Join-Path $destination 'Source/new.txt')) { throw 'Preview copied a file' }
Invoke-Backup -Options @('--machine', 'TestPc')
Assert-Output '| Copied'
Assert-Output '| Created'
Assert-Output '| Preserved'
if (!(Test-Path $extra)) { throw 'Additive backup removed an extra file' }
if ((Get-Content (Join-Path $destination 'Source/new.txt')) -ne 'new content') { throw 'Copied content differs' }
if (!(Test-Path (Join-Path $destination 'Other/second.txt'))) { throw 'Second source was not copied' }
Set-Content (Join-Path $source 'new.txt') 'updated content with a different size'
Invoke-Backup -Options @('--machine', 'TestPc')
Assert-Output '| Updated'
if ((Get-Content (Join-Path $destination 'Source/new.txt')) -ne 'updated content with a different size') { throw 'Updated content was not copied' }
Invoke-Backup -Options @('--machine', 'TestPc', '--mirror', '--dry-run')
Assert-Output '| Would delete'
if (!(Test-Path $extra)) { throw 'Mirror preview deleted an extra file' }
Invoke-Backup -Options @('--machine', 'TestPc', '--mirror')
Assert-Output '| Deleted'
if (Test-Path $extra) { throw 'Mirror did not remove an extra file' }
@{ Machines = @{ TestPc = @{ SourcePaths = @($source, (Join-Path $testRoot 'Missing')); DestinationPath = (Join-Path $testRoot 'Untouched') } } } | ConvertTo-Json -Depth 5 | Set-Content $settings
Invoke-Backup -Options @('--machine', 'TestPc') -ExpectedExit 1
if (Test-Path (Join-Path $testRoot 'Untouched')) { throw 'Copied before validating all sources' }
@{ Machines = @{ TestPc = @{ SourcePaths = @($source); DestinationPath = (Join-Path $source 'Nested') } } } | ConvertTo-Json -Depth 5 | Set-Content $settings
Invoke-Backup -Options @('--machine', 'TestPc') -ExpectedExit 1
Write-Host "Smoke tests passed. Fixtures: $testRoot"
