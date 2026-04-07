# Disaster Recovery Runbook — MeepleAI

**Last Updated**: 2026-04-07
**RTO**: < 2 ore | **RPO**: < 24 ore

---

## Quick Reference

| Risorsa | Percorso / Dettaglio |
|---------|----------------------|
| Backup locali (server) | `/backups/meepleai/YYYYMMDD-HHMMSS/` |
| Backup R2 | `s3://meepleai-backups/` |
| Log backup | `/var/log/meepleai-backup.log` |
| Cron jobs | `/etc/cron.d/meepleai-backup` |
| Repo sul server | `/opt/meepleai/repo/` |
| Secrets | `/opt/meepleai/repo/infra/secrets/` |
| SSH accesso | `ssh -i ~/.ssh/meepleai-staging deploy@204.168.135.69` |
| Server | Hetzner CAX21 — `204.168.135.69` |
| PostgreSQL container | `meepleai-postgres` (image: `pgvector/pgvector:pg16`) |

---

## Verifica backup

```bash
# Stato generale backup
make backup-status

# Verifica integrità ultimo backup
make backup-verify

# Mostra configurazione cron
make backup-cron-show
```

---

## Scenario 1 — Server irraggiungibile (RTO 1–2h)

Server non risponde, SSH non disponibile, datacenter KO.

**Prerequisiti**: accesso Hetzner Cloud Console, credenziali AWS/R2, secrets in luogo sicuro.

### Passi

