# Issue #1801 — Unify `nameof(PdfProcessingState.X)` vs string literals Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Sostituire i raw string literals di `PdfProcessingState` (`"Pending"`, `"Chunking"`, ecc.) con `nameof(PdfProcessingState.X)` nel BC `DocumentProcessing`, e derivare la `InFlightStates` HashSet di `ReindexDocumentCommandHandler` da `Enum.GetNames<PdfProcessingState>()` per rename-safety end-to-end.

**Architecture:** Refactor puro (zero cambi semantici). I 4 file noti contengono 12 raw string literals + 1 HashSet con stringhe duplicate. Tutte le sostituzioni mantengono identica la semantica wire (le enum vengono serializzate in PascalCase identiche ai literals attuali). Nessun cambio a DB, migration, DTO o wire format.

**Tech Stack:** .NET 9, C# (no runtime libs), xUnit per smoke verify.

**Issue**: [#1801](https://github.com/meepleAi-app/meepleai-monorepo/issues/1801) (P3, tech-debt)

**Branch**: `feature/issue-1801-nameof-refactor` (parent: `main-dev`)

---

## Scope freeze (out-of-scope)

- File esclusi: file con match `"Pending|...|Failed"` su un'altra colonna/contesto (es. `OutboxStatus = "Pending"`, `ProcessingPriority = "Normal"`, log template strings).
- Refactor di altri bounded context (UserLibrary, Infrastructure/BackgroundServices) — già canonici.
- Wire format / migration / DTO — invariati.

**Inventario completo (verificato via plan review 2026-06-02)**: 31 raw literals + 1 HashSet derive in **8 file** del BC `DocumentProcessing` (originale v1 del plan limitava a 4 file / 12 literals — incorretto, ora corretto).

## File structure

### Modified
| Path | Change | Lines |
|------|--------|-------|
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` | 7 literals → `nameof()` | 140, 316, 325, 334, 724, 743, 746 |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs` | 5 literals → `nameof()` | 477, 522, 636, 650, 791 |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/VectorDocumentReadyStateHandler.cs` | 3 literals → `nameof()` | 34, 35, 46 |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ExtractPdfTextCommandHandler.cs` | 3 literals → `nameof()` | 75, 90, 109 |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs` | 5 literals → `nameof()` | 77, 86, 98, 108, 148 |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/PurgeStaleDocumentsCommandHandler.cs` | 1 literal → `nameof()` | 41 |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs` | 6 literals → `nameof()` | 170, 214, 462, 704, 752, 786 |
| `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs` | 1 literal (line 83 `"Pending"`) + HashSet derivata da `Enum.GetNames<PdfProcessingState>()` | 21-30 (HashSet), 83 |

### Created
| Path | Purpose |
|------|---------|
| (none) | Refactor puro — nessun nuovo file. |

---

## Tasks

### Task 1: Pre-flight + branch hygiene

**Files:**
- (no edit)

- [ ] **Step 1: Verify clean state**

```bash
cd D:/Repositories/meepleai-monorepo-dev
git status --short
git branch --show-current
```

Expected: clean tree (eccetto `docs/superpowers/plans/*` untracked) and HEAD is on `feature/issue-1673-reindex-version-selector` OR `main-dev`. If on the #1673 branch, switch and pull first:

```bash
git checkout main-dev
git pull --ff-only
```

Expected: `Already up to date.` OR fast-forward.

- [ ] **Step 2: Create feature branch**

```bash
git checkout -b feature/issue-1801-nameof-refactor
git config branch.feature/issue-1801-nameof-refactor.parent main-dev
git branch --show-current
```

Expected: `feature/issue-1801-nameof-refactor`.

- [ ] **Step 3: Confirm baseline build green**

```bash
dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Error(s).`

(No commit in this task — setup only.)

---

### Task 2: Bulk refactor 7 product files (30 literals)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs` (7 literals)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs` (5 literals)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/VectorDocumentReadyStateHandler.cs` (3 literals)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ExtractPdfTextCommandHandler.cs` (3 literals)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs` (5 literals)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/PurgeStaleDocumentsCommandHandler.cs` (1 literal)
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs` (6 literals)

> **Note**: `ReindexDocumentCommandHandler.cs` is handled separately in Task 3 (combines 1 literal + HashSet derivation).

#### Master substitution table

For EACH file, apply these substitutions. The pattern is uniform: `pdfDoc.ProcessingState = "X"` → `pdfDoc.ProcessingState = nameof(PdfProcessingState.X)`, and `string.Equals(pdfDoc.ProcessingState, "X", ...)` → `string.Equals(pdfDoc.ProcessingState, nameof(PdfProcessingState.X), ...)`.

| File | Line | Current | Replacement |
|------|------|---------|-------------|
| `PdfProcessingPipelineService.cs` | 140 | `pdfDoc.ProcessingState = "Chunking";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Chunking);` |
| `PdfProcessingPipelineService.cs` | 316 | `pdfDoc.ProcessingState = "Embedding";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Embedding);` |
| `PdfProcessingPipelineService.cs` | 325 | `pdfDoc.ProcessingState = "Indexing";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Indexing);` |
| `PdfProcessingPipelineService.cs` | 334 | `pdfDoc.ProcessingState = "Ready";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Ready);` |
| `PdfProcessingPipelineService.cs` | 724 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `PdfProcessingPipelineService.cs` | 743 | `&& !string.Equals(pdfDoc.ProcessingState, "Ready", StringComparison.Ordinal))` | `&& !string.Equals(pdfDoc.ProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal))` |
| `PdfProcessingPipelineService.cs` | 746 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `CompleteChunkedUploadCommandHandler.cs` | 477 | `pdfDoc.ProcessingState = "Ready";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Ready);` |
| `CompleteChunkedUploadCommandHandler.cs` | 522 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `CompleteChunkedUploadCommandHandler.cs` | 636 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `CompleteChunkedUploadCommandHandler.cs` | 650 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `CompleteChunkedUploadCommandHandler.cs` | 791 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `VectorDocumentReadyStateHandler.cs` | 34 | `if (string.Equals(evt.CurrentProcessingState, "Ready", StringComparison.Ordinal)` | `if (string.Equals(evt.CurrentProcessingState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal)` |
| `VectorDocumentReadyStateHandler.cs` | 35 | `\|\| string.Equals(evt.CurrentProcessingState, "Failed", StringComparison.Ordinal))` | `\|\| string.Equals(evt.CurrentProcessingState, nameof(PdfProcessingState.Failed), StringComparison.Ordinal))` |
| `VectorDocumentReadyStateHandler.cs` | 46 | `pdfEntity.ProcessingState = "Ready";` | `pdfEntity.ProcessingState = nameof(PdfProcessingState.Ready);` |
| `ExtractPdfTextCommandHandler.cs` | 75 | `pdf.ProcessingState = "Extracting";` | `pdf.ProcessingState = nameof(PdfProcessingState.Extracting);` |
| `ExtractPdfTextCommandHandler.cs` | 90 | `pdf.ProcessingState = "Failed";` | `pdf.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `ExtractPdfTextCommandHandler.cs` | 109 | `pdf.ProcessingState = "Ready";` | `pdf.ProcessingState = nameof(PdfProcessingState.Ready);` |
| `IndexPdfCommandHandler.cs` | 77 | `pdf!.ProcessingState = "Indexing";` | `pdf!.ProcessingState = nameof(PdfProcessingState.Indexing);` |
| `IndexPdfCommandHandler.cs` | 86 | `pdf.ProcessingState = "Failed";` | `pdf.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `IndexPdfCommandHandler.cs` | 98 | `pdf.ProcessingState = "Failed";` | `pdf.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `IndexPdfCommandHandler.cs` | 108 | `pdf.ProcessingState = "Ready";` | `pdf.ProcessingState = nameof(PdfProcessingState.Ready);` |
| `IndexPdfCommandHandler.cs` | 148 | `failedPdf.ProcessingState = "Failed";` | `failedPdf.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `PurgeStaleDocumentsCommandHandler.cs` | 41 | `doc.ProcessingState = "Failed";` | `doc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `UploadPdfCommandHandler.Processing.cs` | 170 | `pdfDoc.ProcessingState = "Uploading";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Uploading);` |
| `UploadPdfCommandHandler.Processing.cs` | 214 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `UploadPdfCommandHandler.Processing.cs` | 462 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `UploadPdfCommandHandler.Processing.cs` | 704 | `pdfDoc.ProcessingState = "Ready";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Ready);` |
| `UploadPdfCommandHandler.Processing.cs` | 752 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |
| `UploadPdfCommandHandler.Processing.cs` | 786 | `pdfDoc.ProcessingState = "Failed";` | `pdfDoc.ProcessingState = nameof(PdfProcessingState.Failed);` |

> **Robust strategy**: For each file, use `Read` to confirm current line numbers (may shift slightly after rebase), then `Edit` (or `MultiEdit`) with the exact `Current` text from the table. The `Current` text is unique enough within each file that line-number drift won't cause false matches.

- [ ] **Step 1: Verify enum namespace is imported in each file**

For each of the 7 files above, check the imports:

```
grep -l "using Api.BoundedContexts.DocumentProcessing.Domain.Enums;" \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/VectorDocumentReadyStateHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ExtractPdfTextCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/PurgeStaleDocumentsCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs
```

Add the missing using to any file that doesn't list it. (`PdfProcessingState` lives in `Api.BoundedContexts.DocumentProcessing.Domain.Enums`.)

- [ ] **Step 2: Apply the 30 substitutions from the master table**

Apply per file (use the master substitution table above as the source of truth). Tip: process files in the order listed (top-down through the table). For each file, use `Read` first to confirm line numbers, then `Edit` (1 call per substitution OR `MultiEdit` for the bulk).

- [ ] **Step 3: Build to verify zero errors**

```bash
dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
```

Expected: `Build succeeded. 0 Error(s).` If a build fails with `CS0103: 'PdfProcessingState' does not exist`, the missing using directive is the cause — add it.

- [ ] **Step 4: Commit (one commit per file for diff clarity)**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs
git commit -m "refactor(api/document-processing): #1801 use nameof in pipeline service (7 literals)"

git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs
git commit -m "refactor(api/document-processing): #1801 use nameof in chunked upload handler (5 literals)"

git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/VectorDocumentReadyStateHandler.cs
git commit -m "refactor(api/document-processing): #1801 use nameof in vector ready state handler (3 literals)"

git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ExtractPdfTextCommandHandler.cs
git commit -m "refactor(api/document-processing): #1801 use nameof in extract text handler (3 literals)"

git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs
git commit -m "refactor(api/document-processing): #1801 use nameof in index pdf handler (5 literals)"

git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/PurgeStaleDocumentsCommandHandler.cs
git commit -m "refactor(api/document-processing): #1801 use nameof in purge stale handler (1 literal)"

git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs
git commit -m "refactor(api/document-processing): #1801 use nameof in upload processing helper (6 literals)"
```

(Skip any commit that has no staged changes — if a file ended up identical after Edit, drop the commit.)

---

### Task 3: Refactor `ReindexDocumentCommandHandler.cs` (1 literal + InFlightStates derive)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs`

- [ ] **Step 1: Read current implementation**

The current code (introduced by #1673) is:

```csharp
private static readonly HashSet<string> InFlightStates =
    new(StringComparer.Ordinal)
    {
        "Pending",
        "Uploading",
        "Extracting",
        "Chunking",
        "Embedding",
        "Indexing",
    };
```

(Lines ~22-30 — confirm via Read before editing.)

- [ ] **Step 2: Add `using` if needed**

```
grep -n "using Api.BoundedContexts.DocumentProcessing.Domain.Enums" apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs
```

Add if missing.

- [ ] **Step 3: Replace the HashSet with a derived set**

Substitute the entire `InFlightStates` declaration with:

```csharp
// In-flight states = all enum values EXCEPT the two terminal states (Ready, Failed).
// Derived from Enum.GetNames so adding a new pre-terminal state to PdfProcessingState
// (or renaming an existing one) automatically updates this set — no manual sync needed.
private static readonly HashSet<string> InFlightStates =
    new(
        Enum.GetNames<PdfProcessingState>()
            .Except(
                new[]
                {
                    nameof(PdfProcessingState.Ready),
                    nameof(PdfProcessingState.Failed),
                },
                StringComparer.Ordinal),
        StringComparer.Ordinal);
```

This guarantees: when a new enum value is added (e.g. `PdfProcessingState.Validating`), `InFlightStates` includes it automatically without the developer remembering to update the HashSet — the exact pattern bug we're refactoring away.

- [ ] **Step 4: Replace literal at line ~83**

At line ~83 (confirm via Read) the handler resets state to `"Pending"` after the conflict guard:

```csharp
pdf.ProcessingState = "Pending";
```

Replace with:

```csharp
pdf.ProcessingState = nameof(PdfProcessingState.Pending);
```

This is the single remaining raw literal in this file outside the (now-derived) HashSet.

- [ ] **Step 5: Build to verify zero errors**

```bash
dotnet build apps/api/src/Api/Api.csproj 2>&1 | tail -3
```

- [ ] **Step 6: Run reindex handler tests to verify zero semantic regression**

```bash
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~ReindexDocumentCommandHandlerTests" --no-build 2>&1 | tail -10
```

Expected: **13 passed**. The 6 in-flight states + 2 terminal states + 5 facts must all behave identically. If even one test fails, the derivation logic has a bug (e.g. `Enum.GetNames` returning unexpected casing).

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs
git commit -m "refactor(api/document-processing): #1801 derive InFlightStates + use nameof in reindex handler"
```

---

### Task 4: Final verification + PR

**Files:**
- (no edit)

- [ ] **Step 1: Final grep — confirm no raw literals remain in the 8 in-scope files**

```bash
grep -nE '"(Pending|Uploading|Extracting|Chunking|Embedding|Indexing|Ready|Failed)"' \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Services/PdfProcessingPipelineService.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/CompleteChunkedUploadCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/VectorDocumentReadyStateHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ExtractPdfTextCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/PurgeStaleDocumentsCommandHandler.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/UploadPdfCommandHandler.Processing.cs \
  apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/ReindexDocumentCommandHandler.cs
```

Also run the BC-wide check to confirm no other ProcessingState assignments slipped through:

```bash
grep -nrE 'ProcessingState [=]=? "(Pending|Uploading|Extracting|Chunking|Embedding|Indexing|Ready|Failed)"' \
  apps/api/src/Api/BoundedContexts/DocumentProcessing 2>&1 | grep -v '\.Designer\.cs'
```

Expected: **no matches** (except log message templates and exception strings that legitimately contain these words in a non-state context — review each match and confirm).

If any match remains in a `ProcessingState = "..."` or `ProcessingState == "..."` context, this is an oversight: replace and re-commit.

- [ ] **Step 2: Run all DocumentProcessing unit tests for regression**

```bash
cd apps/api/src/Api && dotnet test ../../tests/Api.Tests/Api.Tests.csproj --filter "BoundedContext=DocumentProcessing&Category=Unit" --no-build 2>&1 | tail -10
```

Expected: all previously-passing tests still pass (baseline was ~336 unit tests; should remain the same count — refactor adds no tests, removes no tests).

- [ ] **Step 3: Rebase + push**

```bash
git fetch origin main-dev
git rebase origin/main-dev
# Expect clean rebase — no expected conflicts since only 4 files touched
git push -u origin feature/issue-1801-nameof-refactor
```

If the rebase touches conflicting territory (e.g. someone else just changed one of these 4 files), resolve and re-run Step 2.

- [ ] **Step 4: Open PR against `main-dev`**

```bash
gh pr create --base main-dev --title "refactor(api/document-processing): #1801 unify nameof(PdfProcessingState) usage" --body "$(cat <<'EOF'
## Summary

Closes #1801. Sostituisce 12 raw string literals + 1 HashSet hardcoded con riferimenti `nameof(PdfProcessingState.X)` o `Enum.GetNames<PdfProcessingState>()` derivation, per garantire rename-safety end-to-end nel BC \`DocumentProcessing\`.

## Changes

| File | Δ |
|------|---|
| \`PdfProcessingPipelineService.cs\` | 7 literals → \`nameof()\` |
| \`CompleteChunkedUploadCommandHandler.cs\` | 2 literals → \`nameof()\` |
| \`VectorDocumentReadyStateHandler.cs\` | 3 literals → \`nameof()\` |
| \`ReindexDocumentCommandHandler.cs\` | \`InFlightStates\` derived from \`Enum.GetNames<PdfProcessingState>()\` excluding \`Ready\`/\`Failed\` |

## Test plan

- [x] Build clean (0 errors, 0 new warnings)
- [x] \`dotnet test --filter \"BoundedContext=DocumentProcessing&Category=Unit\"\` all green
- [x] \`ReindexDocumentCommandHandlerTests\` 13/13 (covers all 6 in-flight + 2 terminal states)
- [x] No wire-format change, no DTO change, no migration

## Notes

- **Semantica wire invariata**: gli stati continuano a essere serializzati come stringhe PascalCase identiche ai literals attuali. Nessun impatto su client/DB.
- **Cross-cutting**: file fuori scope (\`StalePdfRecoveryService\`, \`PdfSeeder\`, \`UserLibrary\`, \`RetryFailedPdfsJob\`) usano già \`nameof()\` — pattern unificato post-merge.
- **Pattern win**: aggiungere un nuovo \`PdfProcessingState.X\` ora auto-include lo stato in \`InFlightStates\` se non è \`Ready\`/\`Failed\` — niente manuale sync da ricordare.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Comment on issue #1801**

```bash
gh issue comment 1801 --body "PR aperto contro \`main-dev\` (link sopra).

## DoD updates
- [x] 31 raw string literals sostituiti con \`nameof(PdfProcessingState.X)\` negli 8 file in scope.
- [x] \`InFlightStates\` HashSet in \`ReindexDocumentCommandHandler\` ora derivata da \`Enum.GetNames<PdfProcessingState>()\`.
- [x] Test esistenti continuano a passare senza modifiche.
- [x] Nessuna allocazione runtime addizionale (HashSet costruita una volta in \`static readonly\`).

Ready for code review."
```

---

## Self-Review

### Spec coverage check

| Issue #1801 requirement | Task |
|-----------------------------|------|
| Raw string literals in 7 product files → `nameof()` (30 substitutions) | Task 2 |
| `ReindexDocumentCommandHandler` literal at line 83 → `nameof()` | Task 3 step 4 |
| `InFlightStates` derived from `Enum.GetNames` (no manual sync) | Task 3 steps 1-3 |
| Test esistenti pass | Task 3 step 6 + Task 4 step 2 |
| Nessuna nuova allocazione runtime | Task 3 — `static readonly` mantenuto |
| Final grep verifies no residual literals | Task 4 step 1 |

### Placeholder scan

- [x] No "TBD" / "TODO" / "implement later" — ogni sostituzione ha la replacement esatta.
- [x] No "similar to Task N" senza codice esplicito.
- [x] Ogni step ha o un comando esatto o una tabella di sostituzioni puntuali.

### Type consistency

| Symbol | Defined | Used |
|--------|---------|------|
| `PdfProcessingState` (enum) | Already exists at `Domain/Enums/PdfProcessingState.cs` | Tasks 2, 3, 4, 5 |
| `nameof(PdfProcessingState.Pending)` etc. | Compile-time string from enum | Tasks 2-5 |
| `Enum.GetNames<PdfProcessingState>()` | BCL API .NET 5+ | Task 5 |

Nessuna nuova firma o tipo introdotto. Refactor puro.

---

## References

- Issue: [#1801](https://github.com/meepleAi-app/meepleai-monorepo/issues/1801)
- Parent context: PR #1800 (#1673) — code review carry-forward
- Sibling pattern (canonical `nameof()` usage): `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Seeding/PdfSeeder.cs:194`
- Enum source: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Enums/PdfProcessingState.cs`
