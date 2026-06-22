# US Manual Verification Protocol

> **Purpose**: verify a single user story (US) end-to-end against its canonical mockup, via visual + functional inspection. Produces a per-US verdict logged to `audits/us-verification-log.md`, committed to the active PR.
>
> **When to use**:
> - Validate mockup ↔ app coherence after a feature ships
> - Audit user-side flows before a release
> - Triage candidates for design-forward-obsolete reclassification (DS-17 Phase B style)
> - Sanity-check post-merge regressions

## Prompt template

Invoke via `/sc:verify-us US-{N}` OR paste the prompt below directly:

```
/sc:verify-us US-{N}

Verifica manualmente la User Story specificata seguendo il protocollo a 4 step.

## Pre-flight (auto)

1. Verifica che app + API siano running:
   - `curl -s http://localhost:3000` → 200
   - `curl -s http://localhost:8080/health` → 200
   - Se non running, fermati e suggerisci `make dev` da `infra/`

2. Leggi i dati canonici della US:
   - US definitions: `audits/2026-06-10-mockup-coverage-gap-report.md` § "US-perspective gap matrix"
     OR estrai da `docs/superpowers/specs/` cercando per US-{N} / persona / route
   - Mockup mapping: `admin-mockups/MOCKUPS_INDEX.md`
   - Phase B fidelity classification: `audits/2026-06-10-mockup-design-intent-audit.json`
   - Verification log esistente: `audits/us-verification-log.md`

## Protocollo a 4 step

### STEP 1 — Mostra il mockup

Apri / mostra il file canonico del mockup. Per ogni step della US:
- Path assoluto del mockup HTML
- Page sequence (Next.js routes visitati in ordine)
- API endpoints toccati per ogni step
- Stato design_intent (current | forward-refactor | forward-refactor-obsolete) dal fidelity.json
- Eventuali tab/state matrix (empty / loading / error / success)

Cita la fonte: spec doc OR fidelity.json + line.

### STEP 2 — User conferma visual match

Aspetta conferma:
- ✅ "OK" → procedi
- ⚠️ "Looks wrong: <nota>" → log divergenza, chiedi se continuare
- 🚫 "Skip" → log SKIPPED + jump al verdict

### STEP 3 — User performa la US

Fornisci:
- URL di partenza (es. `http://localhost:3000/login`)
- Click sequence numerata (passo per passo)
- Per ogni click: cosa osservare (UI feedback, navigation, network call)
- Almeno 1 edge case (error state OR empty OR loading)

Aspetta il behavior osservato.

### STEP 4 — Logga il verdetto

Append a `audits/us-verification-log.md` sotto "Verification log entries":

\`\`\`markdown
### US-{N} — {VERDICT_EMOJI} {VERDICT_LABEL} — 2026-MM-DD

**Persona**: <persona>
**Mockup**: <path> (design_intent: <classification>)
**Page sequence**: <routes>
**API endpoints**: <endpoints>

**Visual verdict** (mockup ↔ app):
- <ok / divergence note>

**Functional verdict** (US flow):
- <ok / bug / not-implemented note>

**Edge cases tested**:
- <empty/loading/error scenarios>

**Verdict**: {EMOJI} {LABEL}

**Follow-up issues**:
- [ ] open #NNNN: <description>

**Time spent**: <minutes>
**Tester**: <user>

---
\`\`\`

Aggiorna anche la tabella "Verification queue" del log: status `⏳ pending` → `<verdict emoji>` per la riga US-{N}.

## Final step — committa al PR

\`\`\`bash
git add audits/us-verification-log.md
git commit -m "test(us-verify): US-{N} {verdict} — {one-line summary}

DS-17 Phase B manual US verification (sub-issue #2127).
Verdict: {VERDICT_LABEL}
{1-2 line summary}

Refs: PR #2128 (DS-17 Phase B audit), umbrella #2063

Co-Authored-By: Claude Opus 4.7 <noreply@anthropic.com>"
git push
\`\`\`

Conferma commit + push success.

## Boundaries

- NON modificare codice durante la verifica (solo log)
- Se verdict 🔧 FUNCTIONAL_BUG → proponi aprire issue separata, NON aprire senza conferma
- Se verdict 📐 MOCKUP_OBSOLETE → proponi riclassificazione fidelity.json + tracking issue (stile Phase B)
- Se la US ha più mockup (multi-route), verifica ognuno + logga sub-verdict per mockup
- Una sola US per invocazione (no batch)
```

## Verdict taxonomy

| Symbol | Label | Meaning | Follow-up |
|---|---|---|---|
| ✅ | PASS | Mockup + app match, US functional | Move to next US |
| ⚠️ | VISUAL_DRIFT | Functional but UI differs from mockup | Note + decide accept/fix |
| 🔧 | FUNCTIONAL_BUG | UI matches but flow breaks | File bug (with reproduction) |
| 🚫 | NOT_IMPLEMENTED | US sequence not reachable | Confirm if US is in scope |
| 📐 | MOCKUP_OBSOLETE | Mockup outdated; app correct | Reclassify mockup `forward-refactor-obsolete` |

## Recommended verification order (most common user-side first)

Initial queue priorità (alta frequenza → bassa frequenza):

1. **US-2** Login (entry point ogni sessione)
2. **US-6** Dashboard priority-driven (landing)
3. **US-25** Notifications inbox
4. **US-10** Library hybrid hub
5. **US-8** Games hub multi-tab (Discover default)
6. **US-9** Game detail tabs
7. **US-27** AI agent chat
8. **US-26** Profile + achievements
9. **US-13** GameNight create wizard
10. **US-15** GameNight detail + RSVP

Le altre 20 US identificate sono in `audits/2026-06-10-mockup-coverage-gap-report.md` § US-perspective gap matrix.

## Throughput targets

- **Time per US**: 5-10 minuti (incluso bug discovery)
- **Session capacity**: 6-8 US per ora
- **Coverage goal**: 30 US in ~4 ore (singolo testing slot)

## Refs

- Source US definitions: `audits/2026-06-10-mockup-coverage-gap-report.md`
- Source mockup mapping: `admin-mockups/MOCKUPS_INDEX.md`
- Active log: `audits/us-verification-log.md`
- Phase B audit: `audits/2026-06-10-mockup-design-intent-audit.json`
- Umbrella: [#2063 DS-17 Mockup-to-App Fidelity](https://github.com/meepleAi-app/meepleai-monorepo/issues/2063)
- Sub-issue: [#2127 Phase B](https://github.com/meepleAi-app/meepleai-monorepo/issues/2127)
- PR: [#2128](https://github.com/meepleAi-app/meepleai-monorepo/pull/2128)
