# Disaster Recovery Runbook — Hetzner CAX31

> **Status**: Stub artifact for Sprint 0. Full validation drill in Phase 4 Task 4.3.

## Deployment integration note

This task added observability stack files in `infra/observability/`. The plan v2 referenced a file `infra/docker-compose.production.yml` and modifications to `infra/Caddyfile` — these are **plan drift**:

- **Reality**: production stack is `infra/compose.prod.yml`; alpha stack `compose.alpha.yml`; MVP stack `compose.mvp.yml`. Reverse proxy is **Traefik** (see `traefik/` + `compose.traefik.yml`), not Caddy in production.
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
