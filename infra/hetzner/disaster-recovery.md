# Disaster Recovery Runbook — Hetzner CAX31

> **Status**: Stub artifact for Sprint 0. Full validation drill in Phase 4 Task 4.3.

## Deployment integration note

This task added observability stack files in `infra/observability/`. The plan v2 referenced a file `infra/docker-compose.production.yml` and modifications to `infra/Caddyfile` — these are **plan drift**:

- **Reality**: production stack is `infra/compose.prod.yml`; MVP stack `compose.mvp.yml`. Edge ingress is **Cloudflare Tunnel** (cloudflared on VPS, post-PR #738 cutover) — Traefik decommissioned. Caddy is used only for MVP stack on Hetzner CAX31.
- **Decision (controller)**: observability stack lives in `infra/observability/compose.observability.yml` as standalone, deployable via `docker compose -f compose.prod.yml -f observability/compose.observability.yml up -d`. Aaron decides actual integration at deploy time.

## Scenario 1: CAX31 down/lost (RTO target: 2h)

1. Provision new CAX31 (`hcloud server create --type cax31 --image ubuntu-24.04 --location fsn1 ...`)
2. Run bootstrap: `ssh root@<new-ip> 'bash -s' < infra/hetzner/cax31-bootstrap.sh`
3. Reboot: `ssh root@<new-ip> 'reboot'`
4. Mount Storage Box (Step 4 of plan v2 Task 0.4)
5. Restore PostgreSQL: `gunzip < $LATEST_BACKUP.age` after `age -d` decrypt | `docker exec -i meepleai-postgres psql -U meepleai meepleai_db`
6. Restore Redis: `docker cp $LATEST_RDB meepleai-redis:/data/dump.rdb && docker restart meepleai-redis`
7. Restore blob: `rsync -av /mnt/storagebox/backups/blob/ /var/lib/meepleai/blob/`
8. Update Cloudflare DNS A record to new CAX31 IP
9. Verify health: `curl https://api.meepleai.com/health` → 200

## Scenario 2: Region down — Falkenstein (RTO target: 30 min)

Hot standby in Helsinki (Hel1). Steps to be drilled in Phase 4 Task 4.3.

## Scenario 3: Data corruption (RTO target: 4h)

Stop services. Restore from previous good backup. Validate integrity. Restart.

## Validation drill

Run `make dr-drill` (to be created in Phase 4) to validate all 3 scenarios end-to-end on staging.

## Known issues — deferred to follow-up

The following code review findings are documented but NOT fixed in Sprint 0:

- **Item 4** (phantom Prometheus targets): `prometheus.yml` references `postgres-exporter:9187` and `redis-exporter:9121` but no such services exist in the compose stack. Targets must be added before scrape jobs work. Action: comment out or add exporters before deploying.
- **Item 6** (Loki schema deprecated): `loki-config.yml` uses `boltdb-shipper` + `schema v11` (Loki 2.x format). When Loki 3.x is pulled via `:latest`, runtime warnings or failures may occur. Action: pin Loki to `2.9.10` at deploy time, OR migrate to `tsdb` + `schema v13` for Loki 3.x.
- **Item 10** (redundant compose install in bootstrap): `cax31-bootstrap.sh` manually downloads `docker-compose-linux-aarch64` after `get.docker.com` already installs the Compose plugin via APT. This may cause version drift. Action: remove manual download step before production deploy.
- **Offsite copy is configured off, not missing** (#3669). The earlier wording here — *"`backup-to-r2.sh` not implemented; cron line commented out in `backup.cron`"* — described a path that was never deployed. The live backup is `infra/scripts/backup.sh`, installed by `make backup-cron-install`; `infra/hetzner/backup.sh` and `backup.cron` were never copied to `/usr/local/bin` and have been removed.

  `infra/scripts/backup.sh` **already uploads to S3/R2**, but the upload is gated on `S3_BACKUP_ENABLED`, and `backup.secret` is absent on the host — so every nightly run skips it. Until that secret is filled in, backups and production both live in the same Hetzner account and an incident on that account loses them together.

  Since #3669 a run that skips the offsite copy reports `WARN … WITHOUT offsite copy` and sends a `degraded` webhook instead of claiming success. To close the gap: create `infra/secrets/backup.secret` on the host from `backup.secret.example` (needs `S3_BACKUP_ENABLED=true`, credentials, endpoint and — for AWS — a real `S3_BACKUP_REGION`), and install the `aws` CLI, which is not currently present.
- **Promtail positions volatility**: positions file at `/tmp/positions.yaml` is lost on container restart, causing log re-ingestion. Action: mount named volume in compose.observability.yml.
