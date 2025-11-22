# Analyze codebase complexity
param(
    [string]$Path,
    [string]$Pattern = "*.cs",
    [int]$Top = 20
)

Get-ChildItem -Path $Path -Recurse -File -Include $Pattern |
    Where-Object { $_.Length -gt 0 } |
    ForEach-Object {
        $lines = (Get-Content $_.FullName | Measure-Object -Line).Lines
        [PSCustomObject]@{
            File = $_.FullName.Replace("D:\Repositories\meepleai-monorepo\", "")
            Lines = $lines
        }
    } |
    Sort-Object -Property Lines -Descending |
    Select-Object -First $Top |
    Format-Table -AutoSize
