# SP4 Dataset Seed

Idempotent multi-step seed for the full SP4 mockup dataset (`admin-mockups/design_files/data.js`) via REST API.

## What gets seeded

| # | Step | Entity | From `data.json` | API endpoint(s) |
|---|---|---|---|---|
| 00 | `bootstrap` | Admin login | `ADMIN_EMAIL/PASSWORD` from `infra/secrets/admin.secret` | `POST /auth/login` |
| 10 | `users` | 5 users (Marco, Sara, Luca, Giulia, Andrea) | `data.json:users[]` | `POST /admin/users` |
| 20 | `games` | 8 shared games + quick-publish | `data.json:games[]` | `POST /admin/shared-games` + `…/quick-publish` |
| 30 | `kb` | 6 PDFs (renamed from `data/rulebook/*.pdf` to mock filenames) | `data.json:kbDocs[]` | `POST /ingest/pdf` + `GET /admin/pdfs` (poll) |
| 40 | `agents` | 5 agents (multi-login per ownership) | `data.json:agents[]` | `POST /agents/create-with-setup` |
| 50 | `toolkits` | 4 toolkits + ~10 tools (multi-login) | `data.json:toolkits[]` | `POST /game-toolkits` + nested `/{kind}-tools` |
| 60 | `library` | Per-user library entries (Owned / Wishlist) | `data.json:library{}` | `POST /library/games/{gameId}` |
| 70 | `sessions` | 6 live sessions with state transitions | `data.json:sessions[]` | `POST /live-sessions` + `…/start` / `…/complete` |
| 80 | `play-records` | Scaled per-user records (default ×0.2 of mock totals) | `data.json:playRecords{}` | `POST /play-records` + `…/players` + `…/complete` |
| 90 | `events` | 4 game nights | `data.json:events[]` | `POST /game-nights` + `…/publish` |
| 95 | `chats` | 5 chat threads (empty, no AI msgs) | `data.json:chats[]` | `POST /chat/sessions` |
| 99 | `reset` | DELETE all seeded entities | `$STATE_FILE` | `DELETE` reverse-order |

**Total expected time**: ~5–15 minutes (KB indexing is the bottleneck).

## Usage

```bash
# Local: full seed
make seed-sp4

# Single step (after bootstrap)
make seed-sp4-step STEP=30

# Resume from step (e.g. after fixing a bug at step 50)
make seed-sp4-from FROM=50

# Reset (local; safe by default)
make seed-sp4-reset

# Staging (requires SSH tunnel)
make tunnel
make seed-sp4-staging

# Staging reset — explicit confirmation
make seed-sp4-reset-staging
```

## Login credentials

All 5 SP4 dev users (`marco|sara|luca|giulia|andrea @meepleai.test`) share a
single password. It is **not** stored in `data.json` (secret scanners block
committed credentials) — the default lives in the `seed_password()` helper in
`lib/common.sh` and satisfies BE validators (≥8 chars, mixed case, digit,
symbol).

Override for staging or local dev convenience via env var (must be set for any
follow-up `--from N` runs too):

```bash
SEED_SP4_PASSWORD='your-dev-password' make seed-sp4
```

To read the current default, check `seed_password()` in `lib/common.sh`.

## Idempotency

Every step is safe to re-run:
- Lookup before create (`GET /admin/users?search=`, `GET /game-toolkits?gameId=`, …)
- `POST /ingest/pdf` returns existing KB on duplicate (filename + gameId match)
- State persisted in `/tmp/meepleai-sp4-{target}-state.json` (slug → guid map) so subsequent
  steps resolve references without re-fetching.

## PDF rename strategy

Real PDFs live at `data/rulebook/*_rulebook.pdf` (e.g. `azul_rulebook.pdf`, `catan_en_rulebook.pdf`).
For the seed, each KB doc copies its source PDF to a tmpdir with the mock filename
(e.g. `azul-regole-ita.pdf`, `catan-regole.pdf`) and uploads that. The renamed copy is
cleaned up on script exit.

## Known limitations

| Item | Status | Workaround |
|---|---|---|
| Universal agents (`gameSlug: null`) | Not seeded (BE requires `gameId`) | Skipped (1 of 5 agents) |
| Chat messages (AI responses) | Not seedable (real LLM only) | Threads created empty |
| `azul-faq.md` (markdown KB doc) | Endpoint accepts only PDF | Dropped (6 KB instead of 7) |
| Gloomhaven processing state "Failed" | Real upload may succeed (51MB ≤ 100MB limit) | Mock state is decorative; real state shown in UI |
| `play-records` totals | Scaled down via `PLAY_RECORDS_FACTOR=0.2` (default) | Set to `1.0` to match mock exactly (longer) |

## Architecture

- `data.json` — single source of truth (manually ported from SP4 `data.js`, kept under version control)
- `lib/common.sh` — shared helpers: logging, cookie jars per user, state file, polling
- `NN-name.sh` step scripts — each idempotent, can be run standalone
- `seed-sp4.sh` orchestrator — runs steps in order, with `--step` / `--from` / `--to` filtering
- `99-reset.sh` — reverse-order DELETE via API (best-effort)

## Troubleshooting

- **"missing infra/secrets/admin.secret"**: copy `admin.secret.example` and fill `ADMIN_PASSWORD`.
- **Step 30 (KB) timeouts**: indexing large PDFs (>50MB) can exceed default 20-min poll. Set
  `POLL_MAX_SEC=3600` to wait up to 1h.
- **Step 40 (agents) "no session for owner"**: re-run step 10 to warm cookie jars.
- **State out of sync after manual DB edit**: delete `/tmp/meepleai-sp4-{target}-state.json` and re-run from step 00.
