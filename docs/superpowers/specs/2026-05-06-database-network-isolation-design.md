# Database Network Isolation — Specification

**Data**: 2026-05-06
**Autore**: Claude (incident response sc:spec-panel)
**Scope**: Garantire che PostgreSQL e altri servizi interni non siano mai raggiungibili dalla rete pubblica, indipendentemente da errori operativi o regressioni di Docker.
**Status**: APPROVED (post-incident response, utente sign-off 2026-05-06)
**Issue**: #795
**Trigger**: BSI/CERT-Bund report 2026-05-05 14:44:45 UTC — `204.168.135.69:5432` raggiungibile da Internet pubblica.

---

## 1. Context

### 1.1 Incident summary

| Dato | Valore |
|------|--------|
| Reporter | German Federal Office for Information Security (BSI / CERT-Bund), via `abuse@hetzner.com` |
| Target | `204.168.135.69:5432` (TCP, PostgreSQL) — staging server (Hetzner CAX21 ARM64) |
| Timestamp rilevazione | 2026-05-05 14:44:45 UTC |
| Indicator correlato | brute-force su filebrowser/8090 da `156.146.59.27` iniziato 13:52 UTC stesso giorno |
| Esposizione confermata? | Sì — BSI è autoritativo |
| Compromissione confermata? | No — nessuna evidenza nei log applicativi né del DB |

### 1.2 Forensics findings (2026-05-06)

Triage SSH eseguito su staging. Risultati:

