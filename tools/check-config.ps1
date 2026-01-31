param(
  [switch]$Quiet
)

$vars = @(
  @{ Name="POSTGRES_CONNECTION_STRING"; Alt="CONNECTIONSTRINGS__POSTGRES"; Default=$null; Required=$true; Note="API Postgres" },
  @{ Name="OPENROUTER_API_KEY"; Default=$null; Required=$true; Note="LLM key" },
  @{ Name="GOLDEN_DATASET_PATH"; Default=$null; Required=$false; Note="Accuracy tests dataset" },
  @{ Name="API_BASE_URL"; Default="http://localhost:8080"; Required=$false; Note="Baseline tests" },
  @{ Name="API_HEALTH_PATH"; Default="/health/live"; Required=$false; Note="Baseline tests" },
  @{ Name="BASELINE_TEST_EMAIL"; Default="admin@meepleai.dev"; Required=$false; Note="Baseline tests" },
  @{ Name="BASELINE_TEST_PASSWORD"; Default="Admin123!ChangeMe"; Required=$false; Note="Baseline tests" },
  @{ Name="NEXT_PUBLIC_API_BASE"; Default="http://localhost:8080"; Required=$false; Note="Web backend base" },
  @{ Name="NEXT_PUBLIC_SITE_URL"; Default="http://localhost:3000"; Required=$false; Note="Web metadata base" },
  @{ Name="NEXT_PUBLIC_ENABLE_PROGRESS_UI"; Default=$null; Required=$false; Note="Upload UI" },
  @{ Name="NEXT_PUBLIC_ENABLE_REMOTE_LOGS"; Default="true"; Required=$false; Note="Remote logs toggle" },
  @{ Name="NEXT_PUBLIC_LOG_ENDPOINT"; Default="/api/v1/logs"; Required=$false; Note="Remote log endpoint" },
  @{ Name="NEXT_PUBLIC_RETRY_ENABLED"; Default="true"; Required=$false; Note="Retry toggle" },
  @{ Name="NEXT_PUBLIC_RETRY_MAX_ATTEMPTS"; Default="3"; Required=$false; Note="Retry attempts" },
  @{ Name="NEXT_PUBLIC_RETRY_BASE_DELAY"; Default="300"; Required=$false; Note="Retry base delay ms" },
  @{ Name="NEXT_PUBLIC_RETRY_MAX_DELAY"; Default="5000"; Required=$false; Note="Retry max delay ms" },
  @{ Name="NEXT_PUBLIC_CIRCUIT_BREAKER_ENABLED"; Default="true"; Required=$false; Note="Circuit breaker toggle" },
  @{ Name="NEXT_PUBLIC_CIRCUIT_BREAKER_FAILURE_THRESHOLD"; Default="5"; Required=$false; Note="Circuit breaker failure threshold" },
  @{ Name="NEXT_PUBLIC_CIRCUIT_BREAKER_SUCCESS_THRESHOLD"; Default="2"; Required=$false; Note="Circuit breaker success threshold" },
  @{ Name="NEXT_PUBLIC_CIRCUIT_BREAKER_TIMEOUT"; Default="60000"; Required=$false; Note="Circuit breaker timeout ms" },
  @{ Name="NEXT_PUBLIC_CIRCUIT_BREAKER_WINDOW_SIZE"; Default="60000"; Required=$false; Note="Circuit breaker window ms" },
  @{ Name="NEXT_PUBLIC_HYPERDX_API_KEY"; Default="demo"; Required=$false; Note="HyperDX key" }
)

function Get-EnvValue {
  param($entry)
  $primary = [Environment]::GetEnvironmentVariable($entry.Name)
  if (-not $primary -and $entry.Alt) {
    $primary = [Environment]::GetEnvironmentVariable($entry.Alt)
  }
  return $primary
}

$rows = @()
$missingRequired = $false

foreach ($v in $vars) {
  $val = Get-EnvValue $v
  $status = ""
  if (-not $val) {
    if ($v.Required) {
      $status = "❌ Missing (required)"
      $missingRequired = $true
    }
    else {
      $status = "⚠️ Missing (uses code default)"
    }
  }
  elseif ($v.Default -and $val.Trim() -eq $v.Default) {
    $status = "ℹ️ Using default"
  }
  else {
    $status = "✅ Set"
  }

  $rows += [pscustomobject]@{
    Name   = if ($v.Alt -and $val -and -not [Environment]::GetEnvironmentVariable($v.Name)) { "$($v.Name) (via $($v.Alt))" } else { $v.Name }
    Value  = if ($Quiet) { if ($val) { "<set>" } else { "<empty>" } } else { $val }
    Status = $status
    Note   = $v.Note
  }
}

$rows | Format-Table -AutoSize

if ($missingRequired) {
  Write-Error "Config check failed: required variables missing." -ErrorAction SilentlyContinue
  exit 1
}

exit 0
