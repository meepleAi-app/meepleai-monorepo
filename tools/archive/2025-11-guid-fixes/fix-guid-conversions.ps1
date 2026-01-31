# Fix all Guid/string conversion errors in service files
# This script systematically converts Guid parameters to strings where needed

$ErrorActionPreference = "Stop"

# Define files and their fixes
$fixes = @(
    # ConfigurationService.cs
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\ConfigurationService.cs"
        Line = 671
        Old = '            resourceId: config.Id,'
        New = '            resourceId: config.Id.ToString(),'
    },

    # RuleSpecCommentService.cs - Multiple fixes
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleSpecCommentService.cs"
        Line = 48
        Old = '                ParentCommentId = request.ParentCommentId,'
        New = '                ParentCommentId = request.ParentCommentId != null ? Guid.Parse(request.ParentCommentId) : null,'
    },
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleSpecCommentService.cs"
        Line = 59
        Old = '            resourceId: comment.Id,'
        New = '            resourceId: comment.Id.ToString(),'
    },
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleSpecCommentService.cs"
        Line = 61
        Old = '            additionalData: comment.ParentCommentId,'
        New = '            additionalData: comment.ParentCommentId?.ToString(),'
    },
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleSpecCommentService.cs"
        Line = 62
        Old = '            userId: request.UserId);'
        New = '            userId: request.UserId.ToString());'
    },
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleSpecCommentService.cs"
        Line = 81
        Old = '                resourceId: comment.Id,'
        New = '                resourceId: comment.Id.ToString(),'
    },
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleSpecCommentService.cs"
        Line = 83
        Old = '                additionalData: comment.ParentCommentId,'
        New = '                additionalData: comment.ParentCommentId?.ToString(),'
    },
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleSpecCommentService.cs"
        Line = 84
        Old = '                userId: request.UserId);'
        New = '                userId: request.UserId.ToString());'
    },

    # RuleCommentService.cs
    @{
        File = "D:\Repositories\meepleai-monorepo\apps\api\src\Api\Services\RuleCommentService.cs"
        Line = 56
        Old = '            _cache.Remove($"rule-comments-{commentId}");'
        New = '            _cache.Remove($"rule-comments-{commentId.ToString()}");'
    }
)

Write-Host "Fixing Guid/string conversion errors..." -ForegroundColor Cyan

foreach ($fix in $fixes) {
    Write-Host "Processing $($fix.File)..." -ForegroundColor Yellow

    if (Test-Path $fix.File) {
        $content = Get-Content $fix.File -Raw

        if ($content.Contains($fix.Old)) {
            $content = $content.Replace($fix.Old, $fix.New)
            Set-Content $fix.File -Value $content -NoNewline
            Write-Host "  Fixed line $($fix.Line)" -ForegroundColor Green
        } else {
            Write-Host "  Pattern not found (may already be fixed)" -ForegroundColor Gray
        }
    } else {
        Write-Host "  File not found!" -ForegroundColor Red
    }
}

Write-Host "`nAll fixes applied. Running build to check remaining errors..." -ForegroundColor Cyan
