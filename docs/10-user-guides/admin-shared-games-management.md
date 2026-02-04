# Admin Guide - Shared Games Catalog Management

**Comprehensive guide for administrators managing the shared games catalog, BGG imports, PDF workflows, and approval queues**

## Overview

As a MeepleAI administrator, you manage the shared games catalog - a community-driven database of board games with associated rulebooks and documentation. This guide covers all aspects of catalog management.

### Admin vs Editor Responsibilities

| Role | Capabilities |
|------|--------------|
| **Editor** | Import games from BGG, upload PDFs, submit for approval |
| **Admin** | All editor capabilities + approve/reject submissions, bulk actions, manage documents |

### Key Workflows at a Glance

```
1. BGG Import → Review → Submit for Approval
2. PDF Upload → RAG Processing → Document Available
3. Approval Queue → Review → Approve/Reject → Published
```

---

## Getting Started

### Accessing the Admin Dashboard

1. Navigate to **Admin Panel** from the main navigation
2. Select **Shared Games** from the admin menu
3. You'll see the shared games dashboard with:
   - Quick stats (total games, pending approvals, documents)
   - Games grid with filters
   - Action buttons for import and management

### Understanding the Interface

**Dashboard Header**:
- **Stats Cards**: Quick overview of catalog health
- **Quick Actions**: Import from BGG, view approval queue

**Games Grid**:
- **Search**: Full-text search across titles, descriptions, publishers
- **Filters**: Status (Draft, Pending, Published, Archived), sort options
- **Game Cards**: Thumbnail, title, status badge, quick actions

**Navigation**:
- **Shared Games**: Main catalog view
- **Approval Queue**: Pending submissions needing review
- **Game Detail**: Individual game management (click any game)

### Quick Tour of Main Features

| Feature | Location | Purpose |
|---------|----------|---------|
| **Import from BGG** | Dashboard header | Add games from BoardGameGeek |
| **Approval Queue** | Dashboard header / Admin nav | Review pending submissions |
| **Game Detail** | Click any game | Manage individual game + documents |
| **Bulk Actions** | Approval queue | Process multiple items at once |

---

## Importing Games from BoardGameGeek

### Single Game Import

**Step 1: Open Import Modal**
```
Dashboard → "Import from BGG" button → Modal opens
```

**Step 2: Enter BGG Identifier**

You can provide either:
- **BGG ID**: The numeric identifier (e.g., `13` for Catan)
- **BGG URL**: Full link (e.g., `https://boardgamegeek.com/boardgame/13/catan`)

The system automatically extracts the ID from URLs.

**Step 3: Preview and Confirm**

After entering a valid ID:
- Preview card shows game title, year, and thumbnail
- System checks for duplicates in existing catalog
- Click **"Import"** to proceed

**Step 4: Post-Import Options**

- **Auto-submit for approval**: Check this to immediately submit for review
- **Manual review first**: Leave unchecked to review as draft before submitting

### Input Validation

| Input | Result |
|-------|--------|
| `13` | Valid - numeric BGG ID |
| `https://boardgamegeek.com/boardgame/13/catan` | Valid - extracts ID 13 |
| `catan` | Invalid - text not accepted |
| `99999999` | Invalid - game not found on BGG |
| `-1` | Invalid - negative numbers rejected |

### Understanding BGG Rate Limits

> **Important**: BoardGameGeek API has rate limits to prevent abuse.

**Rate Limit Behavior**:
- Requests are throttled to prevent API abuse
- Heavy usage may result in temporary delays
- System shows warning when approaching limits

**Best Practices**:
- Import games one at a time, not rapid-fire
- Wait for preview to load before importing
- If you see rate limit errors, wait 5-10 minutes

### Handling Import Errors

| Error | Cause | Solution |
|-------|-------|----------|
| "Game not found" | Invalid BGG ID | Verify ID on boardgamegeek.com |
| "Rate limited" | Too many requests | Wait 5-10 minutes, then retry |
| "Already exists" | Duplicate detection | Game already in catalog - edit existing |
| "Network error" | Connection issue | Check internet, retry |

### Best Practices for Imports

**Do**:
- Verify game exists on BGG before importing
- Review the preview carefully
- Check for existing entries first (use search)
- Import one game at a time

**Don't**:
- Rapid-fire multiple imports (triggers rate limits)
- Import without checking for duplicates
- Ignore the preview information

---

## Managing Game Documents (PDFs)

### Uploading Rulebooks and Player Aids

**Navigate to Documents Tab**:
```
Game Detail Page → Documents Tab → Upload Section
```

**Upload Methods**:
1. **Drag and Drop**: Drag PDF files onto the upload zone
2. **Click to Browse**: Click the upload area to open file picker

**File Requirements**:
| Requirement | Value |
|-------------|-------|
| File Type | PDF only |
| Max Size | 50MB per file |
| Naming | Descriptive names recommended |

### Document Upload Process

```
1. Select PDF → 2. Upload starts → 3. RAG Processing queued → 4. Document ready
```

