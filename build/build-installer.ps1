<#
.SYNOPSIS
  Gera o instalador do Despertador Produtivo do zero.

.DESCRIPTION
  Faz o publish framework-dependent (um único .exe enxuto, ~5 MB, que usa o
  .NET 8 Desktop Runtime já instalado) e o compila num setup.exe com o Inno
  Setup. Rode da raiz do repositório ou de qualquer lugar — os caminhos são
  resolvidos a partir da localização deste script.

  Requer o Inno Setup 6 (winget install JRSoftware.InnoSetup). O script procura
  o ISCC.exe nos locais de instalação por usuário e por máquina.

.EXAMPLE
  pwsh build\build-installer.ps1
#>
[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [string]$Runtime = 'win-x64'
)

$ErrorActionPreference = 'Stop'

$buildDir = $PSScriptRoot
$repoRoot = Split-Path $buildDir -Parent
$projeto = Join-Path $repoRoot 'src\AlarmClock.App\AlarmClock.App.csproj'
$publishDir = Join-Path $buildDir 'publish'
$iss = Join-Path $buildDir 'installer.iss'

Write-Host "==> Limpando publish anterior" -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }

Write-Host "==> Publicando framework-dependent ($Runtime, single-file)" -ForegroundColor Cyan
& dotnet publish $projeto `
    -c $Configuration `
    -r $Runtime `
    --self-contained false `
    -p:PublishSingleFile=true `
    -p:DebugType=none `
    -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish falhou ($LASTEXITCODE)." }

Write-Host "==> Localizando o Inno Setup (ISCC.exe)" -ForegroundColor Cyan
$candidatos = @(
    (Join-Path $env:LOCALAPPDATA 'Programs\Inno Setup 6\ISCC.exe'),
    (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
    (Join-Path $env:ProgramFiles 'Inno Setup 6\ISCC.exe')
)
$iscc = $candidatos | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) {
    throw "ISCC.exe não encontrado. Instale com: winget install JRSoftware.InnoSetup"
}

Write-Host "==> Compilando o instalador" -ForegroundColor Cyan
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "ISCC falhou ($LASTEXITCODE)." }

$saida = Get-ChildItem (Join-Path $buildDir 'dist') -Filter '*.exe' |
    Sort-Object LastWriteTime -Descending | Select-Object -First 1
Write-Host ""
Write-Host ("Pronto: {0} ({1:N1} MB)" -f $saida.FullName, ($saida.Length / 1MB)) -ForegroundColor Green
