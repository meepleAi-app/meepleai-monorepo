# Mechanic Extractor — Claims Viewer & Per-Claim Review (M1.2 follow-up)

**Status**: Spec approvata (default panel review accettato)
**Owner**: TBD
**Tracker**: GitHub issue (TBD)
**Related**:
- ADR-051 (Mechanic Extractor IP policy) — `docs/architecture/adr/adr-051-mechanic-extractor-ip-policy.md`
- PR #547 (M1.2 admin UI baseline)
- Sprint 2 plan — `claudedocs/` (mechanic-validation-sprint-2)

---

## 1. Contesto e problema

L'admin UI introdotta in PR #547 (`/admin/knowledge-base/mechanic-extractor/analyses`) mostra status, section runs, telemetria (token, costo, modello) e azioni di lifecycle dell'analysis (Generate, Submit, Approve, Suppress) ma **non espone il testo delle claim né le citazioni**.

**Conseguenze osservate** (run 7W `da974ef5-0cf6-49e2-91e7-12c94f0b870c`, 35 claim, status=InReview, 2026-04-27):
- Validazione del contenuto possibile solo via `psql` diretto sul DB.
- Lifecycle `InReview → Published` (AC-10 ADR-051: tutte le claim Approved) **non raggiungibile da UI**: manca il per-claim Approve/Reject.
- Audit trail per-claim assente: `MechanicClaim.ResetToPending()` cancella `RejectionNote`.

## 2. Decisioni di scope (panel default approvato)

| ID | Decisione | Scelta |
|----|-----------|--------|
| D1 | Scope MVP | **Lettura + Approve/Reject per-claim** |
| D2 | Edit-in-place del testo claim | **Backlog** (richiede policy citation re-binding) |
| D3 | `mechanic_claim_status_audit` table | **In MVP** |
| D4 | Bulk Approve-all-pending | **Gated** dietro checkbox "Ho letto tutte le claim della sezione" + flag `bulk=true` nell'audit |
| D5 | Workflow su claim rejected | **Manuale** (admin re-runs intera analysis) — backlog `regenerate-section` |

**Esplicitamente fuori scope**:
- Edit-in-place del testo claim
- PDF inline preview (anchor a page) — backlog separato
- Regenerate-section endpoint (re-run parziale post-reject)
- Telemetria "claim viewed before approved"

## 3. Acceptance Criteria

### 3.1 Lettura
- **AC-CV-1** L'endpoint `GET /admin/mechanic-analyses/{id}/claims` ritorna le claim raggruppate per `MechanicSection` (0..5) con citazioni (`pdfPage`, `quote`, `displayOrder`) e per-section stats (`total`, `pending`, `approved`, `rejected`).
- **AC-CV-2** L'endpoint ritorna anche `overallStats: { total, pending, approved, rejected, allApproved: bool, readyForPublish: bool }`.
- **AC-CV-3** L'endpoint richiede ruolo Admin (`RequireAdminSession`); ritorna 401 senza sessione, 403 con ruolo insufficiente, 404 se l'analysis non esiste o è suppressed (rispetta global query filter).
- **AC-CV-4** L'UI espone una sezione `Claims (N · ✅X · ⏳Y · ❌Z)` collapsible nella card analisi; il fetch è lazy (solo on-expand).
- **AC-CV-5** L'UI rende un accordion con le 6 sezioni nell'ordine canonico: Summary, Mechanics, Victory, Resources, Phases, FAQ.
- **AC-CV-6** Per ogni claim l'UI rende: `displayOrder`, `text`, status badge (Pending/Approved/Rejected), citation chips (`p.{N}: "..."` troncato a 80 char con expand on click).
- **AC-CV-7** Filtro globale `All / Pending / Approved / Rejected` agisce sulle 6 sezioni in maniera coerente.

