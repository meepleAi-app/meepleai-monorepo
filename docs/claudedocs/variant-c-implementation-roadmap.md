# Variant C Enhanced - Implementation Roadmap

**Created**: 2026-02-04
**Epic**: #3532
**Design Choice**: Split-Pane Master-Detail with Import Wizard

---

## ✅ Final Design Decision

**Chosen**: **Variant C - Split-Pane Master-Detail** with enhancements:
1. ✨ Import Wizard (3-step: Search → Confirm → Upload)
2. ✨ Bulk Import via CSV with global rate limit queue
3. ✨ Advanced Search with filter chips and autocomplete
4. ✨ Import Queue Viewer with real-time progress (SSE)

**Mockup**: `docs/mockups/admin-shared-games-variant-c-enhanced.html`

---

## 🎯 Key Features Breakdown

### **1. Import Wizard** (Issue #3535 Updated)

**User Flow**:
```
Admin/Editor clicks [Import ▼]
  ↓
Dropdown Menu:
  • Search & Import from BGG (wizard)
  • Bulk Import (CSV)
  • View Import Queue (12) ← badge shows active imports
  ↓
[Search & Import] clicked
  ↓
STEP 1: Search BGG
  ├─ Input: "Catan"
  ├─ API searches BGG
  ├─ Shows results:
  │   • Catan (BGG: 13) ✓ selected
  │   • Catan: Cities & Knights (BGG: 926)
  │   • Catan: Seafarers (BGG: 325)
  └─ User selects → [Next]
  ↓
STEP 2: Confirm Details
  ├─ Preview metadata
  ├─ Checkbox: "Auto-submit for approval"
  └─ [Confirm & Continue]
  ↓
STEP 3: Upload PDF (Optional)
  ├─ Game already created in DB
  ├─ Upload rulebook PDF
  ├─ OR [Skip & Finish]
  └─ Done!
```

**Components**:
- `ImportWizardModal.tsx` (wizard container)
- `Step1SearchBgg.tsx` (search + results)
- `Step2ConfirmDetails.tsx` (metadata preview)
- `Step3UploadPdf.tsx` (drag-drop upload)
- `WizardProgress.tsx` (step indicator)

---

### **2. Bulk Import CSV** (Issue #3535 + #3541)

**CSV Format**:
```csv
bgg_id,game_name
13,Catan
68448,7 Wonders
266192,Wingspan
```

**User Flow**:
```
Admin clicks [Import ▼] → [Bulk Import (CSV)]
  ↓
Modal Opens:
  ├─ Shows CSV format example
  ├─ Upload CSV file
  ├─ Validate format
  ├─ Checkbox: "Auto-submit for approval"
  └─ [Start Bulk Import]
  ↓
Queue Modal Opens:
  ├─ Progress: 7/100 games imported [████████░░] 58%
  ├─ ETA: ~93 seconds
  ├─ Queue Items:
  │   ✅ 1. Catan - Completed
  │   ⏳ 2. Wingspan - Processing...
  │      3. Terraforming Mars - Queued
  │      4. Terra Mystica - Queued
  └─ [Close] (background processing continues)
```

**Backend**:
- Parse CSV → Validate format
- Enqueue all games to `BggImportQueue`
- Background worker processes at 1 req/sec
- SSE updates UI in real-time

---

### **3. Global BGG Rate Limit Queue** (Issue #3541 NEW)

**Architecture**:
```
All BGG Requests → BggImportQueueService → Background Worker → BGG API
                                              (1 req/sec)

Queue Storage: PostgreSQL table
Queue Processing: Background hosted service
Rate Limit: 1 request/second (configurable)
Retry Logic: 3 attempts with exponential backoff
```

**Database**:
```sql
BggImportQueue:
├─ Id (UUID)
├─ BggId (INT)
├─ GameName (VARCHAR)
├─ Status (Queued | Processing | Completed | Failed)
├─ Position (INT)
├─ RequestedBy (UUID)
├─ CreatedGameId (UUID)
└─ RetryCount (INT)
```

**APIs**:
```
GET  /api/v1/admin/bgg-import-queue          # Queue status
POST /api/v1/admin/bgg-import-queue/enqueue  # Add to queue
GET  /api/v1/admin/bgg-import-queue/stream   # SSE updates
```

---

### **4. Advanced Search** (Issue #3534 Updated)

