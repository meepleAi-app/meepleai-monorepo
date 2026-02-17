# ✅ Validation Checklist: Flusso 1 - Creazione Manuale SharedGame

**Date:** 2026-02-12
**Tester:** Manual validation
**Objective:** Verificare che il flusso manuale funzioni end-to-end

---

## 🎯 Flusso da Validare

```
Login Admin → Dashboard → Gestisci SharedGame → Crea Manuale → Visualizza Creato
```

---

## 📋 Prerequisites

### 1. Services Running
```bash
# Backend API
cd apps/api/src/Api
dotnet run
# Expected: API listening on http://localhost:8080

# Frontend
cd apps/web
pnpm dev
# Expected: Next.js on http://localhost:3000

# Infrastructure
cd infra
docker compose up -d postgres qdrant redis
# Expected: 3 containers running
```

### 2. Admin User Exists
- Username/Email: admin@meepleai.com (o tuo admin user)
- Password: configurato
- Role: Admin

### 3. Database Migrated
```bash
cd apps/api/src/Api
dotnet ef database update
# Expected: Latest migration applied
```

---

## 🧪 Step-by-Step Validation

### **Step 1: Login as Admin** ✅/❌

**URL:** http://localhost:3000/login

**Actions:**
1. Open browser → http://localhost:3000/login
2. Enter admin credentials
3. Click "Login"

**Expected Result:**
- ✅ Redirect to `/admin` or `/admin/dashboard`
- ✅ Sidebar shows "Admin" menu items
- ✅ No console errors

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________
```

---

### **Step 2: Navigate to Dashboard** ✅/❌

**URL:** http://localhost:3000/admin

**Actions:**
1. Verify admin dashboard loads
2. Look for "Shared Games" or "SharedGameCatalog" section

**Expected Result:**
- ✅ Dashboard displays admin widgets
- ✅ "Shared Games" block or link visible
- ✅ Stats load correctly

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________
```

---

### **Step 3: Navigate to SharedGame Management** ✅/❌

**URL:** http://localhost:3000/admin/shared-games

**Actions:**
1. Click "Shared Games" link/button
2. Verify SharedGame list page loads

**Expected Result:**
- ✅ URL is `/admin/shared-games`
- ✅ List of existing SharedGames displayed (or empty state)
- ✅ Filters visible (search, status filter)
- ✅ "Nuovo Gioco" or "Create" button visible
- ✅ View mode toggle (grid/list) works

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________

Screenshots:
```

---

### **Step 4: Click "Create Manual" / "Nuovo Gioco"** ✅/❌

**URL:** Should redirect to `/admin/shared-games/new`

**Actions:**
1. Click "Nuovo Gioco" button
2. Verify form page loads

**Expected Result:**
- ✅ URL is `/admin/shared-games/new`
- ✅ "Nuovo Gioco" title displayed
- ✅ GameForm component renders
- ✅ All form fields visible:
  - Title (required)
  - Publisher
  - Year Published
  - Min Players
  - Max Players
  - Playing Time (minutes)
  - Min Age
  - Description
  - Image URL / Upload
  - Thumbnail URL / Upload
- ✅ "Cancel" and "Submit" buttons visible

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________

Form fields present:
[ ] Title
[ ] Publisher
[ ] Year
[ ] Players (min/max)
[ ] Playing Time
[ ] Min Age
[ ] Description
[ ] Images
```

---

### **Step 5: Fill Form with Test Data** ✅/❌

**Actions:**
Fill form with these values:

```yaml
Title: "Validation Test Game"
Publisher: "Test Publisher Inc."
Year Published: 2024
Min Players: 2
Max Players: 4
Playing Time: 60
Min Age: 12
Description: "This is a test game for Flusso 1 validation. Created on 2026-02-12."
Image URL: "https://via.placeholder.com/400x300"
Thumbnail URL: "https://via.placeholder.com/150x150"
```

**Expected Result:**
- ✅ All fields accept input
- ✅ Validation messages appear for required fields (if left empty)
- ✅ No console errors during typing
- ✅ Form state updates correctly

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________
```

---

### **Step 6: Submit Form** ✅/❌

**Actions:**
1. Click "Submit" or "Crea Gioco" button
2. Wait for API call

**Expected Result:**
- ✅ Loading indicator appears
- ✅ API call to `POST /api/v1/admin/shared-games`
- ✅ HTTP 201 Created response
- ✅ Redirect to `/admin/shared-games/{gameId}`

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________

API Response:
Status: ___
Body: ___________________
```