### 3.2 Mutations
- **AC-CV-8** `POST /admin/mechanic-analyses/{id}/claims/{claimId}/approve` esegue `MechanicClaim.Approve(actorId, utcNow)` e scrive una riga in `mechanic_claim_status_audit` nella **stessa transazione**.
- **AC-CV-9** `POST .../claims/{claimId}/reject` accetta `{ note: string }` con `1..500` char (validato client-side e server-side); esegue `Reject(actorId, note, utcNow)` + audit.
- **AC-CV-10** Le mutations sono ammesse solo se `analysis.Status == InReview`. Altri stati ritornano 409 Conflict con messaggio explicito.
- **AC-CV-11** Le mutations sono **idempotenti**: re-approving una claim Approved è no-op (200 OK con stato corrente, **nessuna** riga audit duplicata).
- **AC-CV-12** Concorrenza: `MechanicClaim` espone un concurrency token (`xmin` PostgreSQL). Mutation con token stale ritorna **409 Conflict**; UI mostra "Già modificata da X, ricarica".
- **AC-CV-13** I bottoni Approve/Reject sono visibili **solo** se `analysis.Status == InReview`; in altri stati l'UI rende solo il badge.

### 3.3 Bulk
- **AC-CV-14** Il bottone "Approve all pending in section" è **disabled** finché l'admin non spunta la checkbox "Ho letto tutte le claim di questa sezione".
- **AC-CV-15** L'azione bulk apre una conferma con riepilogo (`N claim verranno marcate Approved`) e on confirm esegue mutations in una **singola transazione** scrivendo N righe in `mechanic_claim_status_audit` con `bulk=true`.
- **AC-CV-16** Bulk fallisce atomically: se anche una sola mutation 409, l'intera operazione viene rollback e l'UI mostra le claim in conflict per re-fetch.

### 3.4 Audit & lifecycle
- **AC-CV-17** Nuova tabella `mechanic_claim_status_audit` (append-only): `id`, `claim_id` (FK → mechanic_claims, ON DELETE CASCADE), `from_status`, `to_status`, `actor_id`, `note?` (max 500), `bulk` (bool, default false), `occurred_at` (UTC). CHECK constraint su `from_status BETWEEN 0 AND 2 AND to_status BETWEEN 0 AND 2 AND from_status <> to_status`.
- **AC-CV-18** Index: `ix_mechanic_claim_status_audit_claim_id`, `ix_mechanic_claim_status_audit_claim_time` (`claim_id, occurred_at`).
- **AC-CV-19** Il bottone "Promote to Published" della analysis è **disabled** finché `overallStats.allApproved == true`. La condizione è calcolata server-side.

### 3.5 Stati vuoti, errore, accessibilità
- **AC-CV-20** Stato vuoto: analysis con 0 claim (es. PartiallyExtracted con failure) rende messaggio esplicativo e link al log della section run.
- **AC-CV-21** Stato di errore: fetch fallito → banner con retry; mutation fallita → toast con motivo (validation, conflict 409, server 5xx differenziati).
- **AC-CV-22** Accessibilità: keyboard navigation completa (Tab/Shift+Tab tra claim, Enter su bottoni, Escape chiude reject dialog); ogni section header annunciato da screen reader come `region` con count corrente.
- **AC-CV-23** Sanitizzazione: il `text` della claim viene renderizzato come testo React (interpolazione standard, mai HTML raw); markdown/HTML accidentale appare letterale, mitigando XSS.

## 4. Architettura proposta

### 4.1 Backend

**Domain** (`Api.BoundedContexts.SharedGameCatalog.Domain`):
- `MechanicClaim` esistente è già adatto. Aggiungere `RowVersion` o esporre `Xmin` come concurrency token (vedi `MechanicAnalysisEntityConfiguration.Xmin` per il pattern).
- Nuovo aggregate event: `MechanicClaimReviewed { ClaimId, FromStatus, ToStatus, ActorId, Note?, OccurredAt, Bulk }` per scrivere audit nello stesso `SaveChangesAsync()`.

**Entity nuova** (`Api.Infrastructure.Entities.SharedGameCatalog`):
- `MechanicClaimStatusAuditEntity` con i campi di AC-CV-17.

**EF configuration** (`Api.Infrastructure.Configurations.SharedGameCatalog`):
- `MechanicClaimStatusAuditEntityConfiguration` (mirror di `MechanicStatusAuditEntityConfiguration`).
- Migration: `AddMechanicClaimStatusAudit`.

**Application** (commands/queries):
- `GetMechanicAnalysisClaimsQuery(Guid analysisId) : Task<MechanicAnalysisClaimsDto>` — read.
- `ApproveMechanicClaimCommand(Guid analysisId, Guid claimId)` + Handler + Validator.
- `RejectMechanicClaimCommand(Guid analysisId, Guid claimId, string note)` + Handler + Validator (FluentValidation: note 1..500).
- `BulkApproveMechanicClaimsCommand(Guid analysisId, MechanicSection section)` + Handler.

