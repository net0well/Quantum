<#
    Gera o Quantum.exe portátil: um único arquivo, sem instalação e sem
    exigir .NET na máquina de destino. Basta copiar o .exe.
#>
[CmdletBinding()]
param(
    [string]$Output = (Join-Path $PSScriptRoot 'publish'),
    [ValidateSet('Release', 'Debug')]
    [string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot 'src\Quantum.App\Quantum.App.csproj'

Write-Host "Publicando o Quantum ($Configuration) em $Output..." -ForegroundColor Cyan

dotnet publish $project `
    --configuration $Configuration `
    --output $Output `
    --runtime win-x64 `
    --self-contained `
    -p:PublishSingleFile=true `
    --nologo

if ($LASTEXITCODE -ne 0) {
    throw "A publicação falhou com código $LASTEXITCODE."
}

$exe = Join-Path $Output 'Quantum.exe'
if (-not (Test-Path $exe)) {
    throw "O executável não foi gerado em $exe."
}

$sizeMb = [Math]::Round((Get-Item $exe).Length / 1MB, 1)
Write-Host ""
Write-Host "Pronto: $exe ($sizeMb MB)" -ForegroundColor Green
Write-Host "Copie esse arquivo para qualquer Windows 10/11 de 64 bits e execute." -ForegroundColor Green
