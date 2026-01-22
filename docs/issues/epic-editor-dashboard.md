# Epic: Editor Dashboard - Content Management Hub

## Overview
Content editor dashboard with pending approvals queue, recent edits activity, and bulk approval/rejection actions.

## Design Mockup
`docs/design-proposals/meepleai-style/final-pages.html` (Tab 1: Editor)

## Key Features
- Stats cards: Pending (12), Approved (145), Rejected (8), Today Activity (23)
- Pending approvals queue with priority sorting (high/medium/low)
- Per-item actions: Review, Approve (green), Reject (red)
- Bulk approve/reject for selected items
- Recent activity log (all editors' actions)
- Filter by status: All, Pending, In Review

## Implementation Issues (6 total)

### Backend (2)
1. GetPendingApprovalsQuery (priority sorting)
2. BulkApproveGamesCommand / BulkRejectGamesCommand

### Frontend (3)
3. Editor dashboard page with stats + queue
4. Approval queue items with priority indicators
5. Bulk approval UI + floating action bar

### Testing (1)
6. Editor E2E (review, approve, reject, bulk operations)

## Existing Related Issues
Check #2729, #2734, #2737, #2745 - some approval workflow exists

## Timeline
1-2 weeks | Priority: MEDIUM (Editor tool)

## Labels
`epic`, `editor-dashboard`, `frontend`, `backend`
