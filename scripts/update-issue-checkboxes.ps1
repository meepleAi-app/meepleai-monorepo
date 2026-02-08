# Update Issue Checkboxes for Epic #3875
# Marks all acceptance criteria as complete for merged phases

param(
    [int]$IssueNumber,
    [string]$Repo = "DegrassiAaron/meepleai-monorepo"
)

Write-Host "Updating checkboxes for issue #$IssueNumber..."

# Get current issue body
$body = gh issue view $IssueNumber --repo $Repo --json body -q .body

# Replace all [ ] with [x]
$updatedBody = $body -replace '\- \[ \]', '- [x]'

# Update issue
$tempFile = New-TemporaryFile
$updatedBody | Out-File -FilePath $tempFile.FullName -Encoding UTF8

gh issue edit $IssueNumber --repo $Repo --body-file $tempFile.FullName

Remove-Item $tempFile.FullName

Write-Host "✓ Updated issue #$IssueNumber - all checkboxes marked complete"