**Routing** (`Api/Routing/AdminMechanicAnalysesEndpoints.cs`):
```csharp
group.MapGet("/{id:guid}/claims", async (Guid id, IMediator m) =>
    Results.Ok(await m.Send(new GetMechanicAnalysisClaimsQuery(id))));
group.MapPost("/{id:guid}/claims/{claimId:guid}/approve", ...);
group.MapPost("/{id:guid}/claims/{claimId:guid}/reject", ...);
group.MapPost("/{id:guid}/claims/bulk-approve", ...);
```

**DTO** (`MechanicAnalysisClaimsDto`):
```jsonc
{
  "analysisId": "...",
  "analysisStatus": 1,                 // InReview
  "sections": [
    {
      "section": 1,
      "sectionName": "Mechanics",
      "stats": { "total": 8, "pending": 8, "approved": 0, "rejected": 0 },
      "claims": [
        {
          "id": "...",
          "displayOrder": 0,
          "text": "Hand Management: ...",
          "status": 0,                  // Pending
          "reviewedBy": null,
          "reviewedAt": null,
          "rejectionNote": null,
          "rowVersion": "Xmin-token",
          "citations": [
            { "displayOrder": 0, "pdfPage": 6, "quote": "Ogni Epoca è composta..." }
          ]
        }
      ]
    }
  ],
  "overallStats": {
    "total": 35, "pending": 35, "approved": 0, "rejected": 0,
    "allApproved": false, "readyForPublish": false
  }
}
```

### 4.2 Frontend

**Schema** (`apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts`):
- `MechanicClaimSchema`, `MechanicCitationSchema`, `MechanicAnalysisClaimsResponseSchema` (zod).

**Client** (`adminContentClient.ts`):
- `getMechanicAnalysisClaims(analysisId): Promise<MechanicAnalysisClaimsResponse>`
- `approveMechanicClaim(analysisId, claimId, rowVersion): Promise<...>`
- `rejectMechanicClaim(analysisId, claimId, { note, rowVersion }): Promise<...>`
- `bulkApproveMechanicClaimsInSection(analysisId, section): Promise<...>`

**UI** (`apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/analyses/page.tsx`):
- Nuovo componente `<ClaimsSection analysisId status />` (lazy fetch on expand).
- Sub-componenti: `<ClaimsAccordion>`, `<ClaimRow>`, `<CitationChip>`, `<RejectClaimDialog>`, `<BulkApproveBar>`.
- React Query keys:
  - `['mechanic-analysis-claims', analysisId]` per il GET
  - Mutation `onSuccess` invalida sia `['mechanic-analysis-status', analysisId]` che `['mechanic-analysis-claims', analysisId]`

### 4.3 Sequence diagram (per-claim Approve)

```
Admin UI                         API                       DB
   |                              |                         |
   |-- POST .../claims/X/approve  |                         |
   |   { rowVersion }             |                         |
   |                              |-- BEGIN TX              |
   |                              |-- UPDATE mechanic_claims SET status=1, ... WHERE id=X AND xmin=$rv |
   |                              |   (0 rows affected? -> 409)
   |                              |-- INSERT mechanic_claim_status_audit (...)
   |                              |-- COMMIT
   |<- 200 { newStatus, overall } |                         |
   |  invalidate query keys       |                         |
   |  re-fetch status + claims    |                         |
```

## 5. Testing strategy

### 5.1 Backend unit
- `MechanicClaim.Approve()` invariants (status=Approved, ReviewedBy/At settati, RejectionNote=null)
- `MechanicClaim.Reject()` invariants (note required, trim, status=Rejected)
- `MechanicClaim.ResetToPending()` invariants (resetta tutto)
- Validator: `RejectMechanicClaimCommandValidator` accetta 1..500 char
- Validator: `BulkApproveMechanicClaimsCommandValidator` rifiuta se analysis non InReview

### 5.2 Backend integration (Testcontainers)
- GET claims grouped: 6 sezioni, citations populate
- GET su analysis suppressed → 404 (global filter)
- Approve mutation: claim status flippa, audit row scritta nella stessa TX
- Reject mutation: note salvata + audit
- Concurrency: due request simultanee con stesso rowVersion → una vince (200), l'altra perde (409)
- Bulk approve atomicity: se la 5ª claim su 8 fallisce per stale token, le prime 4 sono rollback
- Idempotency: doppio Approve → 1 sola riga audit

