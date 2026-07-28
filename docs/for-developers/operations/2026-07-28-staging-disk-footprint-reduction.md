# Staging disk footprint reduction — 2026-07-28

**Context**: the CAX21 staging server (8GB RAM, 75GB disk) reached **79% disk usage
(57G/75G)**. Goal: reduce footprint without upgrading the machine while the app is
still staging-only (not public). Diagnosis via `docker system df -v` + `du`.

## What was consuming the disk

| Area | Size | Root cause |
|---|---|---|
| `unstructured-service` image + container | **10.2GB** | Residual: staging's active PDF extractor is `Docnet` (in-process, `appsettings.json → PdfProcessing:Extractor:Provider=Docnet`). The deploy CI (`--profile ai-essential`) never starts `unstructured` (profile `pdf-cloud-extractors`). The image was built locally by an old profile run. |
| Drift services (`minio`, `mailpit`, `loki`, `fluent-bit`, `orchestration`) | ~1.4GB + RAM | Started manually / by old profiles; none are in the deploy CI profiles (`ai-essential` / `monitoring-essential`). |
| `reranker_models` volume | 2.3GB | The reranker model is baked into the image (`HF_HOME=/home/reranker/.cache/huggingface`); the named volume mounted on that same path only duplicated it. |
| Prometheus TSDB volume | 2.6GB | Retention `60d` / `5GB` — oversized for a non-public staging env. |
| Container JSON logs | 1.5GB | No global `log-opts` on the host; only `api`/`web`/`cloudflared` set `logging:` in compose. |
| Runner CI cache (`.nuget`, `_work`, `.cache`) | ~10GB | A self-hosted GitHub Actions runner is co-located on the staging server (see Follow-ups). |

## Actions taken

### 1. Runtime cleanup (executed 2026-07-28) — freed ~13GB, disk 79% → 61%

Removed the residuals that the deploy CI never restarts. **Safe**: none of these are
in the `ai-essential` / `monitoring-essential` deploy profiles, and they have
`restart: unless-stopped`, so they must be removed with `rm -f` (not `stop`) to stay
down across a Docker daemon restart.

```bash
docker rm -f meepleai-unstructured && docker rmi meepleai-unstructured-service:latest
docker rm -f meepleai-minio meepleai-minio-init mailpit \
             meepleai-loki meepleai-fluent-bit meepleai-orchestrator
docker image prune -af --filter "until=1h"
```

The 13 remaining containers are exactly the deploy set (api, web, embedding, reranker,
postgres, redis, prometheus, grafana, alertmanager, cadvisor, node-exporter,
cloudflared, pg-proxy). To re-enable a service deliberately, use the intended target
(`make staging-with-tutor` for orchestration, `make logging` for loki/fluent-bit).

### 2. Durable repo fixes (this PR)

- **`infra/docker-compose.yml`** — removed the redundant `reranker_models` volume
  mount + definition. The model is baked into the image; the mount only duplicated
  ~2.3GB. `embedding-service` already runs volume-less for the same reason.
- **`infra/compose.staging.yml`** — Prometheus retention `60d`/`5GB` → `15d`/`1536MB`.
  Staging is non-public pre-prod; 2 weeks of metrics is plenty. Prod keeps `90d`/`50GB`.
- **`infra/hetzner/cax31-bootstrap.sh`** — added `/etc/docker/daemon.json` with
  json-file log rotation (`max-size 10m`, `max-file 3`). New servers now boot with a
  cap on container logs. Idempotent (does not clobber an existing `daemon.json`).

## Applying the durable fixes to the EXISTING staging server

These fixes land in the repo but two of them need a one-time action on the live server,
because they do not apply retroactively.

1. **`reranker_models` volume** — after this PR merges and the next deploy recreates the
   reranker container (now without the mount), reclaim the old volume:
   ```bash
   docker volume rm meepleai_reranker_models   # ~2.3GB
   ```
   Verify first that the reranker container was recreated without the mount:
   `docker inspect meepleai-reranker --format '{{json .Mounts}}'` (should be `[]`/no
   huggingface mount) and that `GET :8003/health` is green.

2. **`daemon.json` log rotation** — the bootstrap change only affects *new* servers.
   On the existing server, create the file and restart Docker once:
   ```bash
   sudo tee /etc/docker/daemon.json >/dev/null <<'EOF'
   { "log-driver": "json-file", "log-opts": { "max-size": "10m", "max-file": "3" } }
   EOF
   sudo systemctl restart docker
   ```
   Per Docker docs, log-opts apply to newly created containers only — existing
   containers keep their current logs until recreated (the next `--force-recreate`
   deploy covers them). To reclaim existing log bulk immediately:
   `sudo truncate -s 0 /var/lib/docker/containers/*/*-json.log`.

3. **Prometheus retention** — applies when the prometheus container is recreated on the
   next deploy. The TSDB volume shrinks gradually as old persistent blocks age past the
   `1536MB` cap (not instantly).

## Follow-ups (not in this PR)

- **Runner CI co-location (~10GB + RAM contention)** — a self-hosted GitHub Actions
  runner lives on the staging server. Its caches (`~/.nuget` 4GB, `_work` checkout 3.6GB,
  `~/.cache` 1.5GB) are rebuilt on every CI run, so a one-time clear only buys temporary
  headroom. Durable options: (a) move the runner to a dedicated host, (b) add a cron that
  prunes `~/.nuget`/`~/.cache` when idle, (c) cap the checkout retention. This is the
  main structural driver of regrowth and should be tracked separately.
- **Application-level retention gaps** (low impact on this server today, pgdata is <500MB):
  `game_analytics_events` has no retention job; abandoned chunked-upload sessions
  (`FindExpiredSessionsAsync` is never invoked) can leave temp `.bin` files + orphan DB
  rows. Worth a backend cleanup job before production scale.