**Features**:

**Multi-Field Search**:
- Title (full-text)
- BGG ID (exact match)
- Publisher (partial match)
- Designer (partial match)

**Filter System**:
```
Active Filters (Chips):
[All Games ▼] [Pending (23)] [Has PDFs] [×Remove]

Quick Filters (Buttons):
[ Pending ] [ Draft ] [ Published ]
[ Has PDFs ] [ No PDFs ] [ Recent ] [ Urgent >7d ]
```

**Search Behavior**:
- Debounced 300ms
- Updates URL params: `?search=wing&status=pending&hasPdfs=true`
- Shareable links (copy URL = share search)
- Autocomplete suggestions

**Components**:
- `SearchBar.tsx` (main input with icon)
- `FilterChips.tsx` (active filters row)
- `QuickFilters.tsx` (preset buttons)
- `SearchSuggestions.tsx` (autocomplete dropdown)

---

## 🏗️ Implementation Order (Updated)

**Critical Path**:
```
1. #3541 (BGG Queue Service) ← FOUNDATIONAL
   ↓
2. #3533 (Backend APIs) ← Depends on queue
   ↓
3. #3534 (Dashboard Split-Pane) ← UI foundation
   ↓
4. #3535 (Import Wizard) ← Depends on backend
   ↓
5. #3536 (Game Detail + PDF)
   ↓
6. #3537 (Approval Queue)
   ↓
7. #3538 (E2E Tests)
   ↓
8. #3539 (Documentation)
```

**Parallel Work Opportunities**:
- #3534 (Dashboard UI) + #3541 (Queue Service) can start in parallel
- #3535 (Wizard) + #3536 (Detail) can be done by different devs

---

## 📊 Updated Timeline Estimate

| Issue | Component | Original ETA | Updated ETA | Reason |
|-------|-----------|--------------|-------------|--------|
| #3541 | BGG Queue Service | N/A | **5-7 days** | New issue |
| #3533 | Backend APIs | 3-5d | **4-6 days** | +Search, +CSV |
| #3534 | Dashboard | 5-7d | **6-8 days** | +Split-pane, +Adv Search |
| #3535 | Import | 3-5d | **5-7 days** | +Wizard, +Bulk |
| #3536 | Detail+Upload | 5-7d | **5-7 days** | Unchanged |
| #3537 | Approval Queue | 3-5d | **3-5 days** | Unchanged |
| #3538 | E2E Tests | 3-5d | **4-6 days** | +Wizard tests |
| #3539 | Docs | 2-3d | **2-3 days** | Unchanged |

**Original Total**: 24-37 days (5-7 weeks)
**Updated Total**: **34-49 days (7-10 weeks)**

**Added Complexity**: +10-12 days
- Import Wizard: +2-3 days
- BGG Queue Service: +5-7 days
- Advanced Search: +2-3 days
- Bulk Import CSV: +1-2 days (overlaps with queue)

---

## 🎨 Design Specifications (Variant C)

### **Layout Dimensions**

```
Screen Layout:
├─ Top Bar: 100% width × 72px (sticky)
├─ Master Pane: 420px × calc(100vh - 72px)
└─ Detail Pane: Flexible × calc(100vh - 72px)

Master Pane Sections:
├─ Header: 100% × auto (title + search)
├─ Filter Pills: 100% × auto (wrappable)
└─ Game List: 100% × flexible (scrollable)

Detail Pane Content:
├─ Max-width: 900px (centered)
├─ Padding: 2rem
└─ Sections: Stack vertically
```

### **Component Spacing**

- List item padding: 1rem
- Section spacing: 1.5rem between sections
- Card internal padding: 1.75rem
- Filter chips gap: 0.5rem
- Button gap: 0.75rem

### **Responsive Breakpoints**

```css
/* Desktop (default): Split-pane */
@media (min-width: 1024px) {
  grid-template-columns: 420px 1fr;
}

/* Tablet: Single pane with toggle */
@media (max-width: 1023px) {
  grid-template-columns: 1fr;
  /* Show master OR detail, toggle button */
}

/* Mobile: Full-screen modals */
@media (max-width: 768px) {
  /* Master list full screen */
  /* Detail opens as full-screen modal */
}
```

---

## 🔄 Queue Integration Flow

### **Single Import**