**Upload States**:
- **Uploading**: File transfer in progress
- **Processing**: RAG system extracting content for AI
- **Ready**: Document available for users
- **Failed**: Error occurred (see troubleshooting)

### Document Approval Workflow

**For Editors**:
Documents uploaded by editors require admin approval before becoming publicly visible.

**For Admins**:
Documents uploaded by admins are auto-approved and immediately available.

**Approval Process**:
1. Admin receives notification of new document
2. Admin reviews document for quality and copyright
3. Admin approves or rejects with feedback

### Understanding RAG Processing

**What is RAG?**
RAG (Retrieval-Augmented Generation) processes PDFs to enable AI-powered Q&A about rulebooks.

**Processing Pipeline**:
```
PDF Upload → Text Extraction → Chunking → Embedding → Vector Storage
```

**Processing Status Indicators**:
| Status | Meaning |
|--------|---------|
| **Pending** | Queued for processing |
| **Processing** | Currently being analyzed |
| **Completed** | Ready for AI queries |
| **Failed** | Error during processing (retry available) |

**What RAG Enables**:
- AI can answer questions about game rules
- Users get page references in answers
- Smart search across all game documentation

### Troubleshooting Failed Uploads

| Issue | Cause | Solution |
|-------|-------|----------|
| "File too large" | Exceeds 50MB | Compress PDF or split into parts |
| "Invalid format" | Not a PDF | Convert to PDF format |
| "Upload failed" | Network issue | Retry upload |
| "Processing failed" | RAG error | Contact tech support, may need manual intervention |

**Retry Processing**:
For failed RAG processing, admins can trigger a retry:
```
Game Detail → Documents Tab → Failed Document → "Retry Processing"
```

---

## Approval Queue Workflow

### Accessing the Approval Queue

```
Admin Panel → Shared Games → "Approval Queue" button
OR
Admin Navigation → Approval Queue
```

### Queue Overview

**Stats Cards**:
- **Total Pending**: Items awaiting review
- **Urgent**: Items pending >7 days (SLA breach)
- **Target SLA**: 3 days for review

**Queue Items Display**:
- Game thumbnail and title
- Days pending badge
- Urgency indicator (red for urgent)
- Quick action buttons

### Filtering and Sorting

**Urgency Filter**:
| Option | Shows |
|--------|-------|
| All Items | Everything in queue |
| Urgent Only | Items pending >7 days |

**Sort Options**:
| Option | Behavior |
|--------|----------|
| Oldest First | Process longest-waiting first (recommended) |
| Newest First | Most recent submissions first |
| Most Urgent | Urgent items prioritized |

### Reviewing Submissions

**Quick Preview**:
Click the eye icon on any queue item to open a preview dialog showing:
- Game title and description
- Thumbnail image
- Submission date
- Quick approve/reject buttons

**Full Detail Review**:
Click "View Full Details" to open the complete game detail page with all information.

### Approval Checklist (Quality Standards)

Before approving, verify:

**Title Verification**:
- [ ] Correct spelling
- [ ] Official game name
- [ ] Appropriate format

**Description Quality**:
- [ ] Accurate game description
- [ ] No inappropriate content
- [ ] No copyright violations

**Metadata Accuracy**:
- [ ] Player count realistic
- [ ] Play time accurate
- [ ] Year published correct

**Documents (if any)**:
- [ ] PDFs are readable
- [ ] Content is appropriate
- [ ] No copyright violations

### Approving Submissions

**Single Approval**:
1. Click the check icon on the queue item
2. Confirm in the dialog
3. Item moves to Published status

**From Detail Page**:
1. Navigate to game detail
2. Click "Approve" button
3. Optionally add admin notes
4. Confirm

### Rejecting Submissions

**Important**: Always provide constructive feedback when rejecting.

**Rejection Process**:
1. Click the X icon on the queue item (or "Reject" on detail page)
2. Enter rejection reason (required)
3. Confirm rejection

**Good Rejection Feedback Examples**:
```
"The description appears to be copied from another source. Please provide
an original description that describes the game mechanics and target audience."

"This game already exists in the catalog (ID #1234). Please review existing
entries before submitting new games."

"The uploaded PDF contains watermarked copyrighted content. Please only
upload documents you have rights to distribute."
```

### Using Bulk Actions

**Selecting Multiple Items**:
- Check individual items using the checkbox
- Use "Select All" to check all visible items
- Selection count shown in bulk action bar

**Bulk Approve**:
1. Select items to approve
2. Click "Approve (N)" button
3. Confirm bulk approval
4. All selected items move to Published

**Bulk Reject**:
1. Select items to reject
2. Click "Reject (N)" button
3. Enter rejection reason (applies to all)
4. Confirm bulk rejection

> **Warning**: Use bulk actions carefully. The rejection reason applies to ALL selected items.

### Auto-Refresh Behavior

The approval queue auto-refreshes every 30 seconds to show new submissions. This ensures you always see the latest queue state without manual refresh.

---

## Game Metadata Management

### Editing Game Details

**Navigate to Edit**:
```
Game Detail Page → Details Tab → Edit button
```

