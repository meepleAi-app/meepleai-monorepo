---
title: Libro Game Nanolith Iter 1 — Cross-Branch Audit
status: draft
type: audit
date: 2026-05-08
authors: [DegrassiAaron]
related-specs:
  - 2026-05-07-libro-game-nanolith-demo-design.md
related-plans:
  - 2026-05-07-libro-game-nanolith-iter1-plan.md
  - 2026-05-07-libro-game-nanolith-iter-1a.md
  - 2026-05-07-libro-game-nanolith-iter-1b.md
related-prs:
  - meepleAi-app/meepleai-monorepo#829
  - meepleAi-app/meepleai-monorepo#830
---

# Libro Game Nanolith Iter 1 — Cross-Branch Audit

> **Scope**: documenta la sovrapposizione tra due tracciati implementativi paralleli che coprono lo stesso vision Iter 1 con decomposizioni differenti. Identifica gap residui, divergenze architetturali, e follow-up post-merge.
>
> **Decision-ready**: produce una checklist operativa di cosa rimane da fare dopo che entrambi i tracciati shippano in `main-dev`.

## 0. Scenario

Due tracciati di lavoro hanno decomposto lo stesso vision Iter 1 (spec `2026-05-07-libro-game-nanolith-demo-design.md`) in modi differenti:

| Tracciato | Branch | Plan files | Status |
|-----------|--------|------------|--------|
| **Tracciato A** (centralizzato) | `feature/nanolith-iter1-plan-extension` | `2026-05-07-libro-game-nanolith-iter1-plan.md` (1 plan, 3008 righe) + Sprint -1 + Phase 0 + Phase 1.A.9 estensioni | PR #829 + #830 open |
| **Tracciato B** (split per iterazione) | `feature/libro-game-iter-1a` | `2026-05-07-libro-game-nanolith-iter-1a.md` (1966 righe) + `2026-05-07-libro-game-nanolith-iter-1b.md` (1173 righe) | iter-1a 12 commit pushati; iter-1b in progress locale (Tasks 6-9) |

I due tracciati condividono lo spec design (`2026-05-07-libro-game-nanolith-demo-design.md`) come fonte di verità ma scompongono il lavoro implementativo diversamente.

## 1. Mapping decomposizioni

### Naming reconciliation

| Vision spec | Tracciato A | Tracciato B |
|-------------|-------------|-------------|
| N3 photo translate-on-the-fly | **Iter 1.A** | **iter-1b** (Tasks 1-22) |
| N4 resume cross-day + glossary | **Iter 1.B** | **iter-1a** (Tasks 1-19) |

⚠️ **Inversione semantica**: ciò che Tracciato A chiama "Iter 1.A" è "iter-1b" per Tracciato B, e viceversa. Quando si discute di "Iter 1.A" il riferimento al plan d'origine è obbligatorio.

### Coverage incrociata

