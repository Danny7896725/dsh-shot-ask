param(
    [string]$Root = $PSScriptRoot
)

$ErrorActionPreference = 'Stop'
$src = Join-Path $Root 'src'
$dist = Join-Path $Root 'dist'
New-Item -ItemType Directory -Force -Path $dist | Out-Null

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 C# 编译器: $csc" }

$files = Get-ChildItem (Join-Path $src '*.cs') | ForEach-Object { $_.FullName }
$out = Join-Path $dist 'ShotAsk.exe'
$icon = Join-Path $Root 'assets\app.ico'

$compilerArgs = @(
    '/nologo',
    '/target:winexe',
    '/platform:anycpu',
    '/optimize+',
    '/langversion:5',
    '/codepage:65001',
    ("/out:" + $out)
)
if (Test-Path $icon) { $compilerArgs += ("/win32icon:" + $icon) }
$compilerArgs += $files
$compilerArgs += @('/r:System.dll', '/r:System.Core.dll', '/r:System.Drawing.dll', '/r:System.Windows.Forms.dll', '/r:System.Web.Extensions.dll')

& $csc @compilerArgs
if ($LASTEXITCODE -ne 0) { throw "编译失败，退出码 $LASTEXITCODE" }

Write-Host "已生成 $out"
$chinese = Join-Path $dist ('截图提问助手.exe')
Copy-Item $out $chinese -Force
Write-Host ("已更新 " + $chinese)
Get-Item $out | Select-Object FullName, Length, LastWriteTime | Format-List
