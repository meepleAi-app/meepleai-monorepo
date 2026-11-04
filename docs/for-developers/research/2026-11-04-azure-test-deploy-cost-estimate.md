# Ricerca — Quanto costa provare MeepleAI su Azure, con il dominio su Cloudflare

**Data**: 2026-11-04 · **Tipo**: ricerca (non è una decisione; le decisioni vanno in ADR) · **Profondità**: standard (listino pubblico + inventario del repo)
**Convenzione**: i prezzi sono listino USD pay-as-you-go estratti il 2026-11-04 dall'API pubblica dei prezzi Azure, regione **Germany West Central** salvo nota, 730 h/mese (2.628.000 s). Dove un prezzo non è stato letto per quella regione lo dice la riga. I totali sono risultati di calcolo con la formula accanto: si rifanno con la query in §6. Non compaiono conteggi di file, test o issue.

Estende la ricerca del 2026-10-08 ([Azure/AI-200](./2026-10-08-azure-ai-200-provider-switch-research.md), §7), che stimava i profili senza il vincolo di un test vero con un dominio. Il cancello d'accesso che impedisce a bot e crawler di accendere i container è in [`2026-11-04-azure-test-access-gate.md`](../specs/2026-11-04-azure-test-access-gate.md).

---

## 1. Sommario

| Scenario | Composizione | Stima mensile |
|---|---|---|
| **Weekend** | Stack di test acceso 48 h, poi `az group delete` | ≈ $2 |
| **Test a sessioni** | scale-to-zero, ~40 h attive al mese, Postgres sempre acceso | ≈ $36 |
| ↳ stesso, Postgres fermato fra le sessioni | compute Postgres solo nelle ore di sessione | ≈ $22 |
| ↳ stesso, nei primi 12 mesi del Free account | Postgres B1ms e 32 GB nel tier gratuito | ≈ $17 |
| **Sempre acceso** | api, web ed embedding con 1 replica minima | ≈ $95–150 |
| Dominio `meepleai.app` | DNS, Tunnel e Access (Free) | $0 in più |

Il Free account Azure dà $200 per 30 giorni: copre il primo mese di ogni scenario.
Il token dell'LLM (DeepSeek) resta fuori dal conto e invariato rispetto a oggi.

---

## 2. Tariffe lette (USD, Germany West Central)

| Meter | Tariffa |
|---|---|
| Container Apps, vCPU attiva | $0,000024 per vCPU-s |
| Container Apps, memoria attiva | $0,000003 per GiB-s |
| Container Apps, vCPU idle | $0,000003 per vCPU-s |
| Container Apps, memoria idle | $0,000003 per GiB-s — **assunta uguale all'attiva**: letta solo per West Europe, dove le due coincidono |
| Container Apps, richieste | incluse nel grant |
| Grant mensile per sottoscrizione | 180.000 vCPU-s, 360.000 GiB-s, 2 milioni di richieste |
| Postgres Flexible Server B1ms | $0,0199 all'ora |
| Postgres, storage | $0,1369 per GB-mese — letto per West Europe e Italy North, **non** per Germany West Central |
| Azure Managed Redis B0 | $0,019 all'ora — letto per West Europe |
| ACR Basic | €0,1496 al giorno — letto per West Europe, in EUR |

**La regione conta sulle Container Apps.** West Europe e Italy North costano circa il 40% in più sul vCPU attivo ($0,000034/s) e circa il 33% in più sulla memoria. Germany West Central, France Central, Sweden Central e North Europe stanno a $0,000024/s. Su Postgres B1ms le regioni europee lette stanno fra $0,018 (North Europe) e $0,0199 all'ora.

---

## 3. Voce per voce

Formula Container Apps: `secondi × (vCPU × tariffa_vCPU + GiB × tariffa_memoria)`. Una replica paga la tariffa idle se usa meno di 0,01 vCPU e meno di 1 KB/s di rete, e solo se la replica minima è maggiore di zero. A zero repliche non si paga nulla.

| Voce | Dimensione | Idle h24 | Attiva h24 |
|---|---|---|---|
| `web` | 0,5 vCPU / 1 GiB | $11,8 | $39,4 |
| `api` | 1 vCPU / 2 GiB | $23,7 | $78,8 |
| `embedding` (e5) | 1 vCPU / 2 GiB | $23,7 | $78,8 |
| Redis come container | 0,25 vCPU / 0,5 GiB | $5,9 | — |
| `cloudflared` | 0,25 vCPU / 0,5 GiB | $5,9 | — |
| Postgres B1ms | `0,0199 × 730` | $14,5 | — |
| Storage Postgres | 32 GB × 0,1369 | $4,4 | — |
| Redis gestito B0 (alternativa) | `0,019 × 730` | $13,9 | — |
| ACR Basic (opzionale) | €0,1496 × 30,4 giorni | ≈ $5 | — |

### Test a sessioni (~40 h attive al mese)

- Le tre app a scale-to-zero consumano `360.000 vCPU-s` e `720.000 GiB-s`. Sopra il grant restano `180.000 × 0,000024 + 360.000 × 0,000003 = $5,4`.
- `cloudflared` e Redis come container restano accesi: `$11,8` in due.
- Postgres sempre acceso: `$14,5 + $4,4 = $18,9`. Totale: `5,4 + 11,8 + 18,9 ≈ $36`.
- Postgres fermato fuori dalle sessioni: il compute scende a `40 × 0,0199 = $0,8`, lo storage resta: `5,4 + 11,8 + 4,4 + 0,8 ≈ $22`.
- Primi 12 mesi del Free account: 750 h di B1ms e 32 GB di storage sono gratuiti, quindi `5,4 + 11,8 ≈ $17`.