| Componente | Tracciato A plan | Tracciato B plan | Status reale |
|------------|------------------|------------------|--------------|
| `GamebookCampaignSession` aggregate | Task 1.B.1.2 | iter-1a Task 2 | ✅ shipped (12 commit Tracciato B) |
| `GamebookProgress` VO history | _non previsto_ | iter-1a Task 1 | ✅ shipped (Tracciato B design choice) |
| `ParagraphId` VO | Task 1.A.1.2 | _non previsto_ | ❌ non implementato (Tracciato B usa `GamebookProgress` history invece) |
| `SegmentationMatchQuality` enum | Task 1.A.1.3 | _implicito in Segment_ | 🟡 non chiaro (Tracciato B Task 1 iter-1b lo includerà?) |
| EF Configuration + migration | Phase 1.B.2 | iter-1a Tasks 3-5 | ✅ shipped (schema `session_tracking`, jsonb progress) |
| Repository interface + impl | Phase 1.B.2 | iter-1a Tasks 3-4 | ✅ shipped |
| `CreateGamebookCampaign` cmd | Phase 1.B.3 | iter-1a Task 6 | ✅ shipped |
| `UpdateGamebookProgress` cmd | (`MarkParagraphRead` Task 1.B.3.5) | iter-1a Task 7 | ✅ shipped (Tracciato B con history vs Tracciato A senza) |
| `Get`/`ListMy` queries | Phase 1.B.3 | iter-1a Task 8 | ✅ shipped |
| REST `/gamebook/campaigns` | Phase 1.B.4 (parziale) | iter-1a Task 9 | ✅ shipped (180 righe) |
| Frontend `GamebookPlayShell` | Task 1.B.4.4 | iter-1a Task 13 | ✅ shipped |
| Frontend `NewCampaignDialog` | _non previsto_ | iter-1a Task 17 | ✅ shipped (Tracciato B addition) |
| Frontend `NanolithCampaignCTA` | _non previsto_ | iter-1a Task 17 | ✅ shipped |
| Route `/library/games/[gameId]/play/[campaignId]` | Task 1.B.4.4 (variant) | iter-1a Task 14 | ✅ shipped |
| Typed API client + 2 TanStack hooks | Task 1.B.4.1+1.B.4.2 | iter-1a Tasks 11-12 | ✅ shipped |
| 13 backend unit + 13 FE unit | Phase 1.B.6.1 (parziale) | iter-1a tests | ✅ shipped |
| E2E `@ci` smoke + `@dogfood` placeholder | Phase 1.B.6.2 | iter-1a Task 16 | ✅ shipped |
| `GamebookPhotoArtifact` aggregate | Task 1.B.1.4 | iter-1b Task 1 | 🟡 in progress (Tracciato B locale) |
| `GamebookSegment` entity | _non previsto separatamente_ | iter-1b Task 1 | 🟡 in progress |
| `TranslatedParagraph` aggregate | Task 1.B.1.3 | iter-1b Task 2 | 🟡 in progress |
| `GamebookGlossaryEntry` entity | _via `MemoryNote` AgentMemory BC_ | iter-1b Task 2 | 🟡 in progress (Tracciato B design choice: aggregate dedicato vs MemoryNote reuse) |
| `GamebookPhotoStorageService` | _implicito S3 client_ | iter-1b Task 4 | 🟡 in progress |
| `IOcrService` adapter | Task 1.A.4.2 (`SmoldoclingClientAdapter`) | iter-1b Task 5 | 🟡 in progress |
| `UploadGamebookPhotoCommand` | Task 1.A.4.1 (`IngestPhotoCommand`) | iter-1b Task 6 | 🟡 in progress |
| `SegmentGamebookPhotoCommand` | Task 1.A.4.2 (parte di `IngestPhotoHandler`) | iter-1b Task 7 | 🟡 in progress |
| `TranslateGamebookSegmentCommand` (SSE) | Task 1.A.5.1 (`TranslateParagraphCommand`) | iter-1b Task 8 | 🟡 in progress |
| `GlossaryBootstrapService` | Task 1.A.5.2 (`GlossaryExtractionPromptService`) | iter-1b Task 9 | 🟡 in progress (Tracciato B bootstrap dai KB indicizzati vs Tracciato A piggy-back LLM extraction) |
| `GamebookPhotoEndpoints` | Phase 1.A.4 + 1.A.5 (`/photo-segments` + `/photo-segments/{id}/translations`) | iter-1b Task 10 | 🟡 in progress |
| Integration test full pipeline | _non esplicito_ | iter-1b Task 11 | 🟡 in progress |
| Integration test glossary consistency | Task 1.B.6.1 (`GlossaryConsistencyIntegrationTest`) | iter-1b Task 12 | 🟡 in progress |
| FE `usePhotoUpload`/`useTranslateSegmentSSE`/`useGamebookGlossary`/`useGamebookHistory` | Tasks 1.A.6.2 + 1.B.4.1-1.B.4.3 | iter-1b Tasks 13-14 | 🟡 in progress |
| FE `TranslateViewer`/`SegmentPicker`/`TranslationPane` | Task 1.A.6.4 | iter-1b Task 15 | 🟡 in progress |
| FE `GlossaryEditor` (mockup H realization) | Task 1.B.4.5 (`GlossaryPillEditor`) | iter-1b Task 16 | 🟡 in progress (UX divergence — vedi §3) |
| FE `HistoryDrawer` | _non previsto_ | iter-1b Task 17 | 🟡 in progress (Tracciato B addition) |
| Route `/translate` | Task 1.A.6.4 (`/play/paragraph/[num]`) | iter-1b Task 18 | 🟡 in progress (URL divergence — vedi §3) |
| E2E `@ci` synthetic + `@dogfood` real | Phase 1.A.8 | iter-1b Task 19 | 🟡 in progress |
| Cleanup job expired photos | Task 1.B.5.1 (`PhotoArtifactPurgeJob`) | iter-1b Task 20 | 🟡 in progress |

