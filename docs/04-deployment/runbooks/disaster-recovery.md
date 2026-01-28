# Disaster Recovery Runbook

Procedure per il recovery da failure catastrofici.

## Recovery Objectives

| Metrica | Target | Descrizione |
|---------|--------|-------------|
| **RTO** (Recovery Time Objective) | < 2 ore | Tempo massimo per ripristino servizio |
| **RPO** (Recovery Point Objective) | < 24 ore | Perdita dati massima accettabile |

---

## Scenario 1: VPS Completamente Guasto

### Sintomi
- Ping al VPS fallisce
- SSH timeout
- Hetzner Console mostra server offline
- Possibile hardware failure

### Procedura (RTO: ~1-2 ore)

#### Step 1: Verifica su Hetzner Console (5 min)

1. Login su https://console.hetzner.cloud
2. Vai al server `meepleai-prod-01`
3. Verifica stato:
   - **Running ma unreachable** → Problema rete/firewall
   - **Stopped** → Prova Power On
   - **Error/Unavailable** → Hardware failure, serve nuovo VPS

#### Step 2: Tentativo Ripristino (10 min)

```
Hetzner Console → Server → Power:
1. "Power Off" (forza spegnimento)
2. Attendi 30 secondi
3. "Power On"
4. Attendi 2-3 minuti
5. Verifica SSH: ssh meepleai@YOUR_VPS_IP
```

Se fallisce → Procedi con Step 3.

#### Step 3: Restore da Snapshot (30-60 min)

```
Hetzner Console → Server → Snapshots:
1. Identifica snapshot più recente
2. Click "Restore" sullo snapshot
3. Conferma (ATTENZIONE: sovrascrive dati attuali)
4. Attendi completamento (10-30 min)
5. Verifica SSH accesso
```

#### Step 4: Se Snapshot Non Disponibile - Nuovo VPS (60 min)

```bash
# 1. Crea nuovo VPS su Hetzner Console
#    - Stesse specifiche: CPX31, Ubuntu 24.04, stessa region
#    - Nome: meepleai-prod-02

# 2. Copia IP nuovo server
NEW_IP=xxx.xxx.xxx.xxx

# 3. Setup base (da locale)
ssh root@$NEW_IP

# Esegui setup iniziale
apt update && apt upgrade -y
apt install -y git curl wget nano htop ufw fail2ban docker.io docker-compose-plugin

# Crea utente
adduser meepleai
usermod -aG sudo meepleai
usermod -aG docker meepleai

# Firewall
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable
```

#### Step 5: Restore Dati da Backup (30-60 min)

Vedi [backup-restore.md](./backup-restore.md) per procedure dettagliate.

```bash
# Se backup sono su storage esterno (R2/S3)
# Altrimenti devono essere copiati da altra fonte

# Clone repo
su - meepleai
cd /home/meepleai
git clone https://github.com/YOUR_USERNAME/meepleai-monorepo-frontend.git app
cd app/infra

# Restore secrets (dal tuo backup locale/password manager)
# Crea i file in secrets/

# Avvia servizi
docker compose -f docker-compose.prod.yml up -d

# Restore database
gunzip -c /path/to/postgres_backup.sql.gz | docker exec -i meepleai-postgres psql -U meepleai
```

#### Step 6: Aggiorna DNS (5 min)

```
Cloudflare Dashboard → DNS:
1. Aggiorna record A per @ e www con nuovo IP
2. Aggiorna record A per api con nuovo IP
3. TTL basso (1 min) per propagazione veloce
```

#### Step 7: Verifica (15 min)

```bash
# Health check
curl https://api.meepleai.com/health
curl https://meepleai.com

# Verifica SSL
echo | openssl s_client -connect meepleai.com:443 2>/dev/null | openssl x509 -noout -dates

# Test funzionale
# - Login
# - Query database
# - Upload file (se applicabile)
```

---

## Scenario 2: Database Corrotto

### Sintomi
- Errori "data corruption" nei log PostgreSQL
- Query falliscono con errori strani
- Inconsistenze nei dati

### Procedura (RTO: 30-60 min)

#### Step 1: Stop Applicazione

```bash
cd /home/meepleai/app/infra

# Stop solo API per evitare ulteriori scritture
docker compose -f docker-compose.prod.yml stop api
```

#### Step 2: Valuta Danno

```bash
# Connetti a PostgreSQL
docker exec -it meepleai-postgres psql -U meepleai -d meepleai

# Check integrità
\dt                          # Lista tabelle
SELECT count(*) FROM users;  # Test query base

# Se errori gravi → restore backup
```

#### Step 3: Restore Database

```bash
# Stop PostgreSQL
docker compose -f docker-compose.prod.yml stop postgres

# Rimuovi dati corrotti
docker volume rm infra_postgres_data

# Ricrea container
docker compose -f docker-compose.prod.yml up -d postgres

# Attendi startup
sleep 30

# Restore da backup
gunzip -c /home/meepleai/backups/postgres/dump_YYYYMMDD_HHMMSS.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai

# Riavvia API
docker compose -f docker-compose.prod.yml up -d api
```