1. **Provision nuovo server Hetzner**

   Vai su [console.hetzner.cloud](https://console.hetzner.cloud), crea nuovo server:
   - Tipo: CAX21 (ARM, 4 vCPU, 8 GB RAM)
   - OS: Ubuntu 24.04
   - Location: Falkenstein (FSN1) o Nbg1 in caso di disaster area
   - Aggiungi SSH key pubblica del team
   - Nota nuovo IP (es. `NEW_IP`)

2. **Installa Docker**

   ```bash
   ssh -i ~/.ssh/meepleai-staging deploy@NEW_IP
   curl -fsSL https://get.docker.com | sudo sh
   sudo usermod -aG docker deploy
   # Ri-accedi per applicare i gruppi
   exit && ssh -i ~/.ssh/meepleai-staging deploy@NEW_IP
   ```

3. **Clona il repository**

   ```bash
   sudo mkdir -p /opt/meepleai
   sudo chown deploy:deploy /opt/meepleai
   git clone https://github.com/meepleAi-app/meepleai-monorepo.git /opt/meepleai/repo
   cd /opt/meepleai/repo
   ```

4. **Ripristina secrets**

   Copia i secrets da storage sicuro (password manager / vault del team):
   ```bash
   mkdir -p /opt/meepleai/repo/infra/secrets
   # Copia manualmente ogni *.secret dalla fonte sicura
   # Oppure, se il vecchio server è ancora accessibile in read:
   scp -i ~/.ssh/meepleai-staging -r deploy@204.168.135.69:/opt/meepleai/repo/infra/secrets/ \
       /opt/meepleai/repo/infra/secrets/
   ```

5. **Scarica backup da R2**

   ```bash
   # Installa AWS CLI se non presente
   sudo apt-get install -y awscli

   # Configura credenziali R2 (endpoint Cloudflare)
   aws configure set aws_access_key_id     "$R2_ACCESS_KEY"
   aws configure set aws_secret_access_key "$R2_SECRET_KEY"
   aws configure set default.region        auto

   # Lista backup disponibili
   aws s3 ls s3://meepleai-backups/ \
       --endpoint-url https://<ACCOUNT_ID>.r2.cloudflarestorage.com

   # Scarica backup più recente
   BACKUP_DATE=$(aws s3 ls s3://meepleai-backups/ \
       --endpoint-url https://<ACCOUNT_ID>.r2.cloudflarestorage.com \
       | sort | tail -1 | awk '{print $2}' | tr -d '/')

   aws s3 sync "s3://meepleai-backups/${BACKUP_DATE}/" /backups/meepleai/restore/ \
       --endpoint-url https://<ACCOUNT_ID>.r2.cloudflarestorage.com
   ```

6. **Avvia il database**

   ```bash
   cd /opt/meepleai/repo/infra
   docker compose -f docker-compose.staging.yml up -d postgres
   ```

7. **Attendi health check PostgreSQL**

   ```bash
   until docker exec meepleai-postgres pg_isready -U meepleai; do
     echo "Attendo postgres..."; sleep 3
   done
   echo "PostgreSQL pronto"
   ```

8. **Ripristina PostgreSQL dal backup**

   ```bash
   BACKUP_FILE="/backups/meepleai/restore/postgres.sql.gz"
   echo "Ripristino da: $BACKUP_FILE"

   gunzip -c "$BACKUP_FILE" | docker exec -i meepleai-postgres \
       psql -U meepleai -d meepleai
   ```

9. **Ripristina PDF uploads**

   ```bash
   # Se storage locale
   UPLOADS_BACKUP=$(ls /backups/meepleai/restore/pdf_uploads_*.tar.gz | sort | tail -1)
   if [ -n "$UPLOADS_BACKUP" ]; then
     docker exec meepleai-api mkdir -p /app/pdf_uploads
     tar -xzf "$UPLOADS_BACKUP" -C /tmp/uploads_restore/
     docker cp /tmp/uploads_restore/. meepleai-api:/app/pdf_uploads/
   fi

   # Se storage S3/R2 — nessuna azione necessaria (i file sono già su R2)
   ```

10. **Avvia l'applicazione completa**

    ```bash
    cd /opt/meepleai/repo/infra
    make staging
    ```

11. **Verifica health endpoints**

    ```bash
    # API health
    curl -sf http://NEW_IP:8080/health && echo "API OK"

    # Web health
    curl -sf http://NEW_IP:3000 && echo "Web OK"

    # Verifica DB connectivity dall'API
    curl -sf http://NEW_IP:8080/health/db && echo "DB OK"
    ```

12. **Aggiorna DNS Cloudflare**

    - Vai su [dash.cloudflare.com](https://dash.cloudflare.com)
    - Zona: `meepleai.app`
    - Aggiorna record A di `meepleai.app` e `api.meepleai.app` con `NEW_IP`
    - TTL: 60 secondi (propagazione ~5 min)

13. **Installa cron backup**

    ```bash
    cd /opt/meepleai/repo/infra
    make backup-cron-install
    make backup-cron-show  # Verifica installazione
    ```

---

## Scenario 2 — Database corrotto (RTO 30–60min)

Dati corrotti, migrazione fallita, tabelle danneggiate o drop accidentale.

### Passi

1. **Ferma API e Web per evitare ulteriori scritture**

   ```bash
   cd /opt/meepleai/repo/infra
   docker compose -f docker-compose.staging.yml stop api web
   ```

2. **Drop e ricrea il database**

   ```bash
   docker exec -i meepleai-postgres psql -U meepleai -d postgres <<'EOF'
   SELECT pg_terminate_backend(pid)
   FROM pg_stat_activity
   WHERE datname = 'meepleai' AND pid <> pg_backend_pid();
   DROP DATABASE IF EXISTS meepleai;
   CREATE DATABASE meepleai OWNER meepleai;
   EOF
   ```

3. **Ripristina dal backup più recente**

   ```bash
   BACKUP_FILE=$(ls /backups/meepleai/*/postgres.sql.gz 2>/dev/null | sort | tail -1)
   echo "Ripristino da: $BACKUP_FILE"
   gunzip -c "$BACKUP_FILE" | docker exec -i meepleai-postgres \
       psql -U meepleai -d meepleai
   ```

4. **Applica migrazioni EF Core pendenti**

   ```bash
   cd /opt/meepleai/repo/apps/api/src/Api
   dotnet ef database update
   ```

5. **Riavvia API e Web**

   ```bash
   cd /opt/meepleai/repo/infra
   docker compose -f docker-compose.staging.yml start api web
   ```

6. **Verifica integrità dati — row count chiave**

   ```bash
   docker exec -i meepleai-postgres psql -U meepleai -d meepleai <<'EOF'
   -- Authentication
   SELECT COUNT(*) AS utenti FROM "Administration"."Users";

   -- GameManagement
   SELECT COUNT(*) AS giochi FROM "GameManagement"."Games";

   -- Verifica tabelle vuote inattese
   SELECT schemaname, tablename, n_live_tup
   FROM pg_stat_user_tables
   WHERE n_live_tup = 0
   ORDER BY schemaname, tablename;
   EOF
   ```

   Confronta i count con i valori pre-incidente (disponibili nei log di monitoring o nel backup precedente).

---

## Scenario 3 — `docker compose down -v` accidentale (RTO 30–60min)

Il flag `-v` elimina i volumi Docker, inclusi i dati PostgreSQL.

Seguire **Scenario 2** dall'inizio (il database è vuoto ma il container esiste ancora).

Se anche il container è stato rimosso, eseguire prima:

```bash
cd /opt/meepleai/repo/infra
docker compose -f docker-compose.staging.yml up -d postgres
# Attendi health
until docker exec meepleai-postgres pg_isready -U meepleai; do sleep 3; done
```

Poi proseguire dal passo 2 dello Scenario 2.

---

## Scenario 4 — Compromissione server (RTO 2–4h)

Accesso non autorizzato, malware, credenziali compromesse.

### Passi

1. **Isola immediatamente il server**

   - Hetzner Console → aggiungi Firewall che blocca tutto il traffico in ingresso/uscita
   - Oppure spegni il server: Hetzner Console → Power Off
   - Notifica il team entro 15 minuti

2. **Non riutilizzare il server compromesso**

   Non fare snapshot, non fare backup dal server compromesso, non fidarsi di nessun dato sul server.

3. **Provision nuovo server pulito**

   Seguire **Scenario 1** per intero, usando un nuovo server Hetzner con nuovo IP.

4. **Ruota TUTTI i secrets**

   Prima di avviare il nuovo server, rigenera:
   - `JWT_SECRET` / `JWT_REFRESH_SECRET`
   - `DB_PASSWORD` (PostgreSQL)
   - `REDIS_PASSWORD`
   - Chiavi R2/S3 (`R2_ACCESS_KEY`, `R2_SECRET_KEY`)
   - `SMTP_PASSWORD`
   - Tutti gli OAuth client secrets (Google, GitHub)
   - Chiavi API (OpenAI, Anthropic, BGG)
   - Aggiorna `infra/secrets/*.secret` con i nuovi valori

5. **Ripristina da backup pulito (pre-compromissione)**

   Usa il backup più vecchio che sia sicuramente pre-compromissione.
   Evita i backup delle ultime ore se l'attacco potrebbe aver iniettato dati.

   ```bash
   # Lista backup R2 con date
   aws s3 ls s3://meepleai-backups/ \
       --endpoint-url https://<ACCOUNT_ID>.r2.cloudflarestorage.com
   # Scegli manualmente un backup sicuro
   ```

6. **Audit dei dati ripristinati**

   ```bash
   docker exec -i meepleai-postgres psql -U meepleai -d meepleai <<'EOF'
   -- Verifica utenti admin non attesi
   SELECT email, role, "CreatedAt"
   FROM "Administration"."Users"
   WHERE role = 'SuperAdmin'
   ORDER BY "CreatedAt" DESC
   LIMIT 20;

   -- Verifica accessi recenti
   SELECT "UserId", "CreatedAt", "IpAddress"
   FROM "Authentication"."Sessions"
   ORDER BY "CreatedAt" DESC
   LIMIT 50;
   EOF
   ```

7. **Hardening post-incidente**

   ```bash
   # Aggiorna sistema operativo
   sudo apt-get update && sudo apt-get upgrade -y

   # Verifica SSH config (solo chiavi, no password)
   sudo grep -E "PasswordAuthentication|PermitRootLogin" /etc/ssh/sshd_config

   # Reinstalla cron backup
   cd /opt/meepleai/repo/infra
   make backup-cron-install

   # Documenta l'incidente in docs/operations/incidents/
   ```

---

## Test DR trimestrale

Eseguire ogni 3 mesi per validare la procedura di recovery.

```bash
# Avvia test di restore automatizzato
cd /opt/meepleai/repo/infra
make backup-restore-test
```

### Checklist di verifica

- [ ] Backup più recente scaricato da R2 senza errori
- [ ] Database ripristinato correttamente (row count coerente con il backup)
- [ ] API risponde su `/health` dopo il restore
- [ ] Web frontend caricabile e autenticazione funzionante
- [ ] PDF uploads accessibili (test download di un documento esistente)
- [ ] Cron backup attivo e schedulato correttamente (`make backup-cron-show`)
- [ ] RTO effettivo documentato e confrontato con target (< 2h)

Documenta i risultati del test in `docs/operations/dr-test-results/YYYY-QN.md`.

---

## Contatti d'emergenza

| Ruolo | Responsabilità |
|-------|----------------|
| Lead Dev | Orchestrazione recovery, decisioni tecniche |
| DevOps | Provisioning server, DNS, R2 access |
| Hetzner Support | `support@hetzner.com` — incidenti datacenter |
| Cloudflare Support | DNS propagation, Firewall rules |

---

*Per dettagli su backup, monitoring e manutenzione ordinaria vedere [operations-manual.md](./operations-manual.md).*
