# Happy Path Testing Program

Verifica browser-based dei percorsi di successo di **tutte** le funzionalità dell'app (220+ route, 13 macro-aree), prima in locale poi in staging.

- **Strategia**: [`docs/superpowers/specs/2026-07-10-happy-path-testing-program-design.md`](../../../superpowers/specs/2026-07-10-happy-path-testing-program-design.md)
- **Piano**: [`docs/superpowers/plans/2026-07-10-happy-path-testing-program.md`](../../../superpowers/plans/2026-07-10-happy-path-testing-program.md)

## File in questa cartella

| File | Ruolo |
|------|-------|
| `_coverage-map.md` | Mappa globale route → area (guardia anti-buchi) |
| `_TEMPLATE.md` | Template scenario + legenda pass/blocked-env |
| `U1-accesso.md` … `A5-monitoraggio.md` | I 13 cataloghi di scenari (Given/When/Then) |
| `RESULTS.md` | Report di esecuzione (Fase B) |

## Cataloghi

| ID | Area | File |
|----|------|------|
| U1 | Accesso & Onboarding | `U1-accesso.md` |
| U2 | Catalogo & Discover | `U2-catalogo.md` |
| U3 | Library & Knowledge Base | `U3-library-kb.md` |
| U4 | Chat RAG & Agenti | `U4-chat-rag.md` |
| U5 | Game Night | `U5-game-night.md` |
| U6 | Sessioni & Scoring | `U6-sessioni-scoring.md` |
| U7 | Toolkit & Gamebook | `U7-toolkit-gamebook.md` |
| U8 | Profilo & Notifiche | `U8-profilo-notifiche.md` |
| A1 | Agenti AI (admin) | `A1-agenti.md` |
| A2 | Knowledge Base (admin) | `A2-kb-admin.md` |
| A3 | Catalogo condiviso (admin) | `A3-catalogo-condiviso.md` |
| A4 | Config & Sistema (admin) | `A4-config-sistema.md` |
| A5 | Monitoraggio & Utenti (admin) | `A5-monitoraggio.md` |

## Come eseguire (Fase B)

```bash
# 1. Avvia lo stack completo (serve l'AI per RAG/chat/toolkit)
cd infra && make dev

# 2. Popola i dati di test (admin + 5 utenti premium + giochi + PDF + sessioni…)
make seed-sp4

# 3. Verifica: web http://localhost:3000 · API http://localhost:8080/scalar/v1
#    Login utente standard: marco@meepleai.test (password: seed_password() in seed-sp4/lib/common.sh)
#    Login admin: da infra/secrets/admin.secret
```

Poi si eseguono gli scenari dei cataloghi nel browser, si registra l'esito in `RESULTS.md`,
e quando un'area è verde in locale la si ripete su `https://meepleai.app`
(`make seed-sp4-staging`). I fallimenti diventano issue GitHub. Vedi spec §7 (gate) e §7.1 (stato dati/cleanup).
