# PDF Wizard - Admin Guide

Guide for administrators and editors using the PDF Wizard to create game entries from PDF rulebooks.

## Overview

The PDF Wizard is a 4-step workflow that creates board game entries in the shared catalog by extracting metadata from uploaded PDF rulebooks, optionally enriching with BoardGameGeek (BGG) data.

## Prerequisites

- **Role**: Admin or Editor
- **PDF**: Board game rulebook in PDF format (max 100 MB)

## Step-by-Step Workflow

### Step 1: Upload PDF

1. Navigate to **Admin > Shared Games > Create from PDF**
2. Drag and drop a PDF file (or click to browse)
3. Wait for upload and automatic text extraction

**Validation rules**:
- File type: PDF only (`.pdf`)
- Maximum size: 100 MB
- Files > 50 MB use optimized processing

**Quality indicator**:
- Green (>= 80%): High quality extraction, most fields populated
- Yellow (50-79%): Medium quality, some fields may need manual input
- Red (< 50%): Low quality, recommend manual input for accuracy

### Step 2: Preview & Edit Metadata

1. Review extracted fields: Title, Year, Players, Playing Time, Age, Description
2. Edit any fields that were incorrectly extracted or missing
3. Review duplicate warnings (if a similar game already exists)
4. Click **Next** to proceed to BGG enrichment, or **Skip BGG** to go directly to confirmation

**Tips**:
- Always verify the Title - it's the most important field
- If confidence is below 50%, consider filling all fields manually
- Duplicate warnings show similar games already in the catalog

### Step 3: BGG Enrichment (Optional)

1. **Search**: Type the game name to search BoardGameGeek
2. **Manual ID**: If you know the BGG ID, enter it directly
3. Review the BGG match and merged data
4. Check **Conflicts** section - shows where PDF and BGG data differ (e.g., different player counts)

**Conflict resolution**: BGG data is preferred by default. Fields where PDF and BGG disagree are highlighted.

**BGG unavailable**: If BGG is unreachable, the wizard preserves PDF-extracted data and shows a warning. You can proceed with PDF data only.

### Step 4: Confirm & Import

1. Review the final merged data summary
2. Click **Confirm Import**

**Behavior by role**:
- **Admin**: Game is created in Draft status, can be published immediately
- **Editor**: Game is created in Draft status with an approval request

## Troubleshooting

| Issue | Solution |
|-------|----------|
| Low quality score (< 50%) | The PDF may be image-based or poorly formatted. Fill fields manually in Step 2. |
| BGG search returns no results | Try shorter search terms or use the Manual ID tab with the BGG game ID from the URL. |
| Upload timeout | Check file size (max 100 MB). Try a smaller file or retry. |
| Duplicate warning | A similar game may already exist. Check the catalog before creating a duplicate. |
| Extraction returns empty fields | The PDF may be a scanned image. OCR is attempted automatically but may fail on complex layouts. |

## Configuration

Relevant settings (managed by system administrators):

| Setting | Default | Description |
|---------|---------|-------------|
| `MaxFileSizeBytes` | 100 MB | Maximum upload size |
| `LargePdfThresholdBytes` | 50 MB | Threshold for temp file optimization |
| `UnstructuredTimeoutSeconds` | 35s | Stage 1 extraction timeout |
| `SmolDoclingTimeoutSeconds` | 30s | Stage 2 extraction timeout |

## Related Documentation

- [Performance Report](../05-testing/performance/pdf-wizard-performance-report.md)
- [SharedGameCatalog README](../../apps/api/src/Api/BoundedContexts/SharedGameCatalog/README.md)
- API Reference: http://localhost:8080/scalar/v1 (tag: "Admin - Game Import Wizard")
