param(
  [string]$BaseUrl = "http://localhost:8080",
  [string]$Username = "admin",
  [string]$Password = "admin123",
  [string]$Tenant = ""
)

$ErrorActionPreference = "Stop"
$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession

Write-Host "E2E Smoke - HN Nexus POS"
Write-Host "BaseUrl: $BaseUrl"

function Get-CsrfToken([string]$html) {
  $m = [regex]::Match($html, 'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"')
  if (-not $m.Success) { return $null }
  return $m.Groups[1].Value
}

$loginPage = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -WebSession $session
$csrf = Get-CsrfToken $loginPage.Content
if (-not $csrf) { throw "No se encontró token antiforgery en Login." }

$loginBody = @{
  "__RequestVerificationToken" = $csrf
  "Input.Username" = $Username
  "Input.Password" = $Password
  "Input.TenantCode" = $Tenant
}

$null = Invoke-WebRequest -Uri "$BaseUrl/Account/Login" -Method Post -Body $loginBody -WebSession $session -MaximumRedirection 5

$pages = @(
  "/",
  "/Sales/New",
  "/Sales",
  "/Config/Monitoring",
  "/Config/Health",
  "/Reports/Enterprise"
)

$failed = @()
foreach ($p in $pages) {
  try {
    $res = Invoke-WebRequest -Uri "$BaseUrl$p" -WebSession $session
    if ($res.StatusCode -lt 200 -or $res.StatusCode -ge 400) {
      $failed += "$p => $($res.StatusCode)"
    } else {
      Write-Host "[OK] $p"
    }
  } catch {
    $failed += "$p => ERROR $($_.Exception.Message)"
  }
}

if ($failed.Count -gt 0) {
  Write-Host "Fallas:"
  $failed | ForEach-Object { Write-Host " - $_" }
  exit 1
}

Write-Host "E2E smoke completado sin errores."
