# Local security checks (alternative to GitHub Advanced Security for private repos)
# Run: pwsh scripts/security-check-local.ps1

Write-Host "🔐 Local Security Checks" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host ""

# 1. Frontend dependency vulnerabilities
Write-Host "📦 1. Checking Frontend Dependencies..." -ForegroundColor Yellow
Set-Location apps/web
# Stessa asserzione del gate CI (#3990): si contano gli `advisories` che il report
# contiene. Non `metadata.vulnerabilities`, che e' il sommario restituito dal registry e
# ripassato da pnpm senza ricalcolo; e non un -match sull'intero output, dove il nome di
# un pacchetto o il titolo di un advisory bastavano a decidere l'esito.
#
# Qui c'era anche una `-notmatch "GHSA-..."` che escludeva un advisory pdfjs-dist
# rimandando a un TODO #4242 — un'issue che non esiste. Due problemi in uno: la clausola
# si applicava all'INTERO output, quindi al riapparire di quell'advisory qualunque altra
# CRITICAL sarebbe passata; e l'eccezione non era tracciata da nulla. Rimossa: se un
# rischio va accettato, si accetta per advisory e per pacchetto, con un riferimento vero.
#
# `pnpm audit` esce non-zero anche per sole moderate, quindi l'exit code non e' il
# segnale: lo e' il JSON.
$frontendJson = pnpm audit --prod --json 2>&1 | Out-String
try {
    $report = $frontendJson | ConvertFrom-Json -ErrorAction Stop
} catch {
    $report = $null
}
if ($null -eq $report -or $null -eq $report.advisories) {
    Write-Host "❌ pnpm audit did not produce a readable report - scan unreliable" -ForegroundColor Red
    Write-Host $frontendJson.Substring(0, [Math]::Min(500, $frontendJson.Length)) -ForegroundColor Gray
} else {
    $blocking = @($report.advisories.PSObject.Properties.Value |
        Where-Object { $_.severity -in @('high', 'critical') })
    if ($blocking.Count -gt 0) {
        Write-Host "❌ $($blocking.Count) high/critical advisories in production dependencies" -ForegroundColor Red
        foreach ($a in $blocking) {
            Write-Host "   $($a.severity.ToUpper()) $($a.module_name) $($a.vulnerable_versions) - $($a.github_advisory_id)" -ForegroundColor Red
        }
    } else {
        Write-Host "✅ No high/critical advisories in production dependencies" -ForegroundColor Green
    }
}
Set-Location ..\..
Write-Host ""

# 2. Backend dependency vulnerabilities
Write-Host "📦 2. Checking Backend Dependencies..." -ForegroundColor Yellow
Set-Location apps/api
dotnet list package --vulnerable --include-transitive > vuln-report.txt 2>&1
# Come il braccio .NET del gate CI (#3707): l'asserzione e' sulla colonna Severity delle
# righe "> pacchetto" (penultimo campo, prima dell'URL dell'advisory), non su un -match
# dell'intero report — dove un nome di pacchetto o un URL potevano far scattare il gate.
$backendBlocking = @(Get-Content vuln-report.txt | Where-Object {
    $fields = -split $_
    $fields.Count -ge 3 -and $fields[0] -eq '>' -and $fields[$fields.Count - 2] -in @('High', 'Critical')
})
if ($backendBlocking.Count -gt 0) {
    Write-Host "❌ $($backendBlocking.Count) .NET packages with High/Critical vulnerabilities" -ForegroundColor Red
    $backendBlocking | ForEach-Object { Write-Host "  $_" -ForegroundColor Red }
} else {
    Write-Host "✅ No HIGH/CRITICAL vulnerabilities in .NET packages" -ForegroundColor Green
}
Remove-Item vuln-report.txt -ErrorAction SilentlyContinue
Set-Location ..\..
Write-Host ""

# 3. Secrets detection with Semgrep (Docker-based)
Write-Host "🔍 3. Checking for Secrets (Semgrep)..." -ForegroundColor Yellow
if (Get-Command docker -ErrorAction SilentlyContinue) {
    docker run --rm -v "${PWD}:/src" semgrep/semgrep:latest `
        semgrep scan `
        --config=p/secrets `
        --config=p/security-audit `
        --quiet `
        /src
    if ($LASTEXITCODE -eq 0) {
        Write-Host "✅ Semgrep scan completed - no issues found" -ForegroundColor Green
    } else {
        Write-Host "⚠️ Semgrep found potential issues (review output above)" -ForegroundColor Yellow
    }
} else {
    Write-Host "⚠️ Docker not available - skipping Semgrep scan" -ForegroundColor Yellow
    Write-Host "   Install Docker Desktop for Windows" -ForegroundColor Gray
}
Write-Host ""

# 4. Python dependencies (if available)
Write-Host "🐍 4. Checking Python Dependencies..." -ForegroundColor Yellow
$pythonServices = @("orchestration-service", "embedding-service", "reranker-service")
foreach ($service in $pythonServices) {
    if (Test-Path "apps/$service/requirements.txt") {
        Write-Host "   Checking $service..." -ForegroundColor Gray
        Set-Location "apps/$service"
        if (Get-Command safety -ErrorAction SilentlyContinue) {
            safety check -r requirements.txt --bare
            if ($LASTEXITCODE -eq 0) {
                Write-Host "   ✅ No vulnerabilities in $service" -ForegroundColor Green
            } else {
                Write-Host "   ⚠️ Vulnerabilities found in $service" -ForegroundColor Yellow
            }
        } else {
            Write-Host "   ℹ️ Install 'safety' for Python vulnerability scanning: pip install safety" -ForegroundColor Gray
        }
        Set-Location ..\..
    }
}
Write-Host ""

# 5. Summary
Write-Host "📊 Security Check Summary" -ForegroundColor Cyan
Write-Host "========================" -ForegroundColor Cyan
Write-Host "✅ Dependency audit: Complete" -ForegroundColor Green
Write-Host "✅ Secrets scan: Complete (if Docker available)" -ForegroundColor Green
Write-Host "ℹ️ For full code scanning, enable GitHub Advanced Security (paid for private repos)" -ForegroundColor Gray
Write-Host ""
Write-Host "💡 Tips:" -ForegroundColor Yellow
Write-Host "  - Run before commits: pwsh scripts/security-check-local.ps1"
Write-Host "  - Install safety: pip install safety (for Python checks)"
Write-Host "  - View Dependabot alerts: gh api repos/meepleAi-app/meepleai-monorepo/dependabot/alerts"