---

### **Step 7: Verify Game Created in Detail View** ✅/❌

**URL:** http://localhost:3000/admin/shared-games/{gameId}

**Actions:**
1. Verify redirect to detail page
2. Check all fields displayed

**Expected Result:**
- ✅ URL contains valid GUID: `/admin/shared-games/[uuid]`
- ✅ Title: "Validation Test Game"
- ✅ Publisher: "Test Publisher Inc."
- ✅ Year: 2024
- ✅ Players: "2-4 players"
- ✅ Playing Time: "60 min"
- ✅ Min Age: "12+"
- ✅ Description displayed
- ✅ Images render (placeholder URLs)
- ✅ Status badge: "Draft" (or "PendingApproval")
- ✅ Tabs visible: Details, Documents, Review History

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________

Game ID created: ___________________
Status shown: ___________________
```

---

### **Step 8: Verify Game in Database** ✅/❌

**Actions:**
Query database to confirm persistence

```sql
SELECT Id, Title, Publisher, YearPublished, Status, IsDeleted, CreatedAt
FROM SharedGames
WHERE Title = 'Validation Test Game'
ORDER BY CreatedAt DESC
LIMIT 1;
```

**Expected Result:**
- ✅ Record exists in `SharedGames` table
- ✅ Title: "Validation Test Game"
- ✅ Status: "Draft" (enum value 0)
- ✅ IsDeleted: false
- ✅ CreatedAt: recent timestamp

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________

Database record:
```

---

### **Step 9: Verify Game in List** ✅/❌

**URL:** http://localhost:3000/admin/shared-games

**Actions:**
1. Navigate back to SharedGame list
2. Search for "Validation Test Game"
3. Verify it appears in list

**Expected Result:**
- ✅ Game appears in list
- ✅ MeepleCard shows correct title
- ✅ Status badge visible
- ✅ Can click to open detail again

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________
```

---

### **Step 10: Cleanup** ✅/❌

**Actions:**
Delete the test game

**Option A - Via UI:**
1. Open detail page
2. Click "Delete" or "Archive"
3. Confirm deletion

**Option B - Via API:**
```bash
curl -X DELETE http://localhost:8080/api/v1/admin/shared-games/{gameId} \
  -H "Authorization: Bearer {token}"
```

**Option C - Via Database:**
```sql
UPDATE SharedGames
SET IsDeleted = true, DeletedAt = CURRENT_TIMESTAMP
WHERE Title = 'Validation Test Game';
```

**Expected Result:**
- ✅ Game soft-deleted (IsDeleted = true)
- ✅ No longer appears in list

**Actual Result:**
```
[ ] Pass
[ ] Fail - Error: ___________________
```

---

## 📊 Summary

| Step | Status | Notes |
|------|--------|-------|
| 1. Login | ⬜ | |
| 2. Dashboard | ⬜ | |
| 3. Navigate to SharedGame | ⬜ | |
| 4. Open create form | ⬜ | |
| 5. Fill form | ⬜ | |
| 6. Submit | ⬜ | |
| 7. View created | ⬜ | |
| 8. Database check | ⬜ | |
| 9. List verification | ⬜ | |
| 10. Cleanup | ⬜ | |

**Overall Result:**
```
[ ] ✅ PASS - Flusso 1 funzionante al 100%
[ ] ⚠️ PARTIAL - Funziona ma con issue
[ ] ❌ FAIL - Blockers trovati
```

---

## 🐛 Issues Found

| Issue | Severity | Description | Fix Required |
|-------|----------|-------------|--------------|
| 1. | | | |
| 2. | | | |
| 3. | | | |

---

## 📝 Notes

**Performance:**
- Form load time: ___ ms
- Submit response time: ___ ms
- Total flow time: ___ seconds

**UX Observations:**
- Form validation: ___________________
- Error messages: ___________________
- Loading states: ___________________

**Console Errors:**
```
(Paste any console errors here)
```

**Network Errors:**
```
(Paste any failed API calls)
```

---

## ✅ Sign-Off

**Validated By:** ___________________
**Date:** 2026-02-12
**Status:** ⬜ Approved for production | ⬜ Needs fixes | ⬜ Blocked

**Next Actions:**
```
[ ] Create issue #4231 (E2E test)
[ ] Document blockers
[ ] Proceed with Flusso 2
```

---

**Checklist complete:** ___/10 steps passed
