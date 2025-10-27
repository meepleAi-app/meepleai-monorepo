# Bulk RuleSpec Export Guide (EDIT-07)

Export multiple game rule specifications as a ZIP archive for backup, migration, or sharing.

## Overview

The bulk export feature allows Editors and Admins to download multiple rule specs at once as a ZIP file containing JSON representations of each rule specification.

## Access Requirements

- **Required Role**: Editor or Admin
- **URL**: `/admin/bulk-export`

## User Interface

### Game Selection

1. Navigate to `/admin/bulk-export`
2. View list of all games with rule specs
3. Select individual games using checkboxes
4. Or use "Select All" to select all games

### Selection Counter

The UI shows how many games are selected:
- "0 of 10 selected" - No games selected
- "5 of 10 selected" - 5 games selected
- "10 of 10 selected" - All games selected

### Export Process

1. **Select Games**: Check one or more games from the list
2. **Click Export**: Click the "Export N Rule Specs" button
3. **Download**: ZIP file automatically downloads to your browser's download folder
4. **Success Message**: "Successfully exported N rule spec(s)" appears

## Export File Format

### ZIP Structure

```
meepleai-rulespecs-2025-10-27.zip
├── chess_v1.json
├── checkers_v2.json
└── tic-tac-toe_v3.json
```

### Filename Format

`{gameId}_{version}.json`

- Special characters in game IDs are sanitized
- Path traversal attempts (../, etc.) are removed
- Max filename length: 50 characters

### JSON Content

Each file contains a complete RuleSpec:

```json
{
  "gameId": "chess",
  "version": "v1",
  "createdAt": "2025-10-27T12:00:00Z",
  "rules": [
    {
      "id": "r1",
      "text": "Two players",
      "section": "Basics",
      "page": "1",
      "line": "1"
    },
    {
      "id": "r2",
      "text": "White moves first",
      "section": "Basics",
      "page": "1",
      "line": "2"
    }
  ]
}
```

## API Endpoint

### Request

```http
POST /api/v1/rulespecs/bulk/export
Content-Type: application/json
Cookie: sessionId=...

{
  "ruleSpecIds": ["chess", "checkers", "tic-tac-toe"]
}
```

### Response

```http
HTTP/1.1 200 OK
Content-Type: application/zip
Content-Disposition: attachment; filename="meepleai-rulespecs-2025-10-27.zip"

[Binary ZIP file content]
```

### Error Responses

| Status | Scenario | Response |
|--------|----------|----------|
| 401 | Not authenticated | `Unauthorized` |
| 403 | Not Editor/Admin | `Forbidden - Editor or Admin role required` |
| 400 | Empty ruleSpecIds | `{ "error": "At least one rule spec ID must be provided" }` |
| 400 | Too many IDs (>100) | `{ "error": "Cannot export more than 100 rule specs at once" }` |
| 500 | No rule specs found | `{ "error": "No rule specs found for the provided game IDs" }` |

## Limitations

- **Maximum Export Size**: 100 rule specs per request
- **Role Requirement**: Editor or Admin only
- **Latest Version Only**: Exports the latest version of each rule spec
- **ZIP Size**: Dependent on rule spec content (typically < 10MB for 100 specs)

## Use Cases

### Backup

Export all rule specs periodically for backup purposes:

1. Select All games
2. Export to ZIP
3. Store ZIP file in secure location

### Migration

Export rule specs from one environment to another:

1. Select games to migrate
2. Export to ZIP
3. Import ZIP in target environment (EDIT-07 Phase 2)

### Sharing

Share rule specs with team members or external parties:

1. Select specific games
2. Export to ZIP
3. Send ZIP file (email, cloud storage, etc.)

## Troubleshooting

### "Please select at least one game to export"

**Cause**: No games selected
**Solution**: Check at least one game checkbox

### "Access denied. Editor or Admin role required"

**Cause**: Logged in as User role
**Solution**: Request Editor or Admin role from administrator

### "Export failed"

**Cause**: Network error or server issue
**Solution**: Check network connection, retry, or contact administrator

### "Cannot export more than 100 rule specs at once"

**Cause**: Selected more than 100 games
**Solution**: Export in batches of 100 or fewer

## Future Features (EDIT-07 Phases 2-4)

- **Bulk Import**: Upload ZIP to import multiple rule specs
- **Bulk Delete**: Delete multiple rule specs with confirmation
- **Bulk Duplicate**: Create copies of multiple rule specs

## Technical Details

- **Backend**: ASP.NET Core 9.0, System.IO.Compression
- **Frontend**: Next.js 14, React hooks
- **Security**: Filename sanitization, path traversal prevention
- **Testing**: Comprehensive unit, integration, and UI tests
- **Performance**: Streaming ZIP creation, no memory bloat

## Related Documentation

- Issue: #428 (EDIT-07)
- Epic: EPIC-04 (Editor & Versioning)
- Sprint: 9-10 (Collaboration)