**Editable Fields**:
| Field | Notes |
|-------|-------|
| Title | Official game name |
| Description | Comprehensive game overview |
| Min/Max Players | Supported player counts |
| Play Time | Typical game duration in minutes |
| Min Age | Recommended minimum age |
| Complexity | 1-5 scale (1=light, 5=heavy) |
| Categories | Game categories (strategy, family, etc.) |
| Mechanics | Game mechanisms (deck building, worker placement, etc.) |
| Designer | Game designer(s) |
| Publisher | Game publisher(s) |

### Refreshing from BGG

For games imported from BGG, you can refresh metadata:
```
Game Detail → "Refresh from BGG" button
```

This updates:
- Title, description, player count
- Play time, complexity, age
- Categories and mechanics
- Thumbnail image

> **Note**: Local edits are overwritten when refreshing from BGG.

### Managing Categories, Mechanics, Designers

**Adding**:
1. Click the category/mechanic/designer field
2. Search for existing entry or create new
3. Select to add

**Removing**:
1. Click the X next to the item
2. Confirm removal

### Image Quality Guidelines

| Aspect | Requirement |
|--------|-------------|
| Resolution | Minimum 300x300px |
| Format | JPG, PNG, WebP |
| Content | Box art or official game image |
| Copyright | Must have rights to use |

---

## Troubleshooting

### BGG API Rate Limited

**Symptoms**:
- Import preview fails to load
- "Rate limited" error message
- Long delays in fetching game data

**Solutions**:
1. Wait 5-10 minutes before retrying
2. Reduce import frequency
3. Use BGG URL instead of searching (more efficient)

### PDF Upload Fails

**Symptoms**:
- Upload progress stalls
- Error message after upload
- File appears but status is "Failed"

**Solutions**:
1. Check file size (max 50MB)
2. Verify file is valid PDF
3. Try re-uploading
4. If persistent, contact tech support

### RAG Processing Stuck

**Symptoms**:
- Document stuck in "Processing" for >1 hour
- No progress indication
- Document unavailable for AI queries

**Solutions**:
1. Check system status page
2. Try "Retry Processing" button
3. If stuck >24 hours, contact tech support
4. May need manual intervention for complex PDFs

### Game Not Appearing in Catalog

**Symptoms**:
- Approved game not showing in public catalog
- Status shows Published but not searchable
- Users report game missing

**Solutions**:
1. Verify status is actually "Published" (not Draft)
2. Check if search index needs refresh (automatic, but may take up to 15 minutes)
3. Clear browser cache and retry search
4. Contact tech support if persists >30 minutes

---

## Best Practices

### Review SLA Targets

| Metric | Target | Action if Exceeded |
|--------|--------|-------------------|
| Time to first review | < 3 days | Prioritize oldest items |
| Queue backlog | < 50 items | Request additional reviewers |
| Urgent items | 0 items | Address immediately |

### Quality Standards for Approvals

**Approve When**:
- Description accurately represents the game
- Metadata matches BGG and physical game
- Documents are appropriate and legal
- No duplicate entry exists

**Request Changes When**:
- Minor corrections needed to description
- Metadata is slightly inaccurate
- Documents need better quality

**Reject When**:
- Spam or inappropriate content
- Clear copyright violation
- Duplicate of existing entry
- Submission is not a real board game

### Communication with Editors

**Constructive Feedback**:
- Be specific about what needs fixing
- Provide examples of good submissions
- Explain why standards exist
- Encourage resubmission after corrections

**Tone Guidelines**:
- Professional and respectful
- Helpful and educational
- Encouraging for first-time contributors
- Clear and actionable

### Monitoring Metrics

**Weekly Review**:
- Check queue backlog size
- Address any urgent items
- Review rejection rate (aim for <10%)
- Ensure response times meet SLA

**Monthly Review**:
- Analyze submission patterns
- Identify common rejection reasons
- Update documentation for common issues
- Provide feedback to product team

---

## Screenshots Reference

### Dashboard Overview
The main dashboard shows stats cards, quick actions, and the games grid with filtering options.

### Import from BGG Modal
Modal includes input field, preview section, rate limit warning, and action buttons.

### Game Detail with PDF Upload
Detail page with tabbed interface showing Details, Documents (with upload zone), and Review History.

### Approval Queue
Queue view with stats, filters, item cards with action buttons, and bulk action bar.

### Bulk Actions Interface
Selection checkboxes, count indicator, and bulk approve/reject buttons in action bar.

---

## Related Documentation

- [Share Game Guide](./share-game-guide.md) - User guide for contributing games
- [Share Requests Admin Guide](./share-requests-admin-guide.md) - Managing user share requests
- [Admin Dashboard Guide](../02-development/admin-dashboard-guide.md) - General admin dashboard usage
- [BGG API Token Setup](../02-development/BGG_API_TOKEN_SETUP.md) - Configuring BGG integration

---

**Last Updated**: 2026-02-04
**Version**: 1.0
**Maintainer**: MeepleAI Admin Team
