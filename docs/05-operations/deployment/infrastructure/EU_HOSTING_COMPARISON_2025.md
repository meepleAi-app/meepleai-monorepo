# Top EU Hosting Providers - Comparazione 2025
## Setup Ottimizzato per MeepleAI (2-5K Utenti)

**Data Analisi:** 2025-11-22
**Target:** 2,000-5,000 utenti attivi/mese
**Requisiti:** 10-12 vCPU, 26-30 GB RAM, 150-200 GB storage, Docker support, EU datacenter

---

## Executive Summary

### 🏆 TOP 3 Raccomandazioni

| Rank | Provider | Setup | Costo/mese | Costo/anno | Best For |
|------|----------|-------|------------|------------|----------|
| **🥇 #1** | **Contabo** | 2× VPS L | **€40-50** | **€480-600** | Budget assoluto |
| **🥈 #2** | **Hetzner** | 2× CPX41 | **€70-90** | **€840-1,080** | Balanced (RACCOMANDATO) |
| **🥉 #3** | **Aruba Cloud** | 2× O8A16 | **€12-36** | **€144-432** | Italia + Promo 50% |

**Altre opzioni valide:** OVH, UpCloud, Netcup, Exoscale

---

## Panoramica 10 Provider EU Analizzati

### Riassunto Comparativo

| Provider | Paese | Datacenter EU | Prezzo Entry | Performance | GDPR | Support |
|----------|-------|---------------|--------------|-------------|------|---------|
| **Hetzner** | 🇩🇪 Germania | DE, FI | €5-90/mese | ⭐⭐⭐⭐⭐ | ✅ Eccellente | ⭐⭐⭐⭐ |
| **Contabo** | 🇩🇪 Germania | DE, EU | €4-25/mese | ⭐⭐⭐⭐ | ✅ Sì | ⭐⭐⭐ |
| **OVH** | 🇫🇷 Francia | FR, PL, DE | €6-40/mese | ⭐⭐⭐⭐ | ✅ Sì | ⭐⭐⭐⭐ |
| **Aruba Cloud** | 🇮🇹 Italia | IT (4 DC) | €2-20/mese | ⭐⭐⭐ | ✅ Eccellente | ⭐⭐⭐⭐ |
| **Scaleway** | 🇫🇷 Francia | FR, NL, PL | €7-50/mese | ⭐⭐⭐⭐ | ✅ Sì | ⭐⭐⭐ |
| **UpCloud** | 🇫🇮 Finlandia | FI, SE, DE, NL | €5-200/mese | ⭐⭐⭐⭐⭐ | ✅ Sì | ⭐⭐⭐⭐⭐ |
| **Netcup** | 🇩🇪 Germania | DE, AT, NL | €1-10/mese | ⭐⭐⭐ | ✅ Sì | ⭐⭐⭐ |
| **Exoscale** | 🇨🇭 Svizzera | CH, DE, AT | €10-300/mese | ⭐⭐⭐⭐ | ✅ Massima | ⭐⭐⭐⭐⭐ |
| **IONOS** | 🇩🇪 Germania | DE, ES, UK | €4-50/mese | ⭐⭐⭐ | ✅ Sì | ⭐⭐⭐ |
| **Hostinger** | 🇱🇹 Lituania | LT, NL, UK | €3-30/mese | ⭐⭐⭐ | ✅ Sì | ⭐⭐⭐ |

---

## 🥇 #1 CONTABO - Budget Champion

### Perché Contabo?

- ✅ **Prezzo imbattibile:** 6 vCPU + 16 GB RAM → €12/mese
- ✅ **Risorse generose:** Storage doppio dei competitor (400-800 GB SSD)
- ✅ **Banda illimitata:** 32 TB/mese inclusi
- ✅ **EU Datacenter:** Germania + altri paesi EU
- ✅ **Docker ready:** Container support nativo

### Setup Raccomandato per 2-5K Utenti

**Configurazione: 2× VPS L**

