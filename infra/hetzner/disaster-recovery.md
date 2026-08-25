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
5. Restore PostgreSQL: see § *Restoring from the offsite copy* below. Do not paste a database name from memory — the cluster dump recreates it, and `meepleai_db` (named here until #3669) never existed.
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
- **Offsite copy: LIVE on staging since 2026-08-12** (#3669). Staging is the ONLY deployed environment — see the note on production below. The earlier wording here — *"`backup-to-r2.sh` not implemented; cron line commented out in `backup.cron`"* — described a path that was never deployed. The live backup is `infra/scripts/backup.sh`, installed by `make backup-cron-install`; `infra/hetzner/backup.sh` and `backup.cron` were never copied to `/usr/local/bin` and have been removed.

  A run that skips the offsite copy now reports `WARN … WITHOUT offsite copy` and sends a `degraded` webhook instead of claiming success — before #3669 it logged *"All backups completed successfully"* while the copy had silently never happened.

## Offsite backup — staging (verified 2026-08-12)

| | |
|---|---|
| Bucket | `meepleai-backups`, `eu-north-1`, public access blocked |
| Endpoint | `https://s3.eu-north-1.amazonaws.com` |
| Versioning | enabled, with the `backup-retention` lifecycle rule above |
| IAM user | `meepleai-backup` — verified `AccessDenied` on `s3:ListAllMyBuckets`, so a stolen key cannot even enumerate the account |
| Encryption | client-side `age`; the private key is **not** on the VPS |
| Binaries | `aws-cli 2.36.21` (aarch64 installer — `apt` has no `awscli` candidate on Ubuntu 24.04 arm64), `age 1.1.1` |

End-to-end proof, run with `env -i PATH=/usr/bin:/bin` so it matches cron's environment rather than an interactive shell:

```
Encrypted 3/3 files.
upload: ... postgres.sql.gz.age → s3://meepleai-backups/20260812-145136/
Backup complete — local: /backups/... | offsite: meepleai-backups
```

The restore path was exercised too, not just the write path: an object was pulled back from S3, decrypted with the private key, and its content validated (Redis RDB header). **A backup nobody has restored is a hypothesis, not a backup.**

### On "production"

**There is no production environment.** Staging is the only deployed one. The
scaffolding exists and reads as if a second host were live — which is exactly how
earlier revisions of this runbook, and #3669 itself, came to carry a "still to do
on production" item that nobody could ever do:

| artefatto | stato reale |
|---|---|
| `.github/workflows/deploy-production.yml` | **`.disabled`** — non gira |
| `infra/secrets/prod/` | solo template `.example`, nessun `.secret` |
| `infra/compose.prod.yml` | referenziato solo dal workflow disabilitato e da `rollback.yml` |
| branch `main` | esiste, ma non deploya su alcun host |

Se un giorno la produzione esiste, il lavoro per la copia offsite è: installare
`age` + l'AWS CLI aarch64, creare `backup.secret`, e generare una chiave age e un
utente IAM **separati** da staging — condividerli significherebbe che una sola
compromissione espone entrambi gli ambienti. Fino ad allora, quel lavoro non è in
ritardo: non ha un bersaglio.

## Restoring from the offsite copy

The offsite copy is encrypted client-side with `age`; the local copy is not. Only the offsite one needs decrypting.

```bash
aws s3 sync s3://meepleai-backups/<TIMESTAMP>/ ./restore/ \
  --endpoint-url "$S3_BACKUP_ENDPOINT" --region "$S3_BACKUP_REGION"

# The private key is NOT on the VPS by design — fetch it from the password manager.
find ./restore -name '*.age' -exec sh -c \
  'age -d -i backup-key.txt -o "${1%.age}" "$1"' _ {} \;

# postgres.sql.gz is a pg_dumpall CLUSTER dump: it carries CREATE ROLE and \connect,
# so it is fed to the maintenance database and recreates the application one under
# whatever name the source used. Do NOT name it here: it is "meepleai_staging" on
# staging, and an earlier revision of this runbook asserted "meepleai" — the third
# wrong database name on this same command. backup-restore-test.sh now discovers
# the name from the restored cluster instead of assuming it (#3669).
gunzip -c ./restore/postgres.sql.gz | docker exec -i meepleai-postgres psql -U meepleai -d postgres -q

# Which database did it recreate?
docker exec meepleai-postgres psql -U meepleai -d postgres -t -A \
  -c "SELECT datname FROM pg_database WHERE datistemplate = false AND datname <> 'postgres';"
```

⚠️ That command targets the **live** container. `backup-restore-test.sh` deliberately restores into a throwaway one; if you are validating a backup rather than recovering from a disaster, use the script instead of this.

⚠️ **Losing the age private key makes every offsite backup unrecoverable.** It is the one artifact that must survive the loss of both providers. Verify it is in the password manager *before* relying on this copy — a restore drill that only ever runs against the local backups will not tell you it is missing.

## Cost guards for the offsite bucket

Measured on staging (2026-08-12): **177 MB per backup**, 7-day retention, ~1.4 GB steady state — roughly **$0.04/month** on S3 Standard. The guards below are not about that figure; they are about the tail.

**1. Scope the IAM credentials to the bucket.** This is the guard that matters. A key sitting on the VPS is a key that can be exfiltrated, and the backup bill is not what an attacker would run up. Dedicated user, no `s3:*`, never root keys:

```json
{ "Version": "2012-10-17", "Statement": [
  { "Effect": "Allow",
    "Action": ["s3:PutObject", "s3:GetObject", "s3:DeleteObject", "s3:AbortMultipartUpload"],
    "Resource": "arn:aws:s3:::meepleai-backups/*" },
  { "Effect": "Allow", "Action": ["s3:ListBucket", "s3:ListBucketMultipartUploads"],
    "Resource": "arn:aws:s3:::meepleai-backups" }
]}
```

`DeleteObject` and `ListBucket` are required — `clean_s3_backups` prunes old prefixes. `AbortMultipartUpload` matters because a 177 MB dump is well past the CLI's multipart threshold: without it a failed transfer leaves orphaned parts behind, which are billed and do not show up in an object listing.

**2. Lifecycle rules — mandatory if versioning is on.** `clean_s3_backups` deletes with `aws s3 rm --recursive`, which on a **versioned** bucket only writes delete markers: noncurrent versions are kept and billed forever. The prune looks like it works while storage grows without bound. Also expire incomplete multipart uploads — fragments are billed and do not appear in object listings.

```json
{ "Rules": [
  { "ID": "backup-retention", "Status": "Enabled", "Filter": {},
    "Expiration": { "Days": 14 },
    "NoncurrentVersionExpiration": { "NoncurrentDays": 7 },
    "AbortIncompleteMultipartUpload": { "DaysAfterInitiation": 7 } }
]}
```

⚠️ Do **not** also enable *"delete expired object delete markers"*. The console rejects it: with `Expiration` active on a versioned bucket, S3 already removes the marker once the last noncurrent version expires. An earlier draft of this runbook listed both — they are mutually exclusive.

The 14-day expiry is a backstop **independent of the script**: `clean_s3_backups` swallows a failed `aws s3 ls` (`|| true`) and prunes nothing, silently.

⚠️ Keep `Expiration.Days` **strictly greater** than `BACKUP_RETENTION_DAYS`. Raise the retention above 14 and the lifecycle rule starts deleting first — the backstop quietly becomes the real policy, and the script's retention setting stops meaning anything.

**3. AWS Budget at $5/month with email alerts.** Expected spend is cents, so $5 never fires by accident and fires immediately when something is wrong — including things that are not S3 at all.

**4. Cost Anomaly Detection** (free) catches a spike before a monthly budget threshold can.

**What not to bother with:** `STANDARD_IA` or Glacier would save ~$0.02/month and add retrieval fees and minimum-duration charges exactly when you are restoring in an emergency. At this scale the optimisation costs more than it saves.

**The real cost driver to watch** is not retention: the nightly backup is **full, not incremental**, so it carries the whole PDF corpus every time. 177 MB is fine; if that corpus reaches several GB, revisit retention before the bill does it for you.
- **Promtail positions volatility**: positions file at `/tmp/positions.yaml` is lost on container restart, causing log re-ingestion. Action: mount named volume in compose.observability.yml.

## Foreign-key drift between the source database and a restored copy

**Measured on staging 2026-08-25 (#3669): 13 constraints, 11,446 orphaned rows.**

`backup-restore-test.sh` now reports `DEGRADED` when `ALTER TABLE ... ADD CONSTRAINT
... FOREIGN KEY` is refused during restore. The backup is not at fault: the dump
faithfully reproduces a source database that already violates its own constraints.
It still matters, and the consequence is easy to miss — **a database restored from
this dump comes back missing those foreign keys.** The data is all there; the
enforcement is not. Nothing in the restored copy says so.

The state should be impossible: `pg_constraint` reports these constraints as
`convalidated = true` while rows violating them exist. That combination only
arises if the parent rows were removed with FK triggers disabled — a bulk relink
run under `session_replication_role = 'replica'`, which is what the game-reset
tooling does.

The affected tables are join tables plus two others, each with roughly half its
rows orphaned — consistent with one generation of `shared_games` / `users` having
been replaced while the children kept pointing at the old ids:

| table | orphans / total |
|---|---|
| `shared_game_publishers` | 3576 / 7166 |
| `shared_game_mechanics` | 1314 / 2669 |
| `shared_game_categories` | 487 / 1008 |
| `shared_game_designers` | 211 / 443 |
| `mechanic_golden_claims` | 115 |
| `system_configurations` | 104 / 117 |
| `mechanic_golden_bgg_tags` | 31 |
| `user_library_entries` | 10 / 21 |

Find them all — this generates one query per single-column FK and reports only
the constraints with orphans:

```bash
docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -t -A -c "
SELECT string_agg(q, E'\nUNION ALL\n') FROM (
  SELECT format(
    'SELECT %L AS con, count(*) AS orphans FROM %I.%I c LEFT JOIN %I.%I p ON p.%I = c.%I WHERE c.%I IS NOT NULL AND p.%I IS NULL',
    con.conname, ns.nspname, cl.relname, fns.nspname, fcl.relname,
    fa.attname, ca.attname, ca.attname, fa.attname) AS q
  FROM pg_constraint con
  JOIN pg_class cl ON cl.oid = con.conrelid
  JOIN pg_namespace ns ON ns.oid = cl.relnamespace
  JOIN pg_class fcl ON fcl.oid = con.confrelid
  JOIN pg_namespace fns ON fns.oid = fcl.relnamespace
  JOIN pg_attribute ca ON ca.attrelid = con.conrelid AND ca.attnum = con.conkey[1]
  JOIN pg_attribute fa ON fa.attrelid = con.confrelid AND fa.attnum = con.confkey[1]
  WHERE con.contype = 'f' AND array_length(con.conkey,1) = 1
) s;" > /tmp/gen.sql

docker exec meepleai-postgres psql -U meepleai -d meepleai_staging -t -A -F'|' \
  -c "$(cat /tmp/gen.sql)" | awk -F'|' '$2>0'
```

ℹ️ Staging is the only deployed environment, so there is no second database to
check. If a production host is ever created, run the query above there too before
relying on a restore — the relink tooling would run there as well.

Cleaning the orphans is a data decision, not a backup fix, and is deliberately not
scripted here: deleting child rows is irreversible and the right answer may be to
re-link them instead.

## Restoring the offsite copy — the drill that cannot be automated

`backup-restore-test.sh` verifies the offsite object monthly, but only as far as it
honestly can: a ranged GET of the first 64 bytes proves the object exists, that the
host's credentials can read it, and that it carries the `age-encryption.org/v1`
header. **It does not prove the ciphertext decrypts.** It cannot: the private key
is deliberately not on the VPS, which is the whole point of encrypting the copy.

So the decrypt path has exactly one form of evidence — a human running it. Do this
quarterly, alongside the DR walkthrough reminder:

```bash
# 1. Fetch the private key from the password manager. Not from the VPS.
#    If this step fails, every offsite backup is already unrecoverable and the
#    monthly test would never have told you: it does not touch the key.

# 2. Pull one object and decrypt it.
aws s3 cp "s3://meepleai-backups/<TIMESTAMP>/postgres.sql.gz.age" ./probe.age \
  --endpoint-url "$S3_BACKUP_ENDPOINT" --region "$S3_BACKUP_REGION"
age -d -i backup-key.txt -o probe.sql.gz probe.age

# 3. Prove it is the real dump, not just well-formed bytes.
gzip -t probe.sql.gz && zcat probe.sql.gz | head -5 | grep "PostgreSQL database"

# 4. Restore it into a throwaway container — never the live one.
BACKUP_DIR=$(mktemp -d) && mkdir -p "$BACKUP_DIR/manual-drill" \
  && mv probe.sql.gz "$BACKUP_DIR/manual-drill/postgres.sql.gz" \
  && BACKUP_DIR="$BACKUP_DIR" bash infra/scripts/backup-restore-test.sh
```

Step 4 reuses the restore test rather than hand-rolling a `psql` invocation, so the
drill and the monthly gate cannot drift apart — which is precisely how the wrong
database name survived in both this runbook and the script.

## Notification channel

Backup scripts report through `infra/scripts/lib/notify.sh`: webhook first
(`BACKUP_WEBHOOK_URL`, else `SLACK_WEBHOOK_URL` from `monitoring.secret`), then
email (`SMTP_*` from `email.secret`, to `BACKUP_ALERT_EMAIL_TO`, defaulting to
`SMTP_FROM_EMAIL`).

⚠️ **The Slack webhook on staging is revoked** — it answers `HTTP 404 no_service`.
It was configured and non-empty the whole time, which is why nothing looked wrong.
`backup-verify.sh` now probes it daily with an empty body (a live hook rejects that
as `400 invalid_payload`; a revoked one still answers 404) and posts no message
either way. With the email fallback working the probe warns rather than fails —
alerts do arrive — but the dead URL should be replaced or removed.