- ❌ Nessun compose-file dichiara `5432:5432` in pubblicazione host (né in `infra/repo/`, né nei legacy `/opt/meepleai/*.yml` top-level)
- ❌ Nessun PostgreSQL host-installato (`apt list --installed | grep postgresql` → solo `postgresql-client*`)
- ❌ Nessun cron job o systemd timer che lanci container PG temporanei
- ❌ Nessuna evidenza in `~/.bash_history` di `docker run -p 5432`
- ⚠️ Docker event log: non persistente di default → trigger esatto dell'esposizione **non ricostruibile**
- 🚨 `filebrowser` running, esposto su `0.0.0.0:8090`, con **mount read-write di `/opt/meepleai`** (avrebbe esposto tutti i secret se l'auth fosse stata bypassata)

### 1.3 Root cause architetturale

**Docker bypassa UFW**. Quando un container pubblica una porta tramite `docker run -p X:X` o tramite `ports:` in compose **senza** prefisso `127.0.0.1:`, Docker inserisce regole nella catena `DOCKER` di iptables **prima** che la catena `ufw-user-input` venga valutata.

Questo significa che le regole UFW non si applicano al traffico verso container Docker. Fino a oggi il sistema aveva:

- ✅ UFW: `default deny incoming`, `allow 22, 80, 443`
- ❌ DOCKER chain: ACCEPT per qualsiasi porta pubblicata (bypass UFW)
- ❌ Nessuna regola in `DOCKER-USER` (l'unica chain che Docker non sovrascrive)
- ❌ Nessun firewall a livello edge (Hetzner Cloud Firewall non configurato)

→ Single-layer firewall. Il bug operativo (qualcosa ha pubblicato 5432 temporaneamente) ha avuto impatto pubblico immediato.

---

## 2. Decision

Adottiamo **defense-in-depth a tre livelli**:

### Layer 1 — Edge firewall (Hetzner Cloud Firewall)

Configurazione manuale dal pannello Hetzner Cloud Console:

- Inbound: TCP 22 (SSH); ICMP
- Tutto il resto: drop
- Attaccato al server staging (`204.168.135.69`)

**Rationale**: protegge anche se UFW e iptables si rompono o vengono accidentalmente flushati. Tracciamento separato (manuale UI), riferimento in `docs/operations/operations-manual.md`.

### Layer 2 — Host firewall (`DOCKER-USER` chain in iptables)

`DOCKER-USER` è l'unica catena iptables che Docker **non** sovrascrive. Inserire regole DROP per tutte le porte interne dell'applicazione:

```bash
sudo iptables -I DOCKER-USER -i <ext-if> -p tcp -m multiport --dports \
  5432,6379,9000,9001,9090,9093,3000,3001,8000,8001,8002,8003,8004,8025,1025 -j DROP
sudo iptables -I DOCKER-USER -i <ext-if> -p tcp -m multiport --dports \
  5678,3100,11434,8090 -j DROP
```

Persistenza tramite `iptables-persistent` + `netfilter-persistent save`.

**Rationale**: anche se domani qualcuno fa `docker run -p 5432:5432` per errore, il traffico in ingresso da `eth0` viene droppato in DOCKER-USER prima che le regole DOCKER lo accettino.

### Layer 3 — Service-level (Docker compose convention)

Convention codificata in code review:

- ✅ `ports: ["127.0.0.1:5432:5432"]` per dev/integration (loopback only)
- ❌ `ports: ["5432:5432"]` o `ports: ["0.0.0.0:5432:5432"]` — **PROIBITO**, anche per servizi non-DB

Lint check (futuro): aggiungere a `infra/scripts/security-audit.sh` un grep su tutti i compose `*.yml` che fallisce se trova `ports:` sul servizio postgres senza loopback prefix.

### Layer 4 — Continuous verification (security-audit + GitHub Action)

- `infra/scripts/security-audit.sh` (bash) e `.ps1` (PowerShell) — esegue scan esterno con `nmap`/`Test-NetConnection`, fallisce se porte non-whitelisted sono open
- `make security-audit` — target Makefile per esecuzione locale
- `.github/workflows/security-audit-staging.yml` — schedule daily, esegue lo scan da GitHub-hosted runner; apre issue su drift

**Rationale**: i requisiti di sicurezza non testabili sono speranza, non specifica (Wiegers). Trasformiamo l'invariante "DB MUST NOT be publicly reachable" in un *executable contract*.

---

## 3. Implementation checklist

- [x] Containment server-side (DOCKER-USER rules + persistenza) — applicato 2026-05-06
- [x] Issue GitHub #795 aperta
- [ ] PR `feature/issue-795-postgres-exposure-hardening` → `main-dev`:
  - [ ] Questo documento ADR
  - [ ] `infra/scripts/security-audit.sh`
  - [ ] `infra/scripts/security-audit.ps1`
  - [ ] `.github/workflows/security-audit-staging.yml`
  - [ ] `infra/Makefile` target `security-audit`
  - [ ] `infra/hetzner/cax31-bootstrap.sh` aggiornato con DOCKER-USER chain (+ pacchetto iptables-persistent)
- [ ] Hetzner Cloud Firewall configurato (manuale UI, fuori repo)
- [ ] Filebrowser: decisione (rimuovere vs CF Tunnel + Access)

---

## 4. Consequences

### Positive

- **Triple-layer defense**: edge firewall + host iptables + service convention
- **Detection automatica**: drift di porte rilevato entro 24h dal cron daily
- **Idempotenza**: il bootstrap script può essere rieseguito senza duplicare regole
- **Auditability**: l'invariante è dichiarata in spec e verificata da tooling

### Negative / trade-off

- **UFW rimosso**: durante il containment `apt install iptables-persistent` ha rimosso `ufw` (Ubuntu 24.04 li mette in conflict). Non è un problema funzionale (le ufw-* chains residue funzionano e iptables-persistent salva tutto), ma perdiamo la CLI friendly. Decisione: gestire iptables direttamente via script idempotenti.
- **Coupling con Hetzner Cloud Firewall**: configurato manualmente fuori repo. Mitigazione: documentazione in `docs/operations/operations-manual.md` con screenshot/checklist.
- **Cost del cron daily**: ~10 sec/giorno di GitHub Actions minutes, trascurabile.

### Neutral

- Le regole DOCKER-USER si applicano solo al traffico **da** `eth0`. Il traffico container-to-container resta libero (corretto: API → PG sulla rete `meepleai` deve funzionare).
- L'IPv6 non è esplicitamente coperto da queste regole. Aggiungere `ip6tables` con stesse regole nel bootstrap. ⚠️ TODO.

---

## 5. References

- BSI / CERT-Bund report: forwarded by `abuse@hetzner.com` 2026-05-06 12:23 CET
- Attacker IP filebrowser brute-force: `156.146.59.27`
- ASN: 24940 (Hetzner Online GmbH)
- Server: `ubuntu-8gb-hel1-1` (CAX21 ARM64)
- Docker UFW bypass — known issue: <https://github.com/moby/moby/issues/22054>
- Hetzner Cloud Firewall docs: <https://docs.hetzner.com/cloud/firewalls/>
- iptables `DOCKER-USER` chain reference: <https://docs.docker.com/network/packet-filtering-firewalls/>

---

## 6. Spec-panel review log

| Data | Trigger | Amendments |
|------|---------|-----------|
| 2026-05-06 | Initial sign-off post-incident | Spec approved, all 4 layers committed |