| Componente | Specs | Prezzo |
|------------|-------|--------|
| **VPS L #1** (App + AI) | 8 vCPU, 30 GB RAM, 800 GB SSD | €20-25/mese |
| **VPS L #2** (DB + Services) | 8 vCPU, 30 GB RAM, 800 GB SSD | €20-25/mese |
| **TOTALE** | 16 vCPU, 60 GB RAM, 1.6 TB | **€40-50/mese** |

**Oppure configurazione economica: 2× VPS M**

| Componente | Specs | Prezzo |
|------------|-------|--------|
| **VPS M #1** | 6 vCPU, 16 GB RAM, 400 GB SSD | €12-15/mese |
| **VPS M #2** | 6 vCPU, 16 GB RAM, 400 GB SSD | €12-15/mese |
| **TOTALE** | 12 vCPU, 32 GB RAM, 800 GB | **€24-30/mese** |

### Distribuzione Servizi (2× VPS L)

**Server #1 - Application Stack:**
- API (ASP.NET): 4 vCPU, 8 GB
- Web (Next.js): 2 vCPU, 4 GB
- Ollama LLM: 2 vCPU, 8 GB
- n8n: 0.5 vCPU, 1 GB
- Monitoring (Prometheus, Grafana): 1.5 vCPU, 3 GB

**Server #2 - Data & Services:**
- PostgreSQL: 4 vCPU, 12 GB
- Qdrant: 2 vCPU, 8 GB
- Redis: 1 vCPU, 2 GB
- Seq (logs): 1 vCPU, 2 GB

### Pro e Contro

**Pro:**
- 💰 **Prezzo imbattibile:** -50-60% vs competitor
- 📦 **Storage massiccio:** 400-800 GB SSD per VPS
- 🌐 **Banda generosa:** 32 TB/mese
- 🇪🇺 **EU compliant:** Datacenter tedeschi GDPR
- 🚀 **Performance solida:** AMD EPYC processors

**Contro:**
- ⚠️ **Support base:** Non 24/7 premium
- ⚠️ **Performance variabile:** Shared resources, overselling possibile
- ⚠️ **Setup manuale:** Self-managed, no managed services
- ⚠️ **Uptime SLA:** 99.9% (vs 99.95% premium)

### Quando Scegliere Contabo

✅ Budget molto limitato (<€100/mese)
✅ Serve molto storage (PDF, logs, backups)
✅ Team tecnico può gestire self-managed
✅ Non serve SLA enterprise 99.99%
❌ Se serve support 24/7 premium
❌ Se performance critiche millisecondi

**Costo Totale Mensile:** €40-50
**Costo Annuale:** €480-600
**Risparmio vs AWS Fargate:** ~€320/mese (-86%)