```typescript
// User searches "Catan"
const searchResults = await searchBggGames({ query: "Catan" });

// User selects result
const selected = searchResults[0]; // BGG: 13

// User confirms
const queueItem = await enqueueBggImport({
  bggId: selected.bggId,
  gameName: selected.name,
  userId: currentUser.id
});

// Response:
{
  queueItemId: "uuid",
  position: 3,        // 3rd in queue
  estimatedWait: 3    // 3 seconds
}

// Frontend subscribes to SSE
const eventSource = new EventSource('/api/v1/admin/bgg-import-queue/stream');
eventSource.onmessage = (event) => {
  const update = JSON.parse(event.data);
  if (update.queueItemId === queueItem.id) {
    // Update UI: position, status, ETA
  }
};
```

### **Bulk Import CSV**

```typescript
// User uploads CSV (100 games)
const formData = new FormData();
formData.append('file', csvFile);
formData.append('autoSubmitForApproval', 'true');

const response = await bulkImportFromCsv(formData);

// Response:
{
  queuedCount: 100,
  queueItemIds: ["uuid1", "uuid2", ...],
  estimatedTime: "100 seconds",
  batchId: "batch-uuid"
}

// Open queue modal
showImportQueueModal(response.batchId);

// SSE updates show progress
{
  batchId: "batch-uuid",
  progress: { completed: 47, total: 100, percentage: 47 },
  currentItem: { name: "Wingspan", status: "Processing" },
  eta: 53
}
```

---

## 📋 Updated Acceptance Criteria

### **Epic #3532 - Additional Criteria**

**Import Wizard**:
- [ ] Step 1: BGG search returns results, user can select
- [ ] Step 2: Metadata preview shown before creation
- [ ] Step 3: Optional PDF upload after game creation
- [ ] Wizard progress indicator shows current step
- [ ] Can navigate back/forward between steps
- [ ] Skip PDF upload works correctly

**Bulk Import**:
- [ ] CSV upload validates format before processing
- [ ] All games enqueued to global rate limit queue
- [ ] Queue processes at exactly 1 req/sec
- [ ] Queue viewer shows real-time progress (SSE)
- [ ] Failed imports show error message and retry option
- [ ] Batch completion triggers notification

**Advanced Search**:
- [ ] Search supports multi-field queries
- [ ] Filter chips display active filters
- [ ] Quick filter buttons toggle on/off
- [ ] Search debounced to 300ms
- [ ] URL params sync with search state
- [ ] Autocomplete shows suggestions
- [ ] Clear all filters button works

**Split-Pane Layout**:
- [ ] Master list (420px) shows games with compact items
- [ ] Detail pane shows selected game info
- [ ] Clicking list item updates detail pane
- [ ] Detail pane scrolls independently
- [ ] Master list maintains scroll position on selection
- [ ] Responsive: single-pane mode on mobile

---

## 🧪 Testing Requirements (Updated)

### **New E2E Tests** (Issue #3538)

**Import Wizard**:
```typescript
test('Import wizard full flow', async ({ page }) => {
  // Step 1: Search
  await page.click('[data-testid="import-dropdown"]');
  await page.click('text=Search & Import');
  await page.fill('[data-testid="bgg-search"]', 'Catan');
  await page.waitForSelector('.bgg-result-item');
  await page.click('.bgg-result-item >> text=Catan');
  await page.click('button:has-text("Next")');

  // Step 2: Confirm
  await expect(page.locator('text=Confirm Details')).toBeVisible();
  await page.click('button:has-text("Confirm & Continue")');

  // Step 3: Upload
  await expect(page.locator('text=Game Created: Catan')).toBeVisible();
  await page.setInputFiles('[data-testid="pdf-upload"]', 'test.pdf');

  // Verify created
  await expect(page.locator('text=Catan')).toBeVisible();
});
```

**Bulk Import**:
```typescript
test('Bulk CSV import with queue', async ({ page }) => {
  await page.click('text=Bulk Import (CSV)');
  await page.setInputFiles('[data-testid="csv-upload"]', 'games.csv');
  await page.click('button:has-text("Start Bulk Import")');

  // Verify queue modal
  await expect(page.locator('text=BGG Import Queue')).toBeVisible();

  // Wait for SSE update (mock in test)
  await expect(page.locator('text=7 / 12')).toBeVisible({ timeout: 5000 });
});
```

