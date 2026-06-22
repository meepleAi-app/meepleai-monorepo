# Claude Design — Handoff Bundles

Snapshot versionati dei prototipi React esportati da [claude.ai/design](https://claude.ai/design) durante le sessioni demo + gap audit sui mockup MeepleAI.

## Perché sono versionati

A differenza di `claude-design-bundle/` (source folder linkata a Claude Design, gitignored e rigenerabile via `cp` script), gli **handoff bundle** sono il risultato di iterazioni semantiche con socratic loop e decisioni product. Versionarli permette:

- **Portabilità**: clonare il repo su un altro terminale, aprire `MeepleAI Prototype.html` in browser, navigare il prototipo. Niente account claude.ai/design richiesto per la consultazione.
- **Review via PR**: i bundle entrano nella code review come qualsiasi asset (gap report, screenshot, JSX modulari).
- **Audit trail**: ogni snapshot è una baseline confrontabile con le successive (vedi issue [#1888](https://github.com/meepleAi-app/meepleai-monorepo/issues/1888), [#1889](https://github.com/meepleAi-app/meepleai-monorepo/issues/1889), [#1890](https://github.com/meepleAi-app/meepleai-monorepo/issues/1890) per re-run post-SP6/7/8).

## Struttura

Una subfolder per snapshot, formato `YYYY-MM-DD[-suffix]/`:

| Snapshot | Scope | Gap report companion |
|---|---|---|
| [`2026-06-04/`](2026-06-04/) | Baseline SP4 + SP7 wizard + Auth (12 mockup) | [`docs/for-developers/audits/2026-06-04-claude-design-gap-report.md`](../docs/for-developers/audits/2026-06-04-claude-design-gap-report.md) |

## Come navigare uno snapshot

```powershell
cd claude-design-handoff/<snapshot-folder>
python -m http.server 8765
# Apri http://localhost:8765/MeepleAI%20Prototype.html
```

Vedi README dentro ogni snapshot per dettagli (route prototipate, struttura JSX, hot-spot da testare).

## Come aggiungere uno snapshot nuovo

Quando completi una nuova iterazione (es: post-merge wave SP6):

1. Export → Handoff bundle dal canvas di Claude Design (`.zip` download)
2. Estrai in `claude-design-handoff/YYYY-MM-DD-spX/` (data + wave nel nome)
3. Aggiungi README dentro la subfolder con: route prototipate, struttura, hot-spot di test
4. Aggiorna la tabella "Struttura" qui sopra
5. Salva gap report in `docs/for-developers/audits/` con stesso prefisso data
6. Commit + PR

## Reference workflow

Pattern di esecuzione demo + gap audit: vedi `MEMORY.md` entry `claude-design-demo-workflow.md` (memoria personale del workflow consolidato 2026-06-04).
