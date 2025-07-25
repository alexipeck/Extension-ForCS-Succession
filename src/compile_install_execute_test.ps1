if (-NOT ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole] "Administrator")) {
    Write-Host "This script requires administrative privileges. Please run PowerShell as Administrator."
    exit
}

$ProjectDir = "C:\Users\Me\projects\Extension-ForCS-Succession"
$LandisExtensionsDir = "C:\Program Files\LANDIS-II-v8\extensions"

cd $ProjectDir\src\
dotnet build -c release
if ($LASTEXITCODE -ne 0) {
    Write-Host "dotnet build failed. Exiting script."
    exit $LASTEXITCODE
}

Copy-Item -Path "$ProjectDir\src\bin\release\netstandard2.0\Landis.Extension.Succession.ForC-v4.dll" -Destination "$LandisExtensionsDir\" -Force

cd "C:\landis\HETRE_ERABLE"
try {
    Write-Output "Executing LANDIS-II..."
    landis-ii-8.cmd .\scenario.txt 2>&1 | Tee-Object -FilePath console-output.txt
    Write-Output "LANDIS-II execution completed."
    cd $ProjectDir\src\
} finally {
    cd $ProjectDir\src\
}
