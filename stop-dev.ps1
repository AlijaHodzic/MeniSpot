$ErrorActionPreference = "SilentlyContinue"

$root = $PSScriptRoot
$pidFile = Join-Path $root ".dev\processes.json"

if (Test-Path $pidFile) {
  $processes = Get-Content $pidFile | ConvertFrom-Json
  foreach ($id in @($processes.backend, $processes.frontend)) {
    if ($id) {
      Stop-Process -Id $id -Force
    }
  }
  Remove-Item $pidFile -Force
}

Get-NetTCPConnection -LocalPort 4200,5158 -State Listen | ForEach-Object {
  Stop-Process -Id $_.OwningProcess -Force
}

Write-Host "MeniSpot backend and frontend dev processes are stopped."
Write-Host "PostgreSQL Docker container is still running. Use 'docker compose down' only when you want to stop the database."