### 5.3 E2E Playwright
- Full flow: 7W (35 claim) → admin reviewer → Approve all section by section → "Promote to Published" abilitato → click → status=Published
- Reject flow: rejecta claim, vede badge rosso + note tooltip, "Promote" resta disabled
- Bulk: checkbox + bulk-approve sezione Mechanics (8 claim) in 1 click → 8 audit rows
- A11y: axe-core su `/analyses` con claims expansa, score ≥ AA

### 5.4 Performance
- GET claims su analysis con 35 claim + 60 citazioni: P95 < 300ms in dev
- Single SQL query con `Include(c => c.Citations)` — verificare con EF Core query log

## 6. Out of scope (backlog)

| Item | Rationale |
|------|-----------|
| **Edit-in-place del testo claim** | Richiede policy per ricalcolo citation binding (text rephrased → citation ancora valida?). Mini-progetto separato. |
| **PDF inline preview** con anchor a `pdf_page` | Nessun PDF viewer in admin oggi. Spec separata `mechanic-extractor-pdf-side-preview`. |
| **Regenerate-section endpoint** per re-run parziale post-reject | Pipeline oggi non supporta partial regeneration; richiede nuovo command + cost cap dedicato. |
| **Telemetria "claim viewed before approved"** | Anti-bulk-approve weak-audit. Se la review-velocity diventa un problema misurabile, riapri. |
| **Permessi non-admin (read-only reviewer)** | Oggi mechanic-extractor è admin-only. Estensione richiede nuovo ruolo `ContentReviewer`. |

## 7. Migration plan

1. **Migration** `AddMechanicClaimStatusAudit`:
   ```sql
   CREATE TABLE mechanic_claim_status_audit (
     id uuid PRIMARY KEY,
     claim_id uuid NOT NULL REFERENCES mechanic_claims(id) ON DELETE CASCADE,
     from_status int NOT NULL,
     to_status int NOT NULL,
     actor_id uuid NOT NULL,
     note varchar(500),
     bulk boolean NOT NULL DEFAULT false,
     occurred_at timestamptz NOT NULL,
     CONSTRAINT ck_mechanic_claim_status_audit_status_range CHECK (
       from_status BETWEEN 0 AND 2 AND to_status BETWEEN 0 AND 2
     ),
     CONSTRAINT ck_mechanic_claim_status_audit_distinct_states CHECK (from_status <> to_status)
   );
   CREATE INDEX ix_mechanic_claim_status_audit_claim_id ON mechanic_claim_status_audit(claim_id);
   CREATE INDEX ix_mechanic_claim_status_audit_claim_time ON mechanic_claim_status_audit(claim_id, occurred_at);
   ```
2. Nessun backfill necessario (analisi esistenti restano in InReview, audit inizia da prossima review action).
3. Deploy backend prima del frontend (compatibilità: nuovi endpoint sono additive).

## 8. Open risks

- **Concorrenza optimistic vs pessimistic**: scelta optimistic + xmin. Verificare che `xmin` sia esposto correttamente da EF Core su `MechanicClaim` (oggi è solo su `MechanicAnalysis`).
- **Bulk audit volume**: bulk-approve di 35 claim = 35 righe audit in 1 commit. Trascurabile per ora; se il volume cresce (>1000 claim per analysis) considerare batch insert.
- **Section enum stability**: aggiungere una nuova `MechanicSection` (es. Setup) richiederà aggiornare l'ordine di rendering UI + i name displayName. Documentare in commento sulla enum.

## 9. Definition of Done

- [ ] Migration applicata in dev, staging, prod
- [ ] Tutti gli AC-CV-* coperti da test (unit + integration + E2E)
- [ ] axe-core ≥ AA su `/analyses` con claims expansi
- [ ] Documentazione admin aggiornata (`docs/operations/admin-runbook.md` se esiste, o nuova sezione)
- [ ] Code review approved (PR target: parent branch del feature)
- [ ] Run di smoke test: validate 7W analysis → Approve all → Publish
- [ ] PR mergiata, branch eliminato locale + remote, issue chiusa