#### Step 4: Verifica Integrità

```bash
docker exec -it meepleai-postgres psql -U meepleai -d meepleai

# Verifica tabelle principali
SELECT count(*) FROM users;
SELECT count(*) FROM games;
SELECT count(*) FROM documents;

# Se OK, test applicazione
curl https://api.meepleai.com/health
```

---

## Scenario 3: Ransomware / Compromissione

### Sintomi
- File criptati
- Richiesta riscatto
- Processi sospetti
- Accessi non autorizzati nei log

### Procedura (RTO: 2-4 ore)

#### Step 1: ISOLAMENTO IMMEDIATO

```bash
# DA HETZNER CONSOLE (non via SSH se compromesso):
# Power Off immediato del server

# Oppure se hai ancora accesso SSH:
sudo iptables -P INPUT DROP
sudo iptables -P OUTPUT DROP
sudo iptables -A INPUT -s YOUR_IP -j ACCEPT  # Solo il tuo IP
```

#### Step 2: NON PAGARE IL RISCATTO

Mai pagare. Non garantisce recovery e finanzia criminali.

#### Step 3: Preserva Evidenze

```
Hetzner Console:
1. Crea snapshot del server compromesso (per analisi forense)
2. Nome: "compromised-YYYYMMDD-DO-NOT-USE"
```

#### Step 4: Crea Nuovo VPS Pulito

Segui "Scenario 1: Step 4" per creare VPS nuovo.

#### Step 5: Restore da Backup PULITO

**IMPORTANTE**: Usa backup PRECEDENTE alla compromissione!

```bash
# Identifica quando è iniziata la compromissione
# Usa backup di almeno 24-48h prima

# Restore solo dati, NON codice (potrebbe essere compromesso)
# Clona repo fresh da GitHub
git clone https://github.com/YOUR_USERNAME/meepleai-monorepo-frontend.git
```

#### Step 6: Rotazione Credenziali

**TUTTE le credenziali devono essere rigenerate**:

```bash
cd /home/meepleai/app/infra/secrets

# Rigenera TUTTO
rm *.secret

# Genera nuove password
./setup-secrets.sh  # o genera manualmente

# Aggiorna anche:
# - Password SSH
# - API key OpenRouter
# - Cloudflare API token
# - Hetzner API token
# - Qualsiasi altra credenziale
```

#### Step 7: Review Sicurezza

- [ ] Analizza come è avvenuta la compromissione
- [ ] Aggiorna firewall rules
- [ ] Abilita 2FA ovunque
- [ ] Review accessi SSH (authorized_keys)
- [ ] Considera audit di sicurezza professionale

---

## Scenario 4: Cancellazione Accidentale Dati

### Sintomi
- Dati mancanti
- Tabella vuota/cancellata
- Volume Docker rimosso

### Procedura (RTO: 30-60 min)

#### Per PostgreSQL

```bash
# Stop scritture
docker compose -f docker-compose.prod.yml stop api

# Identifica ultimo backup valido
ls -la /home/meepleai/backups/postgres/

# Restore completo
gunzip -c /home/meepleai/backups/postgres/dump_YYYYMMDD.sql.gz | \
  docker exec -i meepleai-postgres psql -U meepleai

# Oppure restore singola tabella (se sai quale)
gunzip -c backup.sql.gz | grep -A 9999 "COPY users" | grep -B 9999 "^\\\." | \
  docker exec -i meepleai-postgres psql -U meepleai -d meepleai
```

#### Per Qdrant

```bash
# Restore da snapshot
curl -X POST "http://localhost:6333/collections/documents/snapshots/recover" \
  -H "api-key: $QDRANT_API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"location": "file:///qdrant/snapshots/snapshot_YYYYMMDD.snapshot"}'
```

#### Per Redis

```bash
# Stop Redis
docker compose -f docker-compose.prod.yml stop redis

# Restore dump
docker cp /home/meepleai/backups/redis/dump_YYYYMMDD.rdb meepleai-redis:/data/dump.rdb

# Restart
docker compose -f docker-compose.prod.yml start redis
```

---

## Checklist Pre-Disaster

Assicurati di avere SEMPRE:

- [ ] Backup automatici funzionanti (verifica settimanale)
- [ ] Backup offsite (non solo sul VPS)
- [ ] Secrets salvati in password manager
- [ ] Accesso a Hetzner Console
- [ ] Accesso a Cloudflare Dashboard
- [ ] Questo runbook accessibile offline

---

## Contatti Emergenza

| Servizio | Contatto | Note |
|----------|----------|------|
| Hetzner Support | support@hetzner.com | Hardware, VPS issues |
| Hetzner Abuse | abuse@hetzner.com | Se compromesso |
| Cloudflare | community.cloudflare.com | DNS issues |

---

## Test DR (Trimestrale)

Ogni 3 mesi, esegui test DR:

1. [ ] Crea VPS test temporaneo
2. [ ] Restore backup su VPS test
3. [ ] Verifica applicazione funziona
4. [ ] Documenta tempo impiegato
5. [ ] Elimina VPS test
6. [ ] Aggiorna RTO/RPO se necessario