## 2. Gap residui post-merge entrambi i tracciati

| ID | Gap | Severità | Provenienza |
|----|-----|----------|-------------|
| **R1** | LLM `CircuitBreaker` Polly policy (5 fail/30s → Open, 60s → Half-Open) | 🔴 critico per resilience | Tracciato A Phase 1.A.3 / spec §6.4 (Nygard fix M9) |
| **R2** | LLM `Bulkhead` Polly policy (5 concurrent / 10 queue / 16+ reject) | 🔴 critico per cost cap + protezione superadmin | Tracciato A Phase 1.A.3 |
| **R3** | 9 Prometheus metrics (`translation_*`/`ocr_*`/`glossary_*`/`llm_*`) | 🟡 observability | Tracciato A Phase 1.A.3 |
| **R4** | EXIF GPS stripping client-side (privacy) | 🔴 spec §6 require | Tracciato A Task 1.A.6.3 |
| **R5** | Resume card / Multi-campaign list / Stale warning (mockup G state-02/03/04, scenario N4.4 + N4.5) | 🟡 UX N4 incompleta | Tracciato A Task 1.B.4.4 (mockup G wired) |
| **R6** | Quota credits UI overlay (mockup E) | 🟢 superadmin bypass — minor | Tracciato A Phase 1.A.5 |
| **R7** | Phase 1.A.9 legacy routing cleanup (`/library/games/[gameId]/translate` DEMO + `/gamebook/[gameId]/play/_components/TranslationViewer` orphan) | 🟡 tech debt G13+G14 | Tracciato A Phase 1.A.9 |
| **R8** | Account/Game/PDF/Agent seed automation (`make seed-nanolith-demo`) | 🟡 dogfood friction G8-G11 | Tracciato A Sprint -1 (PR #829) |
| **R9** | SP5 KB upload mockup (brief A5b) | 🟢 design coverage G12 | Tracciato A Phase 0 (PR #830) |
| **R10** | `EntityLink` cross-aggregate references (se Tracciato B aggrega `TranslatedParagraph` + `GamebookGlossaryEntry` + `GamebookPhotoArtifact` come 3 aggregates separati, serve link consistency) | 🟢 emerge solo a integration | Tracciato B design (5 aggregates vs Tracciato A 3) |

## 3. Divergenze architetturali (no right/wrong, ma da riconciliare)

| Area | Tracciato A | Tracciato B | Impatto |
|------|-------------|-------------|---------|
| **Aggregates count** | 3 (Session root + TranslatedParagraph 1-N + PhotoArtifact transient) | 5 (Session + Progress VO history + GlossaryEntry + Segment + PhotoArtifact + TranslatedParagraph) | Più aggregati = più granularità ma più overhead concurrency |
| **Glossary storage** | Riuso `MemoryNote` in `AgentMemory` BC con tag `nanolith-glossary-{campaign_id}` | Nuovo `GamebookGlossaryEntry` aggregate dedicato | Tracciato B più type-safe ma duplica concept con `MemoryNote` |
| **DbContext** | `SessionTrackingDbContext` dedicato | `MeepleAiDbContext` condiviso (modern direct-domain-mapping) | Tracciato B coerente con `ToolkitSessionState` pattern già esistente |
| **PG schema** | default | `session_tracking` schema | Cosmetic, no impact |
| **API resource model** | `/gamebook/{gameId}/photo-segments` (gameId-scoped, photo-id resource) | `/gamebook/campaigns/{id}/photos` (campaign-scoped, sub-collection) | Tracciato B più REST-canonical (resource hierarchy) |
| **Frontend route photo translate** | `/library/games/[gameId]/play/paragraph/[num]` (sub-route under campaign) | `/translate` (route separata top-level) | Tracciato A più contesto (paragraph num in URL); Tracciato B più semplice |
| **Frontend route campaign play** | `/library/games/[gameId]/play/[campaignId]` | `/library/games/[gameId]/play/[campaignId]` | ✅ allineato |
| **Glossary UX** | `GlossaryPillEditor` modal inline su pill click (mockup H state-01-04) | `GlossaryEditor` standalone screen (Task 16) | Tracciato A più direct manipulation; Tracciato B più discoverable. Mockup H definito per pill modal (Tracciato A allineato) |
| **CTA "new campaign"** | spec uniform via `game.hasGamebook` flag | gated `gameTitle === 'Nanolith'` hardcoded (Iter 1) | Tracciato B technical debt esplicito (deferred Iter 2 per `hasGamebook` field) |
| **Resilience** | Polly Circuit Breaker + Bulkhead + 9 metrics esplicito | Non in plan | Tracciato A più production-ready |
| **EXIF privacy** | Client-side stripper esplicito | Non in plan | Tracciato A spec-compliant §6 |

## 4. Compatibility check con #818 hotfix

`feature/libro-game-iter-1a` è **forked da prima del merge #818** (auth security fixes Phase A+B+C+D, mergiato 2026-05-07). Diff vs `main-dev` mostra delete falsi di:
- `2026-05-06-auth-flow-security-fixes-plan.md` (-2855 righe)
- `2026-05-06-auth-flow-security-fixes-design.md` (-773 righe)
- `infra/secrets/bootstrap_admin_token.secret.example` (-23 righe)

**Azione richiesta prima del merge**: Tracciato B deve fare `git rebase main-dev` o `git merge main-dev` per riconciliare. Effort minor, ma necessario per CI green.

## 5. Follow-up plan post-merge

Quando entrambi `feature/libro-game-iter-1a` (iter-1a + iter-1b) merge in `main-dev`, raccomando i seguenti PR follow-up scoped a chiudere il gap a 100% del Tracciato A plan:

### PR Follow-up #1 — Resilience + Observability (R1+R2+R3)
- Branch: `feature/gamebook-resilience`
- Effort: 1-2 giorni
- Files: `Infrastructure/Resilience/LlmCircuitBreakerPolicy.cs` + `LlmBulkheadPolicy.cs` + `MeepleAiMetrics.Translation.cs` + DI wiring + 2 unit tests
- Acceptance: 5 failures consecutivi → circuit Open testato; bulkhead 5+10+16 reject testato; `/metrics` espone le 9 nuove metriche `translation_*`/`ocr_*`/`glossary_*`/`llm_*`

### PR Follow-up #2 — EXIF Privacy (R4)
- Branch: `feature/gamebook-exif-privacy`
- Effort: 4 ore
- Files: `apps/web/src/lib/gamebook/clientExifStripper.ts` + `clientExifStripper.test.ts` + integration in `usePhotoUpload`
- Acceptance: foto fixture con GPS coordinates → upload-time GPS stripped client-side, verificato unit test su JPEG fixture

### PR Follow-up #3 — Resume Card UX completo (R5)
- Branch: `feature/gamebook-resume-states`
- Effort: 1 giorno
- Files: 4 sub-component (`EmptyFirstTime`/`ResumeHero`/`MultiCampaignList`/`StaleWarningCard`) wired al mockup G + dispatch logic in `GamebookPlayShell`
- Acceptance: 4 stati renderizzati (length=0 / length=1 fresh / length=1 stale / length>=2) testati in Playwright

### PR Follow-up #4 — Phase 1.A.9 Legacy Cleanup (R7)
- Branch: `chore/gamebook-legacy-cleanup`
- Effort: 4 ore
- Files: delete `/library/games/[gameId]/translate/page.tsx` + delete `/gamebook/[gameId]/play/_components/TranslationViewer.tsx` + 307 redirect in `next.config` + JSDoc clarification in `/gamebook/page.tsx`
- Acceptance: zero referenze attive ai 2 path deprecati; CI green

**Effort totale follow-up**: ~3-4 giorni per chiudere il gap a 100% del Tracciato A plan.

## 6. Decisioni non prese / Open Questions

1. **Q-AUDIT-1**: il Tracciato B usa `GamebookGlossaryEntry` aggregate dedicato vs Tracciato A riuso `MemoryNote`. Decision: mantenere il design Tracciato B (dedicated aggregate) per type-safety, oppure migrare a `MemoryNote` per consistency? Owner: domain layer review post-merge.
2. **Q-AUDIT-2**: routing FE photo translate diverge (`/translate` vs `/play/paragraph/[num]`). Decision: la route Tracciato B (`/translate`) è meno contestuale ma più semplice. Mantenere o aggiungere sub-route con paragraph num?
3. **Q-AUDIT-3**: l'`hasGamebook` field deferred a Iter 2 — quando viene introdotto sostituisce il check `gameTitle === 'Nanolith'` hardcoded. Tracking issue?
4. **Q-AUDIT-4**: il Tracciato B aggiunge `HistoryDrawer` (timeline paragrafi tradotti) non previsto da Tracciato A. UX nice-to-have da mantenere o scope-cap a Iter 2?

## 7. Conclusioni

- **Funzionalità coperta a ~85%** dal solo Tracciato B (iter-1a shipped + iter-1b in progress) rispetto allo spec design.
- **Gap residuo ~15%** = Resilience + Observability + EXIF + Resume UX completo + Legacy cleanup.
- **Architettura divergente ma compatibile**: i 5 vs 3 aggregates non si scontrano semanticamente; richiede rebase + integration testing.
- **Tracciato A plan rimane utile come reference** per:
  - Production-ready details (Polly, metrics, EXIF) che il Tracciato B non ha previsto
  - Sprint -1 seed automation indipendente dall'implementation work
  - Phase 0 mockup SP5 admin KB upload
  - Phase 1.A.9 legacy cleanup esplicito

## 8. Riferimenti

- Spec design: `docs/superpowers/specs/2026-05-07-libro-game-nanolith-demo-design.md`
- Plan Tracciato A: `docs/superpowers/plans/2026-05-07-libro-game-nanolith-iter1-plan.md` (PR #829)
- Plan Tracciato B iter-1a: `docs/superpowers/plans/2026-05-07-libro-game-nanolith-iter-1a.md` (su `feature/libro-game-iter-1a`)
- Plan Tracciato B iter-1b: `docs/superpowers/plans/2026-05-07-libro-game-nanolith-iter-1b.md` (su `feature/libro-game-iter-1a`)
- Brief SP5 A5b: `admin-mockups/briefs/SP5-admin-tools.md` (PR #830)
- Seed automation: `infra/scripts/seed-nanolith-demo.sh` (PR #829)
