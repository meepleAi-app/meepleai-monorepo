# Issue #4: Component Analysis & Refactor Plan

**Strategy**: Option A (Refactor Shared Components)
**Date**: 2026-02-13

---

## Component Analysis Complete

### **ChatSetupStep.tsx** (270 lines)

**pdfId Dependencies** (4 locations):
```typescript
Line 20:  pdfId: string;  // Props interface
Line 40:  const progress = await api.pdf.getProcessingProgress(pdfId);
Line 104: }, [pdfId]);  // useEffect dependency
Line 27:  // Destructure: { pdfId, ... }
```

**Refactor Required**:
```typescript
// Make pdfId optional
interface ChatSetupStepProps {
  pdfId?: string | null;  // Optional
  // ... other props
}

// Add conditional logic
if (!pdfId) {
  return (
    <Card>
      <p>No PDF uploaded. Agent requires PDF for RAG.</p>
      <Button onClick={onBack}>← Back</Button>
      <Button onClick={() => onComplete(null)}>Skip Agent →</Button>
    </Card>
  );
}

// Rest of component uses pdfId safely (TypeScript narrows type after check)
```

**Complexity**: Low (single conditional check at component entry)
**Risk**: Low (polling logic unchanged if pdfId exists)
**Effort**: 30min

---

### **GameCreationStep.tsx** (363 lines)

**pdfId Dependencies** (5 locations):
```typescript
Line 23:  pdfId: string;  // Props interface
Line 32:  pdfId,  // Destructure
Line 120: pdfId: pdfId,  // API call
Line 184: pdfId,  // useCallback dependency
Line 195: Il PDF "{pdfFileName}" verra' associato  // Description text
```

**Refactor Required**:
```typescript
// Make pdfId optional
interface GameCreationStepProps {
  pdfId?: string | null;  // Optional
  pdfFileName?: string | null;  // Optional
  allowSkipPdf?: boolean;  // Show skip button
  // ... other props
}

// Conditional description
<p>
  {pdfFileName
    ? `Il PDF "${pdfFileName}" verra' associato a questo gioco.`
    : 'Crea il gioco. Potrai aggiungere PDF in seguito.'}
</p>

// Conditional button
{allowSkipPdf && (
  <Button variant="outline" onClick={onSkipPdf}>
    Salta PDF →
  </Button>
)}

// API call with optional pdfId
await api.games.create({
  title: gameName,
  // ...
  pdfId: pdfId || null,  // null if skipped
});
```

**Complexity**: Medium (UI changes + new button)
**Risk**: Low (API already accepts pdfId: null)
**Effort**: 1h

---

## Refactor Execution Plan

### Phase 1: ChatSetupStep (30min)
```yaml
Steps:
  1. Change interface: pdfId?: string | null
  2. Add early return if !pdfId (no-op component)
  3. Update useEffect to handle null pdfId
  4. Test: Verify admin wizard still works

Files Changed: 1
Lines Changed: +15, -3
Risk: ⬇️ Low
```

### Phase 2: GameCreationStep (1h)
```yaml
Steps:
  1. Change interface: pdfId?, pdfFileName? optional
  2. Add allowSkipPdf?: boolean prop
  3. Conditional description text
  4. Add [Skip PDF] button if allowSkipPdf
  5. API call: pdfId || null
  6. Test: Verify admin wizard still works

Files Changed: 1
Lines Changed: +25, -5
Risk: ⬇️ Low (API supports null)
```

### Phase 3: User Wizard Structure (3h)
```yaml
Files to Create:
  - apps/web/src/app/(authenticated)/library/private/add/page.tsx
  - apps/web/src/app/(authenticated)/library/private/add/client.tsx
  - apps/web/src/components/library/user-wizard/UserWizardClient.tsx
  - apps/web/src/components/library/user-wizard/Step1CreateGame.tsx
  - apps/web/src/components/library/user-wizard/Step2UploadPdf.tsx
  - apps/web/src/components/library/user-wizard/Step3ConfigAgent.tsx

State Machine:
  type Step = 'game' | 'pdf' | 'agent' | 'complete';

  Navigation Logic:
    game → pdf (always)
    pdf → agent (if pdfId exists)
    pdf → complete (if skipped)
    agent → complete

Reuse:
  - PdfUploadStep (from admin wizard) ✅
  - GameCreationStep (refactored with allowSkipPdf) ✅
  - ChatSetupStep (refactored with optional pdfId) ✅
```

---

## Risk Mitigation

### Admin Wizard Regression Prevention
```yaml
Before Refactor:
  - [ ] Run admin wizard E2E test baseline
  - [ ] Document expected behavior
  - [ ] Save screenshots/recordings

After Each Refactor:
  - [ ] Run admin wizard E2E again
  - [ ] Compare behavior (should be identical)
  - [ ] Fix any regressions immediately

Final Validation:
  - [ ] Full admin wizard flow (Upload → Game → Chat → QA → Publish)
  - [ ] No visual changes
  - [ ] No functional changes
```

### User Wizard Testing
```yaml
3 Test Paths:
  Path 1 (Full): Game → PDF → Agent → Complete
  Path 2 (Skip PDF): Game → [Skip] → Complete
  Path 3 (Skip Agent): Game → PDF → [Skip Agent] → Complete

Edge Cases:
  - Back navigation from any step
  - Cancel wizard mid-flow
  - PDF upload fails → retry
  - Processing timeout
```

---

## Implementation Timeline

```
Hour 0-0.5:   Refactor ChatSetupStep
Hour 0.5-1.5: Refactor GameCreationStep
Hour 1.5-2:   Test admin wizard (regression)
Hour 2-5:     Create user wizard structure (3h)
Hour 5-8:     Implement Step 1 (3h)
Hour 8-11:    Implement Step 2 (3h)
Hour 11-13:   Implement Step 3 (2h)
Hour 13-16:   Integration + state machine (3h)
Hour 16-19:   Tests (unit + E2E) (3h)
Hour 19-22:   Validation + documentation (3h)
Hour 22-24:   PR + code review (2h)

Total: 24h (~3 days)
```

---

## Next Step

**Immediate**: Start refactoring ChatSetupStep (30min)

**Ready to proceed?**