**Fonti:**
- [Contabo VPS Pricing](https://contabo.com/en/vps/)
- [Contabo Review 2025](https://hostadvice.com/hosting-company/contabo-reviews/contabo-vps-hosting-review/)
- [Contabo vs Hetzner](https://www.vpsbenchmarks.com/compare/contabo_vs_hetzner)

---

## 🥈 #2 HETZNER - Balanced Excellence ⭐ RACCOMANDATO

### Perché Hetzner?

- ✅ **Best price/performance:** Performance top a prezzo competitivo
- ✅ **Affidabilità:** 99.95% uptime, datacenter Tier 3+
- ✅ **Performance eccellente:** AMD EPYC, NVMe SSD, benchmarks superiori
- ✅ **EU compliance:** Germania/Finlandia, GDPR nativo
- ✅ **Supporto qualità:** Responsive, documentazione ottima
- ✅ **20 TB bandwidth incluso** per server

### Setup Raccomandato per 2-5K Utenti

**Configurazione: 2× CPX41**

| Componente | Specs | Prezzo |
|------------|-------|--------|
| **CPX41 #1** (App Stack) | 8 vCPU, 16 GB RAM, 240 GB NVMe | €46.41/mese |
| **CPX41 #2** (Data Stack) | 8 vCPU, 16 GB RAM, 240 GB NVMe | €46.41/mese |
| **TOTALE** | 16 vCPU, 32 GB RAM, 480 GB | **€92.82/mese** |

**Oppure configurazione mista (più economica):**

| Componente | Specs | Prezzo |
|------------|-------|--------|
| **CPX41** (App + AI + Web) | 8 vCPU, 16 GB RAM, 240 GB NVMe | €46.41/mese |
| **CPX31** (DB + Redis + Services) | 4 vCPU, 8 GB RAM, 160 GB NVMe | €23.59/mese |
| **TOTALE** | 12 vCPU, 24 GB RAM, 400 GB | **€70/mese** |

**Oppure nuova opzione Cost-Optimized (2025):**

| Componente | Specs | Prezzo |
|------------|-------|--------|
| **CX33** × 2 | 4 vCPU, 8 GB RAM, 80 GB NVMe (each) | €5.49 × 2 = €10.98 |
| **CPX31** | 4 vCPU, 8 GB RAM, 160 GB NVMe | €23.59 |
| **TOTALE** | 12 vCPU, 24 GB RAM, 320 GB | **€34.57/mese** |

### Distribuzione Servizi (Setup 2× CPX41)

**Server #1 - CPX41 Application:**
- API (ASP.NET): 3 vCPU, 6 GB
- Web (Next.js): 2 vCPU, 4 GB
- Ollama LLM: 2 vCPU, 4 GB
- Monitoring: 1 vCPU, 2 GB

**Server #2 - CPX41 Data:**
- PostgreSQL: 4 vCPU, 8 GB
- Qdrant: 2 vCPU, 4 GB
- Redis: 1 vCPU, 2 GB
- n8n + Seq: 1 vCPU, 2 GB

### Pro e Contro

**Pro:**
- 🚀 **Performance top:** +13% benchmark CPU vs Contabo
- 🏢 **Affidabilità:** 99.95% uptime, Tier 3+ datacenter
- 🇩🇪 **EU nativo:** Germania (Falkenstein, Nuremberg) + Finlandia (Helsinki)
- 📚 **Documentazione:** Eccellente, community attiva
- 🌐 **Banda inclusa:** 20 TB/mese per server (vs 1-5 TB competitor)
- 💾 **NVMe SSD:** Storage veloce, performance I/O superiori
- 🔧 **UI moderna:** Cloud console user-friendly
- 🆘 **Support:** Email responsive, ticket system efficiente

**Contro:**
- 💰 **Prezzo medio-alto:** +40-50% vs Contabo (ma giustificato)
- 🛠️ **Self-managed:** No managed services inclusi
- 📊 **Monitoring:** Base, serve setup Prometheus/Grafana
- ⚠️ **US pricing increase 2024:** Bandwidth ridotta in US (non impatta EU)

### Quando Scegliere Hetzner

✅ **Produzione seria** (2-5K+ utenti)
✅ Budget €50-150/mese disponibile
✅ Performance e affidabilità priorità
✅ Team può gestire self-managed
✅ EU datacenter requirement
✅ Scaling graduale previsto
❌ Se budget <€50/mese totale
❌ Se serve managed database

**Costo Totale Mensile:** €70-93
**Costo Annuale:** €840-1,116
**Risparmio vs AWS Fargate:** ~€270-300/mese (-75%)

**Perché Raccomandato:**
- **Best balance prezzo/performance** per produzione 2-5K utenti
- **Affidabilità provata** (milioni di server deployed)
- **Performance eccellenti** (+13% CPU, NVMe SSD)
- **Support qualità** senza costi premium
- **Scaling path chiaro** (da CX a CCX a Dedicated)

**Fonti:**
- [Hetzner Cloud Pricing](https://www.hetzner.com/cloud)
- [Hetzner CPX Instances](https://www.vpsbenchmarks.com/hosters/hetzner)
- [Hetzner Cost-Optimized 2025](https://www.bitdoze.com/hetzner-cloud-cost-optimized-plans/)
- [Contabo vs Hetzner Performance](https://www.vpsbenchmarks.com/compare/contabo_vs_hetzner)

---

## 🥉 #3 ARUBA CLOUD - Italian Excellence

### Perché Aruba Cloud?

- ✅ **Datacenter italiani:** 4 DC in Italia (Arezzo ×2, Bergamo, Roma)
- ✅ **GDPR compliance massima:** Dati rimangono in Italia
- ✅ **Promo 50% attiva:** Fino al 31/12/2025
- ✅ **Support italiano:** Lingua madre, timezone compatibile
- ✅ **CISPE member:** Cloud Infrastructure Services Provider Europe

### Setup Raccomandato per 2-5K Utenti

**Configurazione: 2× Cloud VPS OpenStack O8A16**

| Componente | Specs | Prezzo Base | Con Promo 50% |
|------------|-------|-------------|---------------|
| **O8A16 #1** (App) | 8 vCPU, 16 GB RAM, 160 GB NVMe | €18.19/mese | **€6.29/mese** |
| **O8A16 #2** (DB) | 8 vCPU, 16 GB RAM, 160 GB NVMe | €18.19/mese | **€6.29/mese** |
| **TOTALE** | 16 vCPU, 32 GB RAM, 320 GB | €36.38/mese | **€12.58/mese** |

**Banda:** 100 TB/mese per VPS inclusi

**Note:** Prezzi +IVA (22% Italia)

**Con IVA 22%:**
- Promo 50%: €12.58 + 22% = **€15.35/mese totale**
- Prezzo normale: €36.38 + 22% = **€44.38/mese**

### Distribuzione Servizi (2× O8A16)

**Server #1 - Application Stack:**
- API (ASP.NET): 3 vCPU, 8 GB
- Web (Next.js): 2 vCPU, 4 GB
- Ollama: 2 vCPU, 4 GB
- Monitoring: 1 vCPU, 2 GB

**Server #2 - Data Stack:**
- PostgreSQL: 4 vCPU, 8 GB
- Qdrant: 2 vCPU, 4 GB
- Redis: 1 vCPU, 2 GB
- n8n + Seq: 1 vCPU, 2 GB

### Datacenter Italiani

**4 Datacenter Tier IV:**

1. **IT1 - Arezzo Campus 1**
   - Tier IV certified
   - 200,000+ server capacity
   - Certificazioni: ISO 27001, CISPE

2. **IT2 - Arezzo Campus 2**
   - Tier IV certified
   - Espansione campus principale

3. **IT3 - Ponte San Pietro (Bergamo)**
   - Near Milano
   - Campus innovativo

4. **IT4 - Roma**
   - Datacenter moderno
   - Connectivity hub sud Italia

**Connectivity:** Multi-carrier, peering diretto principali ISP italiani

### Pro e Contro

**Pro:**
- 🇮🇹 **Datacenter italiani:** Dati restano in Italia
- 💰 **Promo 50%:** €12.58/mese (fino 31/12/2025)
- 🗣️ **Support italiano:** Lingua, timezone, cultura
- 🏢 **GDPR massimo:** Compliance italiana, CISPE member
- 📈 **Scalabile:** OpenStack architecture
- 🌐 **Banda generosa:** 100 TB/mese
- 🔐 **Tier IV:** Datacenter certificati massima affidabilità
- 🏆 **Leader italiano:** #1 hosting provider in Italia

**Contro:**
- ⚠️ **Performance media:** Non veloce come Hetzner
- 💸 **Promo temporanea:** Prezzo sale a €36/mese dal 2026
- 🌍 **Reach limitato:** Solo datacenter italiani
- 📊 **Community piccola:** Meno documentazione online vs Hetzner
- ⚙️ **Features base:** No managed services avanzati

### Quando Scegliere Aruba Cloud

✅ **Dati DEVONO restare in Italia** (compliance, regolamenti)
✅ Preferenza datacenter italiani (latency, supporto)
✅ Budget iniziale basso (promo 50%)
✅ Support in italiano priorità
✅ Target utenti principalmente italiani
⚠️ **Nota promo:** Pianificare aumento costi 2026 (€12 → €36/mese)
❌ Se serve performance massima
❌ Se budget fisso lungo termine

**Costo Totale Mensile:**
- **Con promo 50% (fino 31/12/2025):** €15.35/mese (IVA inclusa)
- **Dopo promo (2026+):** €44.38/mese (IVA inclusa)

**Costo Annuale:**
- **Anno 1 (con promo):** €184/anno
- **Anno 2+ (senza promo):** €532/anno

**Risparmio vs AWS Fargate:**
- Con promo: ~€350/mese (-96%)
- Senza promo: ~€320/mese (-88%)

**Considerazioni:**
- **Eccellente per lancio** con promo 50%
- **Valutare migrazione o rinnovo** al termine promo (fine 2025)
- **Alternative post-promo:** Hetzner €70/mese o Contabo €40/mese

**Fonti:**
- [Aruba Cloud Pricing](https://www.arubacloud.com/pricing)
- [Aruba Cloud VPS](https://www.arubacloud.com/vps/)
- [Aruba Italian Datacenters](https://www.arubacloud.com/infrastructures/italy-dc-it1.aspx)
- [Aruba Cloud 50% Promo](https://www.aruba.it/en/cloud-price-list.aspx)

---

## Altre Opzioni Valide

### OVH - French Giant

**Setup:** 2× VPS Elite (8 vCPU, 16 GB RAM)
**Prezzo:** €60-80/mese stimato

**Pro:**
- Grande provider francese
- Datacenter multipli EU
- Anti-DDoS incluso
- Support 24/7

**Contro:**
- Prezzo medio-alto
- Performance variabile
- UI complessa
- Support quality inconsistente

**Quando:** Se già su ecosistema OVH, serve anti-DDoS, datacenter Francia

**Fonte:** [OVH Cloud Pricing](https://www.ovhcloud.com/en/public-cloud/prices/)

---

### UpCloud - Finnish Premium

**Setup:** 2× General Purpose (8 vCPU, 16 GB RAM)
**Prezzo:** €100-150/mese stimato

**Pro:**
- **Performance eccellente:** Top benchmarks
- **Zero-cost egress:** No bandwidth charges!
- **100% SLA:** Uptime garantito
- **MaxIOPS storage:** Performance I/O massime
- ISO 27001, GDPR, CISPE

**Contro:**
- Prezzo premium (+30-50% vs Hetzner)
- Meno popolare (community piccola)

**Quando:** Budget disponibile, performance critiche, zero bandwidth charges importante

**Fonti:**
- [UpCloud Pricing](https://upcloud.com/pricing/)
- [UpCloud Performance](https://www.vpsbenchmarks.com/compare/upcloud)

---

### Netcup - Ultra Budget

**Setup:** 2× VPS 3000 G11 (8 vCPU, 16 GB RAM)
**Prezzo:** €15-25/mese stimato

**Pro:**
- **Prezzo bassissimo:** €1-3/mese entry
- Best price-performance ratio
- Germania/Austria datacenter
- Price match guarantee (+10% discount)

**Contro:**
- Performance entry-level
- Support base
- Less known internationally

**Quando:** Budget estremo <€30/mese, non serve performance top

**Fonte:** [Netcup VPS Pricing](https://www.netcup.com/en/server/vps)

---

### Exoscale - Swiss Privacy

**Setup:** 2× Medium instances
**Prezzo:** €80-150/mese stimato

**Pro:**
- **Privacy massima:** Svizzera, no US Cloud Act
- **Compliance top:** ISO 27001, SOC-2, BSI C5
- **Kubernetes managed:** CNCF certified
- Data rimangono in Svizzera

**Contro:**
- **Prezzo premium:** +50-100% vs competitor
- Meno feature vs AWS/GCP
- Swiss franc currency fluctuation

**Quando:** Privacy/compliance critici (finance, healthcare, legal), budget disponibile

**Fonti:**
- [Exoscale Pricing](https://www.exoscale.com/)
- [Exoscale Switzerland](https://www.exoscale.com/datacenters/switzerland/)

---

### Scaleway - French Innovation

**Setup:** 2× DEV1-M (3 vCPU, 12 GB RAM) o GP1-M
**Prezzo:** €40-70/mese stimato

**Pro:**
- Prezzi competitivi
- GPU instances disponibili
- Kubernetes managed
- Paris/Amsterdam/Warsaw datacenter

**Contro:**
- Performance variabile
- Support issues reported
- Less mature than OVH

**Quando:** GPU serve per AI, preferenza Francia, Kubernetes managed

**Fonte:** [Scaleway Pricing](https://www.scaleway.com/en/pricing/)

---

## Comparazione Finale Top 3

### Tabella Dettagliata

| Criterio | Contabo | Hetzner ⭐ | Aruba Cloud |
|----------|---------|----------|-------------|
| **Prezzo/mese** | €40-50 | €70-93 | €15-44 |
| **Setup** | 2× VPS L | 2× CPX41 | 2× O8A16 |
| **vCPU** | 16 | 16 | 16 |
| **RAM** | 60 GB | 32 GB | 32 GB |
| **Storage** | 1.6 TB | 480 GB | 320 GB |
| **Bandwidth** | 32 TB/mese | 20 TB/server | 100 TB/VPS |
| **Performance** | ⭐⭐⭐⭐ | ⭐⭐⭐⭐⭐ | ⭐⭐⭐ |
| **Affidabilità** | 99.9% | 99.95% | 99.95% |
| **Support** | Email | Email/Ticket | Italiano 24/7 |
| **Datacenter** | DE, EU | DE, FI | IT (4 DC) |
| **GDPR** | ✅ Sì | ✅ Nativo | ✅ Massimo |
| **Docker** | ✅ Sì | ✅ Sì | ✅ OpenStack |
| **Managed** | ❌ No | ❌ No | ⚠️ Opzionale |
| **Scaling** | Manual | Manual | OpenStack |
| **Best For** | Budget | Production | Italia/Promo |

### Performance Benchmarks (CPU Score)

| Provider | CPU Score | Rank | Note |
|----------|-----------|------|------|
| **Hetzner CPX** | 2,150 | #1 | AMD EPYC, best single-thread |
| **UpCloud** | 2,100 | #2 | MaxIOPS storage |
| **Contabo** | 1,900 | #3 | Buono ma variabile |
| **Aruba Cloud** | 1,700 | #4 | Performance media |
| **OVH** | 1,850 | - | Variabile per DC |

*Score GeekBench 6 multi-core, valori indicativi*

**Fonte:** [VPSBenchmarks Comparisons](https://www.vpsbenchmarks.com/)

---

## Decision Matrix

### Quale Scegliere?

```
Budget disponibile:
├─ <€50/mese    → CONTABO ✅
├─ €50-100/mese → HETZNER ⭐ (RACCOMANDATO)
└─ €100+/mese   → UpCloud o managed AWS

Priorità datacenter:
├─ Italia       → ARUBA CLOUD (con promo) ✅
├─ Germania     → HETZNER o Contabo
├─ Francia      → OVH o Scaleway
└─ Svizzera     → Exoscale (privacy max)

Priorità performance:
├─ Massima      → UpCloud o Hetzner ⭐
├─ Buona        → Hetzner o OVH
└─ Sufficiente  → Contabo o Netcup

Priorità compliance/privacy:
├─ Standard EU  → Tutti OK
├─ Italia-only  → ARUBA CLOUD ✅
└─ Swiss privacy → Exoscale

Esperienza team:
├─ Junior       → Hetzner (doc migliore)
├─ Esperto      → Contabo (best value)
└─ Enterprise   → UpCloud o Exoscale
```

---

## Setup Raccomandato Finale

### Per MeepleAI (2-5K Utenti)

**Scelta #1 - BALANCED (Raccomandato):**
- **Provider:** Hetzner
- **Setup:** 2× CPX41 o 1× CPX41 + 1× CPX31
- **Costo:** €70-93/mese (~€840-1,116/anno)
- **Perché:** Best balance prezzo/performance, affidabilità, scaling

**Scelta #2 - BUDGET:**
- **Provider:** Contabo
- **Setup:** 2× VPS M o 2× VPS L
- **Costo:** €24-50/mese (~€288-600/anno)
- **Perché:** Prezzo minimo, storage massimo, OK per bootstrap

**Scelta #3 - ITALIA (Temporanea):**
- **Provider:** Aruba Cloud
- **Setup:** 2× O8A16 (con promo 50%)
- **Costo:** €15/mese fino 31/12/2025, poi €44/mese
- **Perché:** Datacenter italiani, promo eccellente, compliance Italia
- **⚠️ Nota:** Pianificare migrazione o rinnovo Q1 2026

---

## Migration Path & Scaling

### Fase 1: Lancio (0-2K utenti, mesi 1-6)
- **Provider:** Contabo 1× VPS M (€12/mese) o Hetzner CX33 (€5/mese)
- **Setup:** Single server all-in-one
- **Costo:** €5-15/mese

### Fase 2: Growth (2-5K utenti, mesi 7-12) ⭐ CURRENT
- **Provider:** Hetzner 2× CPX o Contabo 2× VPS
- **Setup:** App server + Data server
- **Costo:** €40-90/mese

### Fase 3: Scale (5-20K utenti, anno 2)
- **Provider:** Hetzner CPX/CCX o UpCloud
- **Setup:** Load balanced (3-4 servers)
- **Costo:** €150-300/mese
- **Aggiunte:** Managed DB (Neon/Supabase), Load Balancer

### Fase 4: Enterprise (20K+ utenti, anno 3+)
- **Provider:** UpCloud, Hetzner Dedicated, o AWS
- **Setup:** Kubernetes cluster, multi-region
- **Costo:** €500-2,000/mese
- **Aggiunte:** CDN premium, Managed K8s, Multi-AZ

---

## Costi Totali Scenario Completo

### Setup Hetzner (Raccomandato)

| Componente | Provider | Costo/mese | Costo/anno |
|------------|----------|------------|------------|
| **Hosting** | Hetzner 2× CPX | €70 | €840 |
| **Database** | Self-hosted (incluso) | €0 | €0 |
| **Redis** | Self-hosted (incluso) | €0 | €0 |
| **AI/LLM** | Ollama + OpenRouter | €15 | €180 |
| **Dominio** | .it Cloudflare | €0.75 | €9 |
| **DNS** | Cloudflare Free | €0 | €0 |
| **SSL** | Let's Encrypt | €0 | €0 |
| **Email** | AWS SES | €2 | €24 |
| **CDN** | Cloudflare Free | €0 | €0 |
| **Backup** | Backblaze B2 | €1 | €12 |
| **Monitoring** | Self-hosted | €0 | €0 |
| **GitHub** | Team 3 utenti | €12 | €144 |
| **TOTALE** | | **€100.75/mese** | **€1,209/anno** |

### Setup Contabo (Budget)

| Componente | Provider | Costo/mese | Costo/anno |
|------------|----------|------------|------------|
| **Hosting** | Contabo 2× VPS L | €45 | €540 |
| **Altri servizi** | (come sopra) | €30 | €360 |
| **TOTALE** | | **€75/mese** | **€900/anno** |

### Setup Aruba (Italia con Promo)

| Componente | Provider | Costo/mese | Costo/anno |
|------------|----------|------------|------------|
| **Hosting** | Aruba 2× O8A16 (promo) | €15 | €180 |
| **Altri servizi** | (come sopra) | €30 | €360 |
| **TOTALE** | | **€45/mese** | **€540/anno** |

**Nota:** Aruba costo sale a €74/mese dal 2026 senza promo

---

## Conclusioni e Raccomandazione Finale

### 🏆 Raccomandazione per MeepleAI

**Provider:** **HETZNER** (2× CPX41 o mixed setup)
**Costo:** ~€100/mese totale (~€1,200/anno)
**Setup:** Balanced production-ready

### Perché Hetzner?

1. ✅ **Best balance:** Prezzo competitivo + performance eccellenti
2. ✅ **Affidabilità provata:** 99.95% uptime, milioni di clienti
3. ✅ **Performance superiori:** +13% CPU vs competitor, NVMe SSD
4. ✅ **EU compliance nativo:** Germania/Finlandia, GDPR, ISO 27001
5. ✅ **Documentation & Support:** Community attiva, docs eccellenti
6. ✅ **Scaling path chiaro:** Da CX → CPX → CCX → Dedicated
7. ✅ **20 TB bandwidth incluso:** No sorprese su bandwidth charges
8. ✅ **Provider maturo:** 25+ anni esperienza

### Alternative Valide

**Se budget <€50/mese:**
- **Contabo** 2× VPS M → €30/mese
- Ottimo rapporto qualità/prezzo, storage massiccio

**Se datacenter Italia obbligatorio:**
- **Aruba Cloud** 2× O8A16 con promo → €15/mese (fino fine 2025)
- Poi pianificare migrazione o €44/mese

**Se performance critiche e budget disponibile:**
- **UpCloud** → €100-150/mese
- Zero bandwidth costs, performance top, 100% SLA

### Next Steps

1. **Settimana 1:** Setup Hetzner account + 1× CPX31 per testing (€23/mese)
2. **Settimana 2-3:** Deploy stack Docker, test performance/reliability
3. **Mese 2:** Aggiungere secondo server (CPX41) se carico aumenta
4. **Mese 3:** Valutare performance reali vs previsioni
5. **Mese 6:** Review e ottimizzazione, decidere scaling strategy

---

## Fonti Complete

### Provider Reviews & Pricing
- [Best European VPS 2025](https://hostadvice.com/vps/europe/)
- [EU Cloud Providers Comparison](https://dev.to/devlinktips/the-best-european-cloud-hosting-providers-in-2025-performance-compliance-and-cost-compared-27k)
- [VPS Benchmarks](https://www.vpsbenchmarks.com/)

### Hetzner
- [Hetzner Cloud Pricing](https://www.hetzner.com/cloud)
- [Hetzner Performance](https://www.vpsbenchmarks.com/hosters/hetzner)
- [Hetzner vs Contabo](https://www.vpsbenchmarks.com/compare/contabo_vs_hetzner)
- [Hetzner Cost-Optimized Plans](https://www.bitdoze.com/hetzner-cloud-cost-optimized-plans/)

### Contabo
- [Contabo VPS Pricing](https://contabo.com/en/vps/)
- [Contabo Review 2025](https://hostadvice.com/hosting-company/contabo-reviews/contabo-vps-hosting-review/)
- [Contabo Pricing 2025](https://www.g2.com/products/contabo/pricing)

### Aruba Cloud
- [Aruba Cloud Pricing](https://www.arubacloud.com/pricing)
- [Aruba VPS](https://www.arubacloud.com/vps/)
- [Aruba Italian Datacenters](https://www.arubacloud.com/infrastructures/italy-dc-it1.aspx)

### OVH
- [OVH VPS Pricing](https://www.ovhcloud.com/en/vps/)
- [OVH Docker VPS](https://www.ovhcloud.com/en/vps/os/vps-docker/)
- [OVH Public Cloud Pricing](https://www.ovhcloud.com/en/public-cloud/prices/)

### UpCloud
- [UpCloud Pricing](https://upcloud.com/pricing/)
- [UpCloud Performance](https://www.vpsbenchmarks.com/compare/upcloud)

### Altri
- [Netcup VPS](https://www.netcup.com/en/server/vps)
- [Exoscale Pricing](https://www.exoscale.com/)
- [Scaleway Pricing](https://www.scaleway.com/en/pricing/)

---

**Documento preparato da:** Claude (Anthropic)
**Per:** MeepleAI Engineering Team
**Ultimo aggiornamento:** 2025-11-22
**Versione:** 1.0 (EU Provider Comparison)
