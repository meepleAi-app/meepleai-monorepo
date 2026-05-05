# 🚧 External Blockers — Aaron Track

> Human/external dependencies che bloccano specifiche task. Aggiornare quando risolti.

## 🔴 Critical path blockers

### B-1 — Procurement 5 manuali gamebook reali (Task 0.1 step 1)

**Owner**: Aaron
**Effort**: €50-300 spesa fisica + 1-3 weeks shipping
**Blocked tasks**: 0.1 step 2-7 (validation cascade)
**Status**: ⚪ Not started
**Criteria**: Acquistare versioni LEGALI di:
1. Tainted Grail (English) — narrative-heavy, layout artistico
2. ISS Vanguard (English) — sci-fi, illustrazioni piene pagina
3. Stuffed Fables (English) — family, illustrations + text
4. Andor Chronicles (German) — chapter-based, no §
5. 7th Continent (French) — atypical layout

**Action item**: Aaron crea ordine entro 2026-05-11 per arrivo entro 2026-05-25 (mid-Sprint 0).

### B-2 — Legal advisor identification (Task 0.3 PR-1)

**Owner**: Aaron
**Effort**: €3000-€8000 + 2-3 weeks calendar
**Blocked tasks**: 0.3 step 1-4 → blocca pre-launch checklist
**Status**: ⚪ Not started
**Criteria**: 
- Studio legale italiano con divisione IP (BonelliErede, DLA Piper Italy) o freelance Linkedin
- Esperienza SaaS user-generated content
- Familiarità EU Copyright Directive 2019/790 + DMCA US
- Idealmente background gaming/entertainment

**Action item**: Aaron invia 3 RFP entro 2026-05-18, signs contract entro 2026-05-25.

### B-3 — Contractor IT native speaker per golden test set (Task 0.2 PR-3)

**Owner**: Aaron
**Effort**: ~$3000 + 4 weeks lead time, 2 weeks effective work
**Blocked tasks**: 0.2 step 1, 4-7 → blocca hallucination CI gate (Task 2.7) → blocca Phase 2 acceptance
**Status**: ⚪ Not started
**Criteria**:
- IT native speaker
- Background board game (BGG profile, gamebook player attestato)
- Esperienza traduzione/copyediting (literary)
- Disponibile 80h totali (~$30-40/h)

**Sources**: Upwork / Fiverr Pro / italian gaming communities

**Action item**: Aaron pubblica brief entro 2026-05-11, identifica contractor entro 2026-05-25, kickoff entro 2026-06-01.

## 🟡 Infrastructure blockers

### B-4 — Hetzner Cloud account + SSH key + Storage Box BX11 (Task 0.4 step 1)

**Owner**: Aaron
**Effort**: 1 hour setup + €14/mese (CAX31) + €4.65/mese (Storage Box) ongoing
**Blocked tasks**: 0.4 step 1 (provisioning effettivo) — Sprint 0 può preparare configs/scripts ma non deploy
**Status**: ⚪ Not started

**Action item**: Aaron crea account entro 2026-05-11, condivide SSH key con dev team.

### B-5 — OpenRouter account + $50 starter credit (Task 0.5)

**Owner**: Aaron
**Effort**: 30 min setup + $50 ongoing
**Blocked tasks**: 0.5 step 1-4 + Task 2.1 (TranslationService integration) + Task 2.4 (router)
**Status**: ⚪ Not started

**Action item**: Aaron crea account entro 2026-05-11, share API key in `infra/secrets/openrouter.secret`.

### B-6 — Stripe account + webhook config (Task 0.6)

**Owner**: Aaron
**Effort**: 1 hour setup + KYC review (1-3 days Stripe-side)
**Blocked tasks**: 0.6 step 1 + Task 3.3a-c (pricing integration) + Task 3.9 (payment flow E2E)
**Status**: ⚪ Not started

**Action item**: Aaron crea account entro 2026-05-18, webhook endpoint configurato post-deploy CAX31.

### B-7 — Computer Vision Engineer per Task 0.1 (OCR validation)

**Owner**: Aaron
**Effort**: 2 weeks dedicati + recruiting time
**Blocked tasks**: 0.1 step 5-7 (validation execution) — può anche essere internalizzato a ML engineer esistente con OpenCV expertise
**Status**: ⚪ Not started
**Criteria**:
- OpenCV expertise (NON generic ML eng)
- Esperienza dewarping + page detection + multi-shot stitching

**Action item**: Aaron decide entro 2026-05-11 se hire contractor o assegnare a ML engineer interno.

## ✅ Resolved blockers

(Vuoto — nessun blocker risolto ancora)

## Checklist riepilogativa

| ID | Blocker | Owner | Deadline | Status |
|----|---------|-------|----------|--------|
| B-1 | Procurare 5 manuali fisici | Aaron | 2026-05-11 (order), 2026-05-25 (arrival) | ⚪ |
| B-2 | Legal advisor | Aaron | 2026-05-25 (contract) | ⚪ |
| B-3 | IT native speaker contractor | Aaron | 2026-06-01 (kickoff) | ⚪ |
| B-4 | Hetzner account | Aaron | 2026-05-11 | ⚪ |
| B-5 | OpenRouter account | Aaron | 2026-05-11 | ⚪ |
| B-6 | Stripe account | Aaron | 2026-05-18 | ⚪ |
| B-7 | Computer Vision Engineer assignment | Aaron | 2026-05-11 (decision) | ⚪ |

---

**Ultima modifica**: 2026-05-04 (kickoff Sprint 0)