### Weekend (48 h, poi si cancella il resource group)

`Postgres 0,0199 × 48 = $1,0` + `storage 4,4 × 2/30 = $0,3` + `Redis e cloudflared idle 2 × 0,4` + le app dentro il grant ≈ **$2**.

### Sempre acceso (1 replica minima per api, web ed embedding)

- Tutto idle: `11,8 + 23,7 + 23,7 = $59,2`, più `≈ $7,6` per 40 h di uso reale sopra la tariffa idle: `≈ $67`. Con Postgres, Redis container e `cloudflared`: `≈ $97`.
- Se l'`api` non va mai in idle (§4.1) paga la tariffa attiva: `11,8 + 78,8 + 23,7 + ≈ 4,5 ≈ $119`, che con gli stessi tre servizi di sopra dà `≈ $150`.
- Il grant mensile toglie al più qualche dollaro.

---

## 4. Cosa può spostare il conto

1. **L'`api` potrebbe non andare mai in idle.** Ha dispatcher outbox e job periodici; se superano 0,01 vCPU o 1 KB/s di rete la tariffa è quella attiva. **Non misurato**: si legge in Cost Analysis dopo un giorno di prova.
2. **Le sonde esterne tengono accese le repliche.** Prometheus o un uptime monitor puntati sull'origin sono un costo continuo. Nel test non se ne mettono.
3. **Immagini.** `deploy-staging.yml` costruisce solo `linux/arm64`; Container Apps accetta solo `linux/amd64`. Serve una build multi-arch (costa tempo di CI, non denaro). Le immagini AI pesano circa 10 GB secondo la ricerca di ottobre: il cold start è lento, quindi l'embedding vuole una replica minima oppure un ACR nella stessa regione.
4. **Redis gestito.** Azure Managed Redis richiede TLS sulla porta 10000; la ricerca di ottobre riportava che il codice costruisce la connessione da `REDIS_HOST/PORT/PASSWORD` senza TLS (non riverificato qui). Per il test Redis resta un container. Sul B0 c'è anche una discrepanza non risolta fra 1 GB dichiarati e 500 MB visti nel portale.
5. **Postgres.** Serve l'estensione `vector` nella allow-list (`azure.extensions`). B1ms ha 2 GiB: basta per un corpus di test, da misurare prima di caricare quello di staging.
6. **PDF.** Unstructured (3 GB) e SmolDocling (2 GB) servono solo all'ingestione: non vanno su Azure. Si porta uno snapshot già indicizzato (flusso `dev-from-snapshot`; la ripristinabilità su Azure è **da verificare**).
7. **LLM.** Sul Free account Azure OpenAI ha quota 0: non serve, l'LLM resta esterno.
8. **Log Analytics.** I primi 5 GB al mese sono gratuiti; un log console verboso può superarli. Tetto giornaliero consigliato.

---

## 5. Il dominio su Cloudflare

DNS, Tunnel e il piano Free di Access non costano nulla in più; il dominio è già pagato. Due modi di collegare Azure:

| Modo | Come | Quando |
|---|---|---|
| **Tunnel** | `cloudflared` come container app (0,25 vCPU / 0,5 GiB, replica minima 1, **nessun ingress**), con le altre app a ingress limitato all'ambiente. Il repo ha già il servizio e `infra/secrets/cloudflared.secret.example` con `TUNNEL_TOKEN`. | Test: nessun certificato Azure, nessun endpoint pubblico. |
| **CNAME diretto** | CNAME verso l'FQDN dell'app e TXT `asuid.<sottodominio>`, con certificato gestito gratuito. Il record deve restare **DNS only** (nuvola grigia): la documentazione Microsoft dice che un CNAME verso un intermediario, e cita Cloudflare, blocca emissione e rinnovo. Usare un sottodominio, non l'apex. | Esercizio sul dominio personalizzato. |

---

## 6. Come rifare i prezzi

```bash
q() { curl -s -G "https://prices.azure.com/api/retail/prices" \
        --data-urlencode "currencyCode=USD" --data-urlencode "\$filter=$1"; }

# Container Apps: vCPU/memoria attiva e idle
q "serviceName eq 'Azure Container Apps' and skuName eq 'Standard' and armRegionName eq 'germanywestcentral'"
# Postgres Flexible Server B1ms
q "serviceName eq 'Azure Database for PostgreSQL' and skuName eq 'B1MS' and armRegionName eq 'germanywestcentral'"
# Azure Managed Redis
q "armRegionName eq 'westeurope' and contains(productName,'Managed Redis')"
```

In EUR le tariffe per secondo delle Container Apps compaiono arrotondate a 0: usare `USD`.

---

## 7. Non verificato

- Tariffa idle della memoria, storage Postgres e Redis B0 per Germany West Central (§2).
- Se `api` e `embedding` vanno davvero in idle (§4.1).
- Il flusso di ripristino dello snapshot su Azure (§4.6).
- Se il Free account è ancora disponibile per questa sottoscrizione, e le quote di vCPU delle Container Apps durante la prova gratuita.

## Fonti

- [Billing in Azure Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/billing)
- [Custom domains e certificati gestiti in Container Apps](https://learn.microsoft.com/en-us/azure/container-apps/custom-domains-managed-certificates)
- [Azure Retail Prices API](https://prices.azure.com/api/retail/prices)
- [Prezzi Azure Database for PostgreSQL Flexible Server](https://azure.microsoft.com/en-us/pricing/details/postgresql/flexible-server/)
