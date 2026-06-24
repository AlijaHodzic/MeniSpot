$ErrorActionPreference = "Stop"

$root = $PSScriptRoot
$devDir = Join-Path $root ".dev"
$logDir = Join-Path $devDir "logs"
$pidFile = Join-Path $devDir "processes.json"

New-Item -ItemType Directory -Force -Path $logDir | Out-Null

docker compose --project-directory $root up -d

$backendOut = Join-Path $logDir "backend.out.log"
$backendErr = Join-Path $logDir "backend.err.log"
$frontendOut = Join-Path $logDir "frontend.out.log"
$frontendErr = Join-Path $logDir "frontend.err.log"

$backend = Start-Process dotnet -ArgumentList @("run", "--project", "backend/DigitalMenu.Api", "--launch-profile", "http") -WorkingDirectory $root -WindowStyle Hidden -RedirectStandardOutput $backendOut -RedirectStandardError $backendErr -PassThru
$frontend = Start-Process "cmd.exe" -ArgumentList @("/c", "npm start -- --host 127.0.0.1") -WorkingDirectory (Join-Path $root "frontend") -WindowStyle Hidden -RedirectStandardOutput $frontendOut -RedirectStandardError $frontendErr -PassThru

@{
  backend = $backend.Id
  frontend = $frontend.Id
  startedAt = (Get-Date).ToString("s")
} | ConvertTo-Json | Set-Content -Path $pidFile -Encoding UTF8

Write-Host "MeniSpot dev environment is starting."
Write-Host "Frontend: http://localhost:4200"
Write-Host "Backend:  http://localhost:5158"
Write-Host "Logs:     $logDir"
Write-Host "Stop:     .\stop-dev.ps1"