**Advanced Search**:
```typescript
test('Advanced search with multiple filters', async ({ page }) => {
  await page.goto('/admin/shared-games');

  // Search
  await page.fill('[data-testid="search-input"]', 'wing');

  // Apply filters
  await page.click('button:has-text("Pending")');
  await page.click('button:has-text("Has PDFs")');

  // Verify URL
  expect(page.url()).toContain('search=wing&status=pending&hasPdfs=true');

  // Verify filter chips
  await expect(page.locator('.filter-chip:has-text("Pending")')).toHaveClass(/active/);
});
```

---

## 📦 Deliverables Summary

### **Mockups Created** (3 Variants)

1. ✅ **Variant A**: `admin-shared-games-meepleai.html` (Card Grid + Sidebar)
2. ✅ **Variant B**: `admin-shared-games-variant-b-table.html` (Table Dense + Top Nav)
3. ✅ **Variant C Enhanced**: `admin-shared-games-variant-c-enhanced.html` (Split-Pane + Wizard) ← **CHOSEN**

### **Documentation**

1. ✅ `variant-c-enhanced-specification.md` (This file's precursor)
2. ✅ `design-variants-comparison.md` (3-way comparison)
3. ✅ `variant-c-implementation-roadmap.md` (This file)
4. ✅ `epic-admin-shared-games-management.md` (Original epic spec)

### **GitHub Issues**

**Epic**: #3532 (Updated with new requirements)

**Sub-Issues** (Updated):
- #3533 - Backend APIs (+Search, +CSV parsing)
- #3534 - Frontend Dashboard (+Split-pane, +Adv Search)
- #3535 - Import Wizard (+3 steps, +Bulk)
- #3536 - Game Detail + PDF Upload (unchanged)
- #3537 - Approval Queue (unchanged)
- #3538 - E2E Tests (+Wizard, +Bulk, +Search)
- #3539 - Documentation (unchanged)

**New Issue**:
- #3541 - BGG Queue Background Service ← **START HERE**

---

## 🎯 Implementation Priority

### **Phase 1: Foundation** (2 weeks)
**Focus**: Queue service + backend APIs
- #3541 - BGG Queue Service (5-7d)
- #3533 - Backend APIs (4-6d)

**Deliverable**: Working queue + API endpoints

### **Phase 2: Core UI** (2-3 weeks)
**Focus**: Split-pane dashboard + wizard
- #3534 - Dashboard Split-Pane (6-8d)
- #3535 - Import Wizard (5-7d)

**Deliverable**: Import workflow functional

### **Phase 3: Details** (2 weeks)
**Focus**: Detail panel + approval queue
- #3536 - Game Detail + PDF (5-7d)
- #3537 - Approval Queue (3-5d)

**Deliverable**: Full admin workflow

### **Phase 4: Quality** (1 week)
**Focus**: Testing + documentation
- #3538 - E2E Tests (4-6d)
- #3539 - Admin Guide (2-3d)

**Deliverable**: Production-ready

**Total**: **7-10 weeks**

---

## 🎨 Final Design Highlights

**What Makes Variant C Special**:
- 🎯 **Focus Mode**: One game at a time, deep review
- 📱 **No Modal Fatigue**: Detail always visible
- ⌨️ **Keyboard Friendly**: Arrow keys navigate list
- 🚀 **Fast Actions**: Buttons always in view
- 📊 **Context Preservation**: Selected game stays visible
- 🎨 **MeepleAI Brand**: Warm beige, orange/purple, wood texture

**Interactive Elements**:
- Hover lift on list items
- Smooth transitions between games
- Animated status dots
- Progress bars with gradients
- Glass morphism cards

---

## 🔗 Quick Links

**Mockups**:
- [Variant C Enhanced](../mockups/admin-shared-games-variant-c-enhanced.html) ← **FINAL CHOICE**
- [Variant B Table](../mockups/admin-shared-games-variant-b-table.html)
- [Variant A Card Grid](../mockups/admin-shared-games-meepleai.html)

**Issues**:
- [Epic #3532](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3532)
- [Backend #3533](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3533)
- [Dashboard #3534](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3534)
- [Wizard #3535](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3535)
- [BGG Queue #3541](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3541) ← **NEW**

---

**Status**: ✅ Design finalized, issues updated, ready to start implementation with #3541
