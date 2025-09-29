# create-issues.ps1
# UTF-8 (no BOM) - SOLO ASCII. Niente caratteri “smart”.
# Funzioni:
# - Crea le label mancanti (palette default).
# - Crea le milestone mancanti.
# - Crea/aggiorna issue da CSV (ID, Title, Description, Component, Type, Priority, Effort, Labels, Milestone, Dependencies, AcceptanceCriteria).
# Opzioni:
# -Update     : aggiorna body/title/labels/milestone se l'issue esiste.
# -LinkDeps   : aggiunge commento "Depends on #XYZ" in base al campo Dependencies (ID separati da virgola/;).
# -DryRun     : non chiama GitHub, stampa solo le azioni.

[CmdletBinding()]
Param(
  [string]$Repo,                            # owner/repo; se vuoto deduce da 'gh repo view'
  [string]$CsvPath = ".\meepleai_backlog\meepleai_backlog.csv",
  [switch]$Update,
  [switch]$LinkDeps,
  [switch]$DryRun
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Resolve-Repo {
  param([string]$RepoIn)
  if ($RepoIn) { return $RepoIn }
  $raw = gh repo view --json nameWithOwner 2>$null
  if (-not $raw) { throw "Impossibile determinare il repo. Passa -Repo owner/repo oppure esegui 'gh auth login' nella cartella del repo." }
  $obj = $raw | ConvertFrom-Json
  if (-not $obj.nameWithOwner) { throw "gh repo view non ha restituito nameWithOwner." }
  return $obj.nameWithOwner
}

function Ensure-File([string]$Path) {
  if (-not (Test-Path -LiteralPath $Path)) {
    throw "File non trovato: $Path"
  }
}

function Get-AllPages {
  param([string]$UrlBase)
  $page = 1
  $acc = @()
  while ($true) {
    $url = "$UrlBase" + (if ($UrlBase -like "*?*") { "&" } else { "?" }) + "per_page=100&page=$page"
    $raw = gh api $url 2>$null
    if (-not $raw) { break }
    $arr = $raw | ConvertFrom-Json
    if (-not $arr -or $arr.Count -eq 0) { break }
    $acc += $arr
    $page++
  }
  return $acc
}

function Get-Labels { param([string]$Repo) return Get-AllPages -UrlBase ("repos/{0}/labels" -f $Repo) }

function Create-Label {
  param([string]$Repo,[string]$Name,[string]$Color,[string]$Description)
  if ($DryRun) { Write-Host "[DRY-RUN] create label: $Name ($Color)"; return }
  try {
    gh api "repos/$Repo/labels" -f "name=$Name" -f "color=$Color" -f "description=$Description" | Out-Null
  } catch {
    if ($_.Exception.Message -notmatch "422") { throw }
    Write-Host "Label gia' esistente o conflitto benigno: $Name"
  }
}

function Ensure-Labels {
  param([string]$Repo,[string[]]$Wanted)

  $palette = @{
    "area/infra"       = @{ color="1f6feb"; desc="Infrastruttura e DevEx"; }
    "area/security"    = @{ color="d73a4a"; desc="Sicurezza e policy"; }
    "area/auth"        = @{ color="0e8a16"; desc="Auth, ruoli, tenancy"; }
    "area/db"          = @{ color="5319e7"; desc="Database e migrazioni"; }
    "area/rulespec"    = @{ color="fbca04"; desc="RuleSpec e parsing"; }
    "area/pdf"         = @{ color="bfd4f2"; desc="Pipeline PDF/OCR"; }
    "area/ui"          = @{ color="c5def5"; desc="UI/UX e editor"; }
    "area/ai"          = @{ color="a2eeef"; desc="AI, RAG, embeddings"; }
    "area/automations" = @{ color="e4e669"; desc="n8n e automazioni"; }
    "area/perf"        = @{ color="a295d6"; desc="Performance e caching"; }
    "area/admin"       = @{ color="ffea7f"; desc="Pagine amministrative"; }
    "area/ops"         = @{ color="b60205"; desc="Logging/observability"; }
    "area/docs"        = @{ color="0366d6"; desc="Documentazione"; }

    "kind/task"        = @{ color="ededed"; desc="Task tecnico"; }
    "kind/feature"     = @{ color="0e8a16"; desc="Nuova funzionalita'"; }
    "kind/schema"      = @{ color="5319e7"; desc="Schema/DB"; }
    "kind/spec"        = @{ color="fbca04"; desc="Specifica"; }
    "kind/ci"          = @{ color="1d76db"; desc="CI/CD"; }
    "kind/security"    = @{ color="d73a4a"; desc="Security work"; }
    "kind/test"        = @{ color="5319e7"; desc="Test automatizzati"; }
    "kind/infra"       = @{ color="1f6feb"; desc="Infra"; }
    "kind/perf"        = @{ color="a295d6"; desc="Performance"; }

    "ai/nlp"           = @{ color="a2eeef"; desc="NLP/OCR/Parsing"; }
  }

  $current = Get-Labels -Repo $Repo
  $have = @{}
  foreach ($l in $current) { $have[$l.name] = $true }

  $unique = ($Wanted | Where-Object { $_ -and $_.Trim() -ne "" } |
             ForEach-Object { $_.Trim() } | Select-Object -Unique)

  foreach ($w in $unique) {
    if ($have.ContainsKey($w)) { continue }
    $meta  = $palette[$w]
    $color = if ($meta) { $meta.color } else { "777777" }
    $desc  = if ($meta) { $meta.desc  } else { "Auto-created" }
    Write-Host "Creazione label: $w ($color)"
    Create-Label -Repo $Repo -Name $w -Color $color -Description $desc
  }
}

function Get-Milestones { param([string]$Repo) return Get-AllPages -UrlBase ("repos/{0}/milestones" -f $Repo) }

function Create-Milestone {
  param([string]$Repo,[string]$Title)
  if ($DryRun) { Write-Host "[DRY-RUN] create milestone: $Title"; return @{ title=$Title } }
  $raw = gh api "repos/$Repo/milestones" -f "title=$Title"
  return ($raw | ConvertFrom-Json)
}

function Ensure-Milestone {
  param([string]$Repo,[string]$Title)
  if ([string]::IsNullOrWhiteSpace($Title)) { return $null }
  $all = Get-Milestones -Repo $Repo
  $found = $all | Where-Object { $_.title -eq $Title } | Select-Object -First 1
  if ($found) { return $found.title }
  Write-Host "Creazione milestone: $Title"
  $null = Create-Milestone -Repo $Repo -Title $Title
  return $Title
}

function Find-IssueByIdInTitle {
  param([string]$Repo,[string]$Id)
  $raw = gh issue list --repo $Repo --search "$Id in:title" --limit 1 --json number
  if (-not $raw) { return $null }
  $arr = $raw | ConvertFrom-Json
  if ($arr -and $arr.Count -gt 0) { return [int]$arr[0].number }
  return $null
}

function New-IssueBody {
  param(
    [string]$Component,[string]$Type,[string]$Priority,[string]$Effort,
    [string]$Dependencies,[string]$Description,[string]$AcceptanceCriteria
  )
@"
**Component**: $Component
**Type**: $Type
**Priority**: $Priority
**Effort**: $Effort
**Dependencies**: $Dependencies

### Description
$Description

### Acceptance Criteria
$AcceptanceCriteria
"@
}

function Create-Issue {
  param(
    [string]$Repo,[string]$Title,[string]$Body,[string[]]$Labels,[string]$MilestoneName
  )
  $args = @("issue","create","--repo",$Repo,"--title",$Title,"--body",$Body)
  foreach ($l in ($Labels | Where-Object { $_ })) { $args += @("--label",$l) }
  if ($MilestoneName) { $args += @("--milestone",$MilestoneName) }
  if ($DryRun) { Write-Host "[DRY-RUN] gh $($args -join ' ')"; return $null }
  $out = gh @args
  if ($out -match "/issues/(\d+)") { return [int]$Matches[1] }
  return $null
}

function Update-Issue {
  param(
    [string]$Repo,[int]$Number,[string]$Title,[string]$Body,[string[]]$Labels,[string]$MilestoneName
  )
  if ($DryRun) {
    Write-Host "[DRY-RUN] PATCH issue #$Number (title/body/labels/milestone)"
    return
  }
  $payload = @{ title=$Title; body=$Body; labels=$Labels }
  if ($MilestoneName) {
    $ms = Get-Milestones -Repo $Repo | Where-Object { $_.title -eq $MilestoneName } | Select-Object -First 1
    if ($ms) { $payload.milestone = $ms.number }
  }
  $json = ($payload | ConvertTo-Json -Depth 5)
  $tmp = New-TemporaryFile
  Set-Content -LiteralPath $tmp -Value $json -Encoding UTF8
  gh api "repos/$Repo/issues/$Number" --method PATCH --input $tmp | Out-Null
  Remove-Item $tmp -ErrorAction SilentlyContinue
}

function Add-DependsComment {
  param([string]$Repo,[int]$Number,[int[]]$DependsOn)
  if (-not $DependsOn -or $DependsOn.Count -eq 0) { return }
  $refs = ($DependsOn | ForEach-Object { "#$_" }) -join ", "
  $text = "Depends on: $refs"
  if ($DryRun) { Write-Host "[DRY-RUN] comment on #$Number -> $text"; return }
  gh issue comment $Number --repo $Repo --body $text | Out-Null
}

# MAIN
$Repo = Resolve-Repo -RepoIn $Repo
Ensure-File -Path $CsvPath

$rows = Import-Csv -Path $CsvPath

# 1) Prepara labels da CSV e creale se mancano
$allLabels = @()
foreach ($r in $rows) {
  $labelsRaw = "$($r.Labels)".Trim()
  if ($labelsRaw) {
    $allLabels += ($labelsRaw -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" })
  }
}
Ensure-Labels -Repo $Repo -Wanted $allLabels

# 2) Creazione/aggiornamento issues
$issueIndex = @{}   # mappa ID -> issue number
foreach ($r in $rows) {
  $id            = "$($r.ID)".Trim()
  $titlePlain    = "$($r.Title)".Trim()
  $title         = "$id - $titlePlain"
  $component     = "$($r.Component)".Trim()
  $type          = "$($r.Type)".Trim()
  $priority      = "$($r.Priority)".Trim()
  $effort        = "$($r.Effort)".Trim()
  $deps          = "$($r.Dependencies)".Trim()
  $milestoneName = "$($r.Milestone)".Trim()
  $labelsRaw     = "$($r.Labels)".Trim()
  $desc          = "$($r.Description)"
  $ac            = "$($r.AcceptanceCriteria)"

  $labels = @()
  if ($labelsRaw) {
    $labels = $labelsRaw -split "," | ForEach-Object { $_.Trim() } | Where-Object { $_ -ne "" }
  }

  if ($milestoneName) { $null = Ensure-Milestone -Repo $Repo -Title $milestoneName }

  $body = New-IssueBody -Component $component -Type $type -Priority $priority -Effort $effort `
                        -Dependencies $deps -Description $desc -AcceptanceCriteria $ac

  $existing = Find-IssueByIdInTitle -Repo $Repo -Id $id
  if ($existing) {
    Write-Host "Trovata issue esistente #$existing $title"
    if ($Update) {
      Update-Issue -Repo $Repo -Number $existing -Title $title -Body $body -Labels $labels -MilestoneName $milestoneName
      Write-Host "Aggiornata #$existing"
    } else {
      Write-Host "Skip update (manca -Update)"
    }
    $issueIndex[$id] = $existing
    continue
  }

  $num = Create-Issue -Repo $Repo -Title $title -Body $body -Labels $labels -MilestoneName $milestoneName
  if ($num) {
    Write-Host "Creata issue #$num $title"
    $issueIndex[$id] = $num
  } else {
    Write-Warning "Impossibile determinare il numero issue creata per $id"
  }
}

# 3) Linking dipendenze (commento "Depends on: #X, #Y")
if ($LinkDeps) {
  foreach ($r in $rows) {
    $id      = "$($r.ID)".Trim()
    $depsRaw = "$($r.Dependencies)".Trim()
    if (-not $depsRaw) { continue }
    if (-not $issueIndex.ContainsKey($id)) { continue }

    $depIds = @()
    foreach ($tok in ($depsRaw -split "[,;]")) {
      $t = $tok.Trim()
      if ($t) { $depIds += $t }
    }
    if ($depIds.Count -eq 0) { continue }

    $currentNum = [int]$issueIndex[$id]
    $depNums = @()
    foreach ($did in $depIds) {
      if ($issueIndex.ContainsKey($did)) {
        $depNums += [int]$issueIndex[$did]
      } else {
        $found = Find-IssueByIdInTitle -Repo $Repo -Id $did
        if ($found) { $depNums += [int]$found }
      }
    }
    if ($depNums.Count -gt 0) {
      Add-DependsComment -Repo $Repo -Number $currentNum -DependsOn $depNums
      Write-Host "Aggiunto commento dipendenze su #$currentNum -> $($depNums -join ', ')"
    }
  }
}
