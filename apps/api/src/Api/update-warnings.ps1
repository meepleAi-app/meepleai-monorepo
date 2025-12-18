param(
    [string]$BuildOutput = "build_output.txt",
    [string]$TrackingCsv = "warnings_tracking.csv",
    [switch]$RunBuild
)

function Compute-Hash($s) {
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($s)
    $md5 = [System.Security.Cryptography.MD5]::Create()
    $hash = $md5.ComputeHash($bytes)
    return ([System.BitConverter]::ToString($hash)).Replace('-','').ToLower()
}

function Parse-WarningsFromLines($lines) {
    $warnings = @()
    for ($i=0; $i -lt $lines.Count; $i++) {
        $l = $lines[$i]
        if ($l -match '(?i)\bwarning\b') {
            $trim = $l.Trim()
            $file=''; $line=''; $col=''; $code=''; $msg=''; $proj=''
            if ($trim -match '^(.*\\\.[A-Za-z0-9_]+)\\((\\d+),(\\d+)\\):\\s*warning\\s+([A-Za-z0-9_-]+):\\s*(.+?)\\s*\\[(.+)\\]$') {
                $file=$matches[1]; $line=$matches[2]; $col=$matches[3]; $code=$matches[4]; $msg=$matches[5]; $proj=$matches[6]
            }
            elseif ($trim -match '^\\s*warning\\s+([A-Za-z0-9_-]+):\\s*(.+?)\\s*\\[(.+)\\]$') {
                $code=$matches[1]; $msg=$matches[2]; $proj=$matches[3]
            }
            elseif ($trim -match '^\\s*([^:]+):\\s*warning\\s+([A-Za-z0-9_-]+):\\s*(.+)$') {
                $file=$matches[1]; $code=$matches[2]; $msg=$matches[3]
            }
            else {
                $msg = $trim
                $j = $i+1
                while ($j -lt $lines.Count -and $lines[$j] -match '^\\s') {
                    $msg += ' ' + $lines[$j].Trim()
                    $j++
                }
            }
            $msg = $msg -replace '\\s+',' '
            $prefix = if ($code -ne '') { ($code -replace '[^A-Za-z].*$','') } else { '' }
            switch -regex ($prefix) {
                '^CS' { $cat='Compiler' }
                '^CA' { $cat='CodeAnalysis' }
                '^NU' { $cat='NuGet' }
                '^BC' { $cat='Binding' }
                '^AD' { $cat='Analyzer' }
                '^RS' { $cat='Roslyn' }
                default { $cat='Other' }
            }
            $key = "$code|$file|$line|$msg"
            $id = Compute-Hash($key)
            $warnings += [PSCustomObject]@{
                Id = $id
                Project = $proj
                File = $file
                Line = $line
                Column = $col
                Code = $code
                Category = $cat
                Message = $msg
            }
        }
    }
    return $warnings
}

# Optionally run a clean build to get fresh warnings
if ($RunBuild) {
    Write-Host "Running dotnet build - this may take a while..."
    dotnet build 2>&1 | Tee-Object -FilePath $BuildOutput
}

if (-not (Test-Path $BuildOutput)) {
    Write-Error "Build output file not found: $BuildOutput"
    exit 1
}

$lines = Get-Content $BuildOutput -ErrorAction Stop
$current = Parse-WarningsFromLines -lines $lines
Write-Host "Parsed $($current.Count) warnings from $BuildOutput"

# Load existing tracking CSV if present
$tracking = @{}
if (Test-Path $TrackingCsv) {
    $rows = Import-Csv $TrackingCsv
    foreach ($r in $rows) {
        $tracking[$r.Id] = [PSCustomObject]@{
            Id = $r.Id
            Project = $r.Project
            File = $r.File
            Line = $r.Line
            Column = $r.Column
            Code = $r.Code
            Category = $r.Category
            Message = $r.Message
            Status = $r.Status
            FirstSeen = [datetime]::Parse($r.FirstSeen)
            LastSeen = [datetime]::Parse($r.LastSeen)
            TimesSeen = [int]$r.TimesSeen
            FixedAt = if ($r.FixedAt) { [datetime]::Parse($r.FixedAt) } else { $null }
        }
    }
}

$now = [datetime]::UtcNow.ToString('o')
$foundIds = @{}
foreach ($w in $current) {
    $foundIds[$w.Id] = $true
    if ($tracking.ContainsKey($w.Id)) {
        $entry = $tracking[$w.Id]
        $entry.LastSeen = [datetime]::Parse($now)
        $entry.TimesSeen = $entry.TimesSeen + 1
        $entry.Status = 'Open'
        # update message if changed
        $entry.Message = $w.Message
    }
    else {
        $tracking[$w.Id] = [PSCustomObject]@{
            Id = $w.Id
            Project = $w.Project
            File = $w.File
            Line = $w.Line
            Column = $w.Column
            Code = $w.Code
            Category = $w.Category
            Message = $w.Message
            Status = 'Open'
            FirstSeen = [datetime]::Parse($now)
            LastSeen = [datetime]::Parse($now)
            TimesSeen = 1
            FixedAt = $null
        }
    }
}

# Mark entries not present in current warnings as Fixed (if previously Open)
foreach ($k in $tracking.Keys) {
    if (-not $foundIds.ContainsKey($k)) {
        $entry = $tracking[$k]
        if ($entry.Status -eq 'Open') {
            $entry.Status = 'Fixed'
            $entry.FixedAt = [datetime]::Parse($now)
        }
    }
}

# Export tracking to CSV sorted by Status, TimesSeen desc
$out = $tracking.Values | Sort-Object @{Expression={$_.Status}}, @{Expression={$_.TimesSeen};Descending=$true}
# Normalize datetime formatting
$out2 = $out | ForEach-Object {
    [PSCustomObject]@{
        Id = $_.Id
        Project = $_.Project
        File = $_.File
        Line = $_.Line
        Column = $_.Column
        Code = $_.Code
        Category = $_.Category
        Message = $_.Message
        Status = $_.Status
        FirstSeen = if ($_.FirstSeen) { $_.FirstSeen.ToString('o') } else { '' }
        LastSeen = if ($_.LastSeen) { $_.LastSeen.ToString('o') } else { '' }
        TimesSeen = $_.TimesSeen
        FixedAt = if ($_.FixedAt) { $_.FixedAt.ToString('o') } else { '' }
    }
}
$out2 | Export-Csv -Path $TrackingCsv -NoTypeInformation -Encoding UTF8
Write-Host "Updated tracking CSV: $TrackingCsv (Total entries: $($out2.Count))"

# Also export a flat current warnings CSV for quick reference
$current | Select-Object Id,Project,File,Line,Code,Category,Message | Export-Csv -Path 'warnings_current.csv' -NoTypeInformation -Encoding UTF8
Write-Host "Wrote current warnings to warnings_current.csv"
