param(
  [Parameter(Mandatory=$true)][int]$Issue,
  [string]$BaseBranch = "main",
  [string]$Reviewers = "",          # es: "org/team-backend,user1"
  [string]$MergeMethod = "squash",  # merge|squash|rebase
  [switch]$Draft                    # crea PR in bozza
)

# ===== Config monorepo: adatta questi comandi =====
$env:TEST_CMD       = $env:TEST_CMD       ? $env:TEST_CMD       : "pnpm -r test"
$env:LINT_CMD       = $env:LINT_CMD       ? $env:LINT_CMD       : "pnpm -r lint"
$env:TYPECHECK_CMD  = $env:TYPECHECK_CMD  ? $env:TYPECHECK_CMD  : "pnpm -r typecheck"
$env:E2E_CMD        = $env:E2E_CMD        ? $env:E2E_CMD        : ""      # opzionale
# ==================================================

function ExecOrFail($cmd) {
  Write-Host "→ $cmd" -ForegroundColor Cyan
  cmd /c $cmd
  if ($LASTEXITCODE -ne 0) {
    Write-Host "✖ Command failed: $cmd" -ForegroundColor Red
    exit 1
  }
}

# 1) Recupera titolo issue per branch leggibile
$t = (gh issue view $Issue --json title --jq ".title") 2>$null
if (-not $t) { Write-Host "Impossibile leggere l'issue #$Issue. Sei nel repo giusto?" -ForegroundColor Red; exit 1 }
$slug = ($t -replace "[^a-zA-Z0-9\- ]","" -replace "\s+","-").ToLower()
$branch = "feature/$Issue-$slug"

# 2) Crea branch
ExecOrFail "git fetch origin $BaseBranch"
ExecOrFail "git checkout -B $branch origin/$BaseBranch"

# 3) (Facoltativo) apri editor per commit iniziale
# ExecOrFail "git commit --allow-empty -m ""chore($Issue): start work"""

# 4) Test locali (falliscono → stop)
if ($env:LINT_CMD  -ne "") { ExecOrFail $env:LINT_CMD }
if ($env:TYPECHECK_CMD -ne "") { ExecOrFail $env:TYPECHECK_CMD }
if ($env:TEST_CMD  -ne "") { ExecOrFail $env:TEST_CMD }
if ($env:E2E_CMD   -ne "") { ExecOrFail $env:E2E_CMD }

# 5) Push branch
ExecOrFail "git push -u origin $branch"

# 6) Crea PR collegata all'issue
$draftFlag = $Draft.IsPresent ? "--draft" : ""
$title = "[#$Issue] $t"
$body  = "Closes #$Issue`n`n<!-- Checklist -->`n- [x] Lint`n- [x] Typecheck`n- [x] Test locali"
$reviewerFlag = ($Reviewers -ne "") ? "--reviewer $Reviewers" : ""
ExecOrFail "gh pr create --base $BaseBranch --head $branch --title `"$title`" --body `"$body`" $draftFlag $reviewerFlag"

# 7) Se era draft, marcale come pronta quando vuoi:
# ExecOrFail "gh pr ready --yes"

# 8) Abilita auto-merge → GitHub fonderà quando: 1 approvazione + checks verdi + branch protection ok
ExecOrFail "gh pr merge --auto --$MergeMethod --delete-branch=false"

Write-Host "✅ PR creata e auto-merge abilitato (metodo: $MergeMethod). Attende review + CI." -ForegroundColor Green
