if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script requires administrative privileges. Please run PowerShell as Administrator."
    exit
}

$ProjectDir = "C:\Users\Me\projects\Extension-ForCS-Succession"
$LandisExtensionsDir = "C:\Program Files\LANDIS-II-v8\extensions"

$LandisExecutionDir = "C:\landis\HETRE_ERABLE"
$LandisExecutionDir = "C:\landis\HETRE_ERABLE (5)"
#$LandisExecutionDir = "$ProjectDir\testing\v8 Scenario"

Write-Host "Configuration:"
Write-Host "  Project Directory: $ProjectDir"
Write-Host "  LANDIS-II Extensions: $LandisExtensionsDir"
Write-Host "  LANDIS-II Execution Directory: $LandisExecutionDir"
Write-Host "  To change the LANDIS-II execution directory, modify the \$LandisExecutionDir variable above"
Write-Host ""

cd $ProjectDir\src\
dotnet build -c release
if ($LASTEXITCODE -ne 0) {
    Write-Host "dotnet build failed. Exiting script."
    exit $LASTEXITCODE
}

Copy-Item -Path "$ProjectDir\src\bin\release\netstandard2.0\Landis.Extension.Succession.ForC-v4.dll" -Destination "$LandisExtensionsDir\" -Force

cd $LandisExecutionDir
try {
    Write-Output "Executing LANDIS-II..."
    landis-ii-8.cmd .\scenario.txt 2>&1 | Tee-Object -FilePath console-output.txt
    Write-Output "LANDIS-II execution completed."
    cd $ProjectDir\src\
} finally {
    cd $ProjectDir\src\
}

Write-Output "Creating filtered console output..."
$consoleOutputPath = "$LandisExecutionDir\console-output.txt"
$filteredOutputPath = "$LandisExecutionDir\console-output-filtered.txt"

if (Test-Path $consoleOutputPath) {
    Get-Content $consoleOutputPath | Where-Object { 
        $_ -match "^Site: \(" -or $_ -match "^Current time: " -or $_ -match "^Adding new cohort:" -or $_ -match "^Transferring from"
    } | ForEach-Object {
        if ($_ -match "^Adding new cohort:" -or $_ -match "^Transferring from") {
            "    $_"
        } else {
            $_
        }
    } | Out-File -FilePath $filteredOutputPath -Encoding UTF8
    Write-Output "Filtered console output saved to: $filteredOutputPath"
} else {
    Write-Output "console-output.txt not found. Skipping filtering."
}
