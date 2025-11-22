# MeepleAI - Analisi Costi Ottimizzata 2025
## Setup per 2,000-5,000 Utenti Iniziali

**Data Analisi:** 2025-11-22
**Versione:** 2.0 (Ottimizzata)
**Target Utenti:** 2,000-5,000 utenti attivi/mese
**Approccio:** Budget-friendly, scalabile, production-ready

---

## Executive Summary

### 💰 Costi Mensili Totali (2-5K Utenti)

| Categoria | Opzione Budget | Opzione Balanced | Opzione Premium |
|-----------|---------------:|------------------:|-----------------:|
| **GitHub** | €0 | €4-12 | €20-60 |
| **Hosting Infra** | €60-100 | €150-250 | €400-600 |
| **Database** | €15-30 | €50-80 | €120-200 |
| **AI/LLM** | €10-30 | €50-100 | €200-400 |
| **Dominio/DNS** | €1-2 | €2-3 | €3-5 |
| **Email** | €0 | €0-10 | €20-50 |
| **CDN/Banda** | €0-5 | €10-20 | €30-60 |
| **Storage/Backup** | €5-10 | €15-30 | €40-80 |
| **Monitoring** | €0 | €0-20 | €50-100 |
| **TOTALE MENSILE** | **€91-177** | **€281-525** | **€883-1,555** |
| **TOTALE ANNUALE** | **€1,092-2,124** | **€3,372-6,300** | **€10,596-18,660** |

**Raccomandazione per fase iniziale (2-5K utenti):** **Opzione Balanced** → ~**€350-400/mese** (~€4,200-4,800/anno)

---

## 1. GitHub - Repository Privato & CI/CD

### 1.1 Piano Consigliato per Team Piccolo (1-3 dev)

| Piano | Costo | Actions Minutes | Raccomandazione |
|-------|------:|----------------:|-----------------|
| **Free** | €0 | 2,000/mese | ✅ **Ideale per 1-2 dev** |
| Team | €4/utente | 3,000/mese | Se 3+ dev o >2K min/mese |
| Enterprise | €21/utente | 50,000/mese | ❌ Overkill per fase iniziale |

**Analisi workflow CI per 2-5K utenti:**
- ~10-15 PR/mese (team piccolo)
- ~60-80 minuti/PR
- **Totale stimato:** ~800-1,200 minuti/mese

**Raccomandazione:**
- **1-2 developer:** Piano Free (€0/mese) ✅
- **3-5 developer:** Piano Team (€12-20/mese)

**Fonti:** [GitHub Pricing](https://github.com/pricing), [GitHub Actions Billing](https://docs.github.com/en/billing/managing-billing-for-github-actions/about-billing-for-github-actions)

---

## 2. Infrastruttura Cloud - Dimensionamento per 2-5K Utenti

### 2.1 Requisiti Ridimensionati

Per 2,000-5,000 utenti con traffico medio (non 24/7 picco), i requisiti sono **molto inferiori** al setup production completo:

**Setup Completo Original (10K+ utenti MAU):**
- 15 servizi, 29 vCPU, 61.5 GB RAM
- Costo: ~€1,000/mese (AWS Fargate)

**Setup Ottimizzato 2-5K Utenti:**
- 10-12 servizi attivi, **8-12 vCPU, 20-30 GB RAM**
- Costo target: €150-400/mese

### 2.2 Architettura Semplificata

**Servizi CORE (sempre attivi):**

| Servizio | vCPU | RAM (GB) | Storage | Note |
|----------|-----:|----------:|---------|------|
| **API** (ASP.NET) | 2 | 4 | 5 GB | Auto-scale 1-3 istanze |
| **Web** (Next.js) | 1 | 2 | 5 GB | Auto-scale 1-2 istanze |
| **PostgreSQL** | 2 | 4 | 50 GB | Managed o self-hosted |
| **Redis** | 0.5 | 1 | 5 GB | Cache + sessioni |
| **Qdrant** | 1 | 4 | 20 GB | Vector DB ridotto |
| **Ollama** | 2 | 8 | 15 GB | Self-hosted LLM (CPU mode) |
| **n8n** | 0.5 | 1 | 5 GB | Workflow automation |
| **TOTALE CORE** | **9 vCPU** | **24 GB** | **105 GB** |

**Servizi OSSERVABILITÀ (opzionale ridotto):**

| Servizio | vCPU | RAM (GB) | Storage | Note |
|----------|-----:|----------:|---------|------|
| Prometheus | 0.5 | 1 | 20 GB | 30d retention (vs 90d) |
| Grafana | 0.25 | 0.5 | 2 GB | Dashboards |
| Seq | 0.5 | 1 | 10 GB | Logs 30d retention |
| **TOTALE OBSERV** | **1.25 vCPU** | **2.5 GB** | **32 GB** |

**Servizi ELIMINABILI in fase iniziale:**
- ❌ Jaeger (tracing) → usare solo in staging/debug
- ❌ Alertmanager → email dirette da Prometheus
- ❌ Embedding-service → usare solo Ollama
- ❌ Unstructured/SmolDocling → attivare on-demand

**TOTALE OTTIMIZZATO:** **10-11 vCPU, 26-27 GB RAM, 137 GB storage**

### 2.3 Comparazione Provider (Setup Ottimizzato)

#### Opzione 1: Hetzner Cloud (Budget) ⭐ **RACCOMANDATO**

**Configurazione:**
- 1x CPX41 (8 vCPU, 16 GB RAM): €46.41/mese
- 1x CPX31 (4 vCPU, 8 GB RAM): €23.59/mese
- Storage volumes (150 GB): €15/mese
- Backup: €5/mese
- **Totale:** ~**€90/mese** (~€1,080/anno)

**Pro:**
- Costo bassissimo (1/10 di AWS)
- Datacenter EU (Germania/Finlandia) - GDPR compliant
- Performance eccellente (AMD EPYC, NVMe SSD)
- Predicibile (prezzi fissi)

**Contro:**
- Self-managed (Docker Compose)
- No auto-scaling automatico
- Support base (no SLA enterprise)

**Fonte:** [Hetzner Cloud Pricing](https://www.hetzner.com/cloud)

---

#### Opzione 2: DigitalOcean (Balanced) ⭐ **ALTERNATIVA SEMPLICE**

**Configurazione:**
- 1x Droplet CPU-Optimized 8GB ($80/mese → ~€74): API + workers
- 1x Droplet General Purpose 4GB ($24/mese → ~€22): Web + Redis
- Managed PostgreSQL Basic ($15/mese → ~€14): Database
- Managed Redis 1GB ($15/mese → ~€14): Cache
- Spaces (150 GB) ($5/mese → ~€5): Storage
- **Totale:** ~**€129/mese** (~€1,548/anno)

**Pro:**
- Managed database incluso
- UI semplice, ottimo per startup
- Auto-backup inclusi
- Support decent

**Contro:**
- ~40% più costoso di Hetzner
- Meno performance a parità di prezzo

**Fonte:** [DigitalOcean Pricing](https://www.digitalocean.com/pricing)

---

#### Opzione 3: AWS Fargate (Premium/Auto-scale)

**Configurazione ottimizzata 2-5K utenti:**
- Compute (10 vCPU, 26 GB RAM × 730h):
  - vCPU: 10 × $0.04048 × 730 = $295.50 (~€272)
  - Memory: 26 × $0.004445 × 730 = $84.40 (~€78)
- Storage EBS gp3 (150 GB): $17.25 (~€16)
- **Totale:** ~**€366/mese** (~€4,392/anno)

**Con Reserved Instances (1 anno, -30%):**
- **Totale:** ~**€256/mese** (~€3,072/anno)

**Pro:**
- Auto-scaling perfetto
- Pay-per-use (scala a zero se necessario)
- Managed completo
- Integrazione AWS (RDS, S3, CloudWatch)

**Contro:**
- 3-4x più costoso di Hetzner
- Curva apprendimento
- Costi variabili difficili da prevedere

**Fonte:** [AWS Fargate Pricing](https://aws.amazon.com/fargate/pricing/)

---

### 2.4 Database PostgreSQL Dimensionamento

**Stima per 2-5K utenti:**
- Utenti: 5,000
- Sessioni: ~50-200 simultanee picco
- Giochi: ~500-1,000 entries
- PDF documenti: ~200-500
- Vettori Qdrant: ~100K-500K chunks
- **Storage DB:** ~20-50 GB (crescita ~5 GB/mese)

**Opzioni:**

| Provider | Configurazione | Costo/mese | Note |
|----------|----------------|------------|------|
| **Self-hosted (Hetzner)** | PostgreSQL container 2 vCPU, 4 GB | €0 (incluso) | Budget best |
| **DigitalOcean Managed** | Basic Node (1 vCPU, 1 GB, 10 GB) | €15 | Auto-backup |
| **AWS RDS** | db.t4g.micro (2 vCPU, 1 GB) | €13 + storage | Free tier 1 anno |
| **Supabase** | Free tier | €0 (→€25 Pro) | PostgreSQL + API |
| **Neon** | Free tier | €0 (→€19 Pro) | Serverless Postgres |

**Raccomandazione:**
- **Budget:** Self-hosted su Hetzner (€0 extra)
- **Balanced:** Neon Free → Pro €19/mese (serverless, auto-scale)
- **Premium:** AWS RDS db.t4g.small (€26/mese con backup)

**Fonti:**
- [AWS RDS Pricing](https://aws.amazon.com/rds/postgresql/pricing/)
- [DigitalOcean Managed Databases](https://www.digitalocean.com/pricing/managed-databases)
- [Neon Pricing](https://neon.tech/pricing)

---

## 3. AI/LLM - Costi Ottimizzati Self-Hosted

### 3.1 Stima Query per 2-5K Utenti

**Assunzioni:**
- 5,000 utenti/mese
- 20% utenti attivi chiedono domande = 1,000 utenti
- Media 3-5 query/utente = **3,000-5,000 query/mese**
- Media token: 2,000 input + 1,000 output

### 3.2 Opzione 1: Self-Hosted Ollama (CPU) ⭐ **RACCOMANDATO INIZIALE**

**Setup:**
- Ollama container: 2-4 vCPU, 8 GB RAM (già nel setup Hetzner)
- Modelli quantizzati GGUF 4-bit:
  - **Llama 3.1 8B Q4**: ~4.5 GB VRAM/RAM, ~20-40 token/sec CPU
  - **Mistral 7B Q4**: ~4 GB, veloce per FAQ
  - **Gemma 2 9B Q4**: ~5 GB, bilanciato

**Costi:**
- Hardware: €0 (incluso in server Hetzner CPX41)
- Modelli: €0 (open-source)
- Elettricità self-hosted: ~€5-10/mese (stima EU)
- **Totale:** ~**€5-10/mese**

**Pro:**
- Costo fisso bassissimo
- Privacy completa
- Zero rate limits
- Latenza bassa (EU datacenter)

**Contro:**
- Qualità inferiore a GPT-4 (ma OK per 80% query)
- Serve fallback per query complesse
- CPU-only = ~15-30 token/sec (accettabile)

**Performance CPU vs GPU:**
- **CPU (Hetzner CPX41):** ~20-30 token/sec (Llama 3.1 8B Q4)
- **GPU Hetzner GEX44 (+€184/mese):** ~100-150 token/sec
- **Conclusione:** Per 5K query/mese, CPU sufficiente! GPU overkill.

**Fonti:**
- [Ollama Multi-User Cost Analysis](https://markaicode.com/ollama-multi-user-cost-analysis/)
- [Ollama Hardware Requirements](https://www.arsturn.com/blog/hardware-requirements-for-running-ollama)

---

### 3.3 Opzione 2: Hybrid - Ollama + OpenRouter Fallback ⭐ **PRODUCTION IDEAL**

**Strategia:**
1. **80% query semplici** → Ollama Llama 3.1 8B (gratis)
2. **15% query medie** → OpenRouter Claude Haiku ($0.25/$1.25 per 1M token)
3. **5% query complesse** → OpenRouter GPT-4o ($2.50/$10 per 1M token)

**Calcolo costi OpenRouter (5,000 query/mese):**
- Query semplici (80%): 4,000 × €0 = €0 (Ollama)
- Query medie (15%): 750 × 2K input × $0.00025 + 1K output × $0.00125 = **$1.31** (~€1.20)
- Query complesse (5%): 250 × 2K input × $0.0025 + 1K output × $0.01 = **$3.75** (~€3.45)

**Totale:** €1.20 + €3.45 = **€4.65/mese OpenRouter + €5-10 Ollama = €10-15/mese totale**

**Ottimizzazioni ulteriori:**
- **KV Cache:** -40% ripetizioni (Redis già nel stack) → **€6-9/mese**
- **Semantic cache:** Risposta identica per query simili → **€5-7/mese**
- **Batch processing:** Aggregare query simili → **€4-6/mese**

**TOTALE OTTIMIZZATO:** ~**€5-10/mese** per AI/LLM 🎯

**Confronto con solo OpenRouter (senza Ollama):**
- 5,000 query × GPT-4: ~€230/mese ❌
- 5,000 query × Claude: ~€35/mese
- **Risparmio hybrid:** ~85-95% 💰

**Fonti:**
- [OpenRouter Pricing](https://openrouter.ai/docs#models)
- [LLM Inference Optimization](https://developer.nvidia.com/blog/mastering-llm-techniques-inference-optimization/)

---

### 3.4 Alternative GPU Cloud (Se Serve)

**Solo se >20K query/mese o latency critica <1sec:**

| Provider | GPU | Prezzo/ora | Prezzo/mese (24/7) | Note |
|----------|-----|------------:|-------------------:|------|
| **Hetzner GEX44** | RTX 4000 20GB | - | €184 | Dedicated, EU |
| **RunPod Spot** | RTX 4090 | $0.34 | ~€242 | Spot (interruptible) |
| **Vast.ai Spot** | RTX 4090 | $0.20-0.40 | ~€146-292 | Marketplace, variabile |
| **RunPod On-Demand** | H100 80GB | $1.99 | ~€1,458 | Overkill per 5K utenti |

**Raccomandazione:** ❌ **NON necessario per 2-5K utenti**. Usare CPU Ollama.

**Fonti:**
- [Hetzner GPU Servers](https://www.hetzner.com/dedicated-rootserver/matrix-gpu/)
- [RunPod Pricing](https://www.runpod.io/pricing)

---

### 3.5 Quantizzazione Modelli - Massimo Risparmio VRAM

**Tecniche per ridurre requisiti hardware:**

| Modello | FP16 (16-bit) | INT8 (8-bit) | INT4 (4-bit) | Qualità |
|---------|---------------|--------------|--------------|---------|
| **Llama 3.1 8B** | 16 GB | 8 GB | **4.5 GB** | -2% accuracy |
| **Mistral 7B** | 14 GB | 7 GB | **4 GB** | -1% accuracy |
| **Llama 3.1 70B** | 140 GB | 70 GB | 35 GB | -3% accuracy |

**Formato raccomandato:** **GGUF Q4_K_M** (4-bit, bilanciato qualità/size)

**Strumenti:**
- `ollama pull llama3.1:8b-instruct-q4_K_M` (auto-download quantizzato)
- llama.cpp per custom quantization

**Risparmio:** 4x meno RAM/VRAM → **run su CPU senza GPU!** 🎯

**Fonti:**
- [GGUF vs GPTQ Quantization](https://newsletter.maartengrootendorst.com/p/which-quantization-method-is-right)
- [Visual Guide to Quantization](https://www.maartengrootendorst.com/blog/quantization/)

---

## 4. Dominio, DNS, SSL

### 4.1 Dominio

**Opzioni economiche per www.meeple-ai.it o simili:**

| Estensione | Registrar | Prezzo/anno | Rinnovo/anno | Note |
|------------|-----------|------------:|-------------:|------|
| **.it** | Cloudflare | €9 | €9 | Italia, best choice |
| **.com** | Cloudflare | €9.77 | €9.77 | Globale, popolare |
| **.dev** | Cloudflare | €12.66 | €12.66 | Tech-friendly, HTTPS required |
| **.io** | Cloudflare | €35 | €35 | Startup vibe, più costoso |
| **.ai** | Cloudflare | €140 | €140 | ❌ Troppo costoso |

**Raccomandazione:** **.it** o **.com** → **€9-10/anno** (~€0.75-0.85/mese)

**Cloudflare Registrar:** Zero markup, wholesale price, nessun costo nascosto.

**Fonti:**
- [Cloudflare Domain Pricing](https://cfdomainpricing.com/)
- [Cloudflare .ai Domains](https://www.cloudflare.com/application-services/products/registrar/buy-ai-domains/)

---

### 4.2 DNS

**Opzione 1: Cloudflare DNS** ⭐ **RACCOMANDATO**
- **Costo:** €0 (gratis su tutti i piani)
- **Performance:** ~15-30ms globally
- **DDoS protection:** Inclusa
- **Analytics:** Base incluse

**Opzione 2: Route53 (AWS)**
- **Costo:** $0.50/zona + $0.40/million query → ~€2-5/mese
- Utile solo se già su AWS

**Raccomandazione:** **Cloudflare Free** → **€0/mese**

**Fonte:** [Cloudflare Plans](https://www.cloudflare.com/plans/)

---

### 4.3 SSL/TLS

**Opzione 1: Let's Encrypt** ⭐ **RACCOMANDATO**
- **Costo:** €0 (gratis)
- **Auto-renewal:** Certbot o Cloudflare
- **Wildcard support:** Sì
- **Trust:** Tutti i browser

**Opzione 2: Cloudflare SSL**
- **Costo:** €0 (incluso in Free plan)
- **Universal SSL:** Automatico
- **Tipo:** Edge certificates

**Raccomandazione:** **Let's Encrypt o Cloudflare** → **€0/mese**

**TOTALE Dominio + DNS + SSL:** ~**€0.75-1/mese** (~€9-12/anno)

---

## 5. Email Transazionale

### 5.1 Stima Volume per 2-5K Utenti

**Email transazionali:**
- Registrazione/verifica: 5,000 × 1 = 5,000/mese
- Password reset: 5,000 × 0.05 = 250/mese
- Notifiche: 5,000 × 2 = 10,000/mese
- **Totale:** ~**15,000-20,000 email/mese**

### 5.2 Opzioni Provider

| Provider | Free Tier | Oltre Free Tier | Costo 20K email |
|----------|-----------|-----------------|-----------------|
| **AWS SES** | 3,000/mese (se su EC2: 62K) | $0.10/1,000 | €1.70 |
| **Mailgun** | 5,000/mese | €30/10K (poi $0.30/1K) | ~€30 |
| **SendGrid** | 100/giorno = 3,000/mese | $19.95/mese (40K) | ~€18.50 |
| **Resend** | 3,000/mese | $20/50K | €0 (sotto free) |
| **Brevo** | 300/giorno = 9,000/mese | €25/20K | ~€25 |

**Raccomandazione:**
- **Se già su AWS:** AWS SES → **€1.70/mese** (best price)
- **Se Hetzner/DigitalOcean:** Resend Free tier → **€0/mese**
- **Se >20K/mese:** AWS SES o SendGrid → **€2-20/mese**

**Fonti:**
- [AWS SES vs SendGrid](https://www.courier.com/integrations/compare/amazon-ses-vs-sendgrid)
- [Transactional Email Pricing 2025](https://www.notificationapi.com/blog/transactional-email-apis)

---

## 6. CDN e Banda

### 6.1 Stima Traffico 2-5K Utenti

**Assunzioni:**
- 5,000 utenti/mese
- 30% utenti attivi = 1,500
- Pagine/utente: 20/mese
- Dimensione pagina: 2 MB (con assets)
- **Totale:** 1,500 × 20 × 2 MB = **60 GB/mese egress**

**Asset statici (CSS/JS/images):** ~40 GB
**API response:** ~20 GB

### 6.2 Opzioni CDN

| Provider | Free Tier | Banda/GB | Costo 60 GB |
|----------|-----------|----------|-------------|
| **Cloudflare Free** | Unlimited | €0 | **€0** ⭐ |
| **BunnyCDN** | No free tier | €0.01/GB (EU) | €0.60 |
| **CloudFront (AWS)** | 1 TB/anno free | $0.085/GB | €4.69 |
| **Vercel** | 100 GB/mese | $0.15/GB extra | €0 (sotto free) |

**Raccomandazione:** **Cloudflare Free** → **€0/mese** 🎯

**Banda cloud provider inclusa:**
- Hetzner: 20 TB/mese inclusi (no costi extra)
- DigitalOcean: 1-6 TB/mese inclusi per droplet
- AWS: Primi 100 GB gratis, poi $0.09/GB

**TOTALE CDN + Banda:** **€0-5/mese**

**Fonti:**
- [Cloudflare CDN Pricing](https://www.cloudflare.com/plans/)
- [BunnyCDN Pricing](https://bunny.net/pricing/)

---

## 7. Storage e Backup

### 7.1 Storage Persistente

**Necessità:**
- Database PostgreSQL: 50 GB
- Qdrant vectors: 20 GB
- Prometheus metrics (30d): 20 GB
- Logs (Seq 30d): 10 GB
- n8n workflows: 5 GB
- **Totale:** ~**105 GB** (crescita ~10 GB/mese)

**Opzioni:**

| Provider | Tipo | Prezzo/GB/mese | Costo 105 GB |
|----------|------|----------------|--------------|
| **Hetzner Volume** | Block storage | €0.119 | €12.50 |
| **DigitalOcean Volume** | Block storage | €0.10 | €10.50 |
| **AWS EBS gp3** | Block storage | €0.106 | €11.13 |
| **Backblaze B2** | Object storage | $0.005 (~€0.0046) | €0.48 |

**Raccomandazione:**
- **Primary storage:** Incluso in server (Hetzner 160 GB SSD) → €0
- **Extra volumes:** Hetzner Volumes → €12/mese (se serve)

---

### 7.2 Backup

**Strategia:**
- Database backup: giornaliero, 7 giorni retention
- Volume snapshot: settimanale, 4 settimane retention
- Configurazioni: versioned su Git

**Opzioni:**

| Provider | Tipo | Prezzo | Costo per 100 GB |
|----------|------|--------|------------------|
| **Hetzner Snapshot** | Block snapshot | €0.0119/GB | €1.19/snapshot × 4 = €4.76 |
| **Backblaze B2** | Object storage | $0.005/GB | €0.46/mese |
| **AWS S3 Glacier** | Cold storage | $0.004/GB (~€0.0037) | €0.37/mese |

**Setup raccomandato:**
- Backblaze B2: 100 GB backup → **€0.50/mese**
- Script backup automatico con restic/rclone

**TOTALE Storage + Backup:** **€5-15/mese**

**Fonti:**
- [Backblaze B2 Pricing](https://www.backblaze.com/b2/cloud-storage-pricing.html)
- [AWS S3 Glacier Pricing](https://aws.amazon.com/s3/pricing/)

---

## 8. Monitoring e Error Tracking

### 8.1 Self-Hosted (Stack Esistente) ⭐ **RACCOMANDATO INIZIALE**

**Già incluso nella configurazione:**
- Prometheus + Grafana: metriche (€0, self-hosted)
- Seq: structured logging (€0, self-hosted)
- (opzionale) Jaeger: tracing se serve debug

**Costo:** **€0/mese** (incluso in compute)

**Pro:**
- Zero costi aggiuntivi
- Privacy completa
- Customizzazione totale

**Contro:**
- No alerting mobile app (solo email/webhook)
- Devi gestire uptime monitoring stack stesso
- Dashboard da configurare

---

### 8.2 Error Tracking SaaS (Opzionale)

**Per errori applicazione lato client/server:**

| Provider | Free Tier | Oltre Free | Costo 5K utenti |
|----------|-----------|------------|-----------------|
| **Sentry** | 5K events/mese | $29/mese (50K) | €0-27 |
| **Rollbar** | 5K events/mese, unlimited users | $15/mese (25K) | €0-14 |
| **GlitchTip** (open-source) | Self-hosted gratis | $15/100K events | €0 |
| **Highlight.io** | 1K sessions/mese | $20/10K sessions | ~€18 |

**Raccomandazione:**
- **Fase iniziale:** Self-hosted Seq + Prometheus → **€0**
- **Se serve frontend error tracking:** Sentry Free → **€0-27/mese**

**Fonti:**
- [Sentry Alternatives 2025](https://rollbar.com/blog/sentry-alternatives-for-error-tracking/)
- [Sentry vs Rollbar Pricing](https://trackjs.com/compare/sentry-vs-rollbar/)

---

### 8.3 Uptime Monitoring

**Monitorare che il sito sia up:**

| Provider | Free Tier | Costo/mese |
|----------|-----------|------------|
| **UptimeRobot** | 50 monitors, 5min interval | €0 |
| **Better Uptime** | 10 monitors, 3min interval | €0 (→ €10 Pro) |
| **Healthchecks.io** | 20 checks, cron monitoring | €0 (→ $5 paid) |
| **Pingdom** | Trial, poi $10/mese | €9.25 |

**Raccomandazione:** **UptimeRobot Free** → **€0/mese**

**TOTALE Monitoring:** **€0-30/mese** (€0 se tutto self-hosted)

---

## 9. Altri Servizi

### 9.1 Autenticazione OAuth (Opzionale)

**Già implementato nel sistema:**
- Google OAuth: gratis
- GitHub OAuth: gratis
- Discord OAuth: gratis

**Costo:** **€0/mese**

---

### 9.2 Analytics (Opzionale)

**Privacy-friendly alternatives a Google Analytics:**

| Provider | Free Tier | Note |
|----------|-----------|------|
| **Plausible** (self-hosted) | Gratis | Open-source, GDPR |
| **Umami** (self-hosted) | Gratis | Open-source, lightweight |
| **Plausible Cloud** | $9/10K pageviews | €8.30/mese |
| **Fathom** | $14/100K pageviews | €12.90/mese |

**Con 5K utenti × 20 pages = 100K pageviews/mese**

**Raccomandazione:**
- **Self-hosted Umami** → **€0** (deploy su stesso server)
- **Se preferisci managed:** Plausible → **€8-17/mese**

---

### 9.3 APM (Application Performance Monitoring)

**Opzionale per fase iniziale:**

| Provider | Free Tier | Note |
|----------|-----------|------|
| **New Relic** | 100 GB/mese, 1 user | Sufficiente per iniziare |
| **Datadog** | 14-day trial | No free tier sostanziale |
| **Elastic APM** | Self-hosted | Gratis, heavy |

**Raccomandazione:** **New Relic Free** o **skip** (usare Prometheus già presente)

**Costo:** **€0/mese**

---

## 10. Riepilogo Completo Budget 2-5K Utenti

### 10.1 Setup BUDGET (Raccomandato Start) 💰

**Target: <€150/mese**

| Categoria | Soluzione | Costo/mese | Costo/anno |
|-----------|-----------|------------|------------|
| **GitHub** | Free plan | €0 | €0 |
| **Hosting** | Hetzner Cloud 2× CPX | €70 | €840 |
| **Database** | Self-hosted PostgreSQL | €0 | €0 |
| **AI/LLM** | Ollama CPU + caching | €10 | €120 |
| **Dominio** | .it Cloudflare | €0.75 | €9 |
| **DNS** | Cloudflare Free | €0 | €0 |
| **SSL** | Let's Encrypt | €0 | €0 |
| **Email** | Resend Free / AWS SES | €0-2 | €0-24 |
| **CDN** | Cloudflare Free | €0 | €0 |
| **Banda** | Hetzner included (20TB) | €0 | €0 |
| **Storage** | Hetzner SSD included | €0 | €0 |
| **Backup** | Backblaze B2 | €1 | €12 |
| **Monitoring** | Self-hosted (Prometheus/Seq) | €0 | €0 |
| **Error Tracking** | Sentry Free / Self-hosted | €0 | €0 |
| **Uptime Monitor** | UptimeRobot Free | €0 | €0 |
| **Analytics** | Umami self-hosted | €0 | €0 |
| **TOTALE** | | **€81-83/mese** | **€972-996/anno** |

**✅ Best per:** MVP, bootstrap, 1-2 developer, budget limitato

---

### 10.2 Setup BALANCED (Raccomandato Produzione) ⭐

**Target: €300-400/mese**

| Categoria | Soluzione | Costo/mese | Costo/anno |
|-----------|-----------|------------|------------|
| **GitHub** | Team plan (3 utenti) | €12 | €144 |
| **Hosting** | Hetzner Cloud 2× CPX + managed DB | €100 | €1,200 |
| **Database** | Neon Serverless Pro | €19 | €228 |
| **Redis** | Upstash 1GB | €10 | €120 |
| **AI/LLM** | Ollama + OpenRouter fallback | €20 | €240 |
| **Dominio** | .com Cloudflare | €0.85 | €10 |
| **DNS** | Cloudflare Pro (optional) | €0-20 | €0-240 |
| **SSL** | Cloudflare SSL | €0 | €0 |
| **Email** | AWS SES | €2 | €24 |
| **CDN** | Cloudflare Free | €0 | €0 |
| **Banda** | Included | €0 | €0 |
| **Storage/Backup** | Hetzner + Backblaze | €15 | €180 |
| **Monitoring** | Self-hosted + Sentry paid | €27 | €324 |
| **Uptime Monitor** | Better Uptime Pro | €10 | €120 |
| **Analytics** | Plausible Cloud | €9 | €108 |
| **TOTALE** | | **€224-244/mese** | **€2,688-2,928/anno** |

**✅ Best per:** Production-ready, team 3-5 dev, 2-5K utenti, growth phase

---

### 10.3 Setup PREMIUM (Over-Engineered) 🚀

**Target: €800-1,200/mese**

| Categoria | Soluzione | Costo/mese | Costo/anno |
|-----------|-----------|------------|------------|
| **GitHub** | Team plan (5 utenti) + extra min | €30 | €360 |
| **Hosting** | AWS Fargate (ottimizzato) | €300 | €3,600 |
| **Database** | AWS RDS Multi-AZ | €150 | €1,800 |
| **Redis** | AWS ElastiCache | €50 | €600 |
| **AI/LLM** | Ollama GPU + OpenRouter | €200 | €2,400 |
| **Dominio** | .com + .it | €1 | €12 |
| **DNS** | Cloudflare Pro | €20 | €240 |
| **SSL** | Cloudflare Advanced | €0 | €0 |
| **Email** | SendGrid Pro | €55 | €660 |
| **CDN** | Cloudflare Pro | €20 | €240 |
| **Banda** | AWS egress | €30 | €360 |
| **Storage/Backup** | AWS S3 + Glacier | €40 | €480 |
| **Monitoring** | Datadog / New Relic paid | €100 | €1,200 |
| **Uptime Monitor** | Pingdom | €10 | €120 |
| **Analytics** | Plausible Cloud | €9 | €108 |
| **TOTALE** | | **€1,015/mese** | **€12,180/anno** |

**⚠️ Overkill** per 2-5K utenti! Solo se scaling rapido previsto a >50K utenti.

---

## 11. Ottimizzazioni Costi Chiave

### 11.1 AI/LLM - Risparmio 85-95%

**Strategia:**
1. ✅ **Self-hosted Ollama CPU** per 80% query → €0 API costs
2. ✅ **Quantizzazione 4-bit (GGUF)** → 4x meno VRAM/RAM
3. ✅ **Semantic caching (Redis)** → -40% duplicati
4. ✅ **Batch processing** → aggregare query simili
5. ✅ **OpenRouter fallback** per 20% query complesse

**Risparmio:** Da €200-400/mese (solo API) → **€5-20/mese** (hybrid) = **€180-380/mese salvati**

---

### 11.2 Infrastruttura - Risparmio 60-80%

**Strategia:**
1. ✅ **Hetzner vs AWS** → -70% costi compute
2. ✅ **Self-hosted database** vs managed → -€30-100/mese
3. ✅ **Ridurre stack Observability** in produzione iniziale → -€20-50/mese
4. ✅ **Auto-shutdown staging** fuori orario (16h/giorno off) → -60% costi staging

**Risparmio:** Da €800-1,200/mese (AWS full) → **€70-150/mese** (Hetzner) = **€730-1,050/mese salvati**

---

### 11.3 CDN/Banda - Risparmio 100%

**Strategia:**
1. ✅ **Cloudflare Free** vs CloudFront → €0 vs €30-100/mese
2. ✅ **Hetzner 20 TB inclusi** → niente bandwidth charges
3. ✅ **Image optimization** (WebP, lazy load) → -50% bandwidth

**Risparmio:** **€30-100/mese**

---

### 11.4 Email - Risparmio 80-95%

**Strategia:**
1. ✅ **AWS SES** ($0.10/1K) vs SendGrid ($60/10K) → -80%
2. ✅ **Resend Free tier** per <20K email/mese → €0

**Risparmio:** Da €30-60/mese → **€0-5/mese** = **€25-55/mese salvati**

---

### 11.5 Monitoring - Risparmio 100%

**Strategia:**
1. ✅ **Self-hosted Prometheus/Grafana** vs Datadog (€100+/mese)
2. ✅ **Self-hosted Seq** vs Loggly/Splunk
3. ✅ **Sentry Free tier** (5K events) sufficiente per iniziare

**Risparmio:** **€100-300/mese**

---

## 12. Roadmap Costi per Crescita

### 12.1 Fase 1: MVP/Launch (0-2K utenti, mesi 1-6)

**Budget:** €80-120/mese

**Setup:**
- Hetzner 1× CPX31 (€23.59): tutto in un server
- Ollama CPU solo
- Cloudflare Free everything
- Self-hosted tutto
- GitHub Free

**Team:** 1-2 developer

---

### 12.2 Fase 2: Growth (2-5K utenti, mesi 7-12)

**Budget:** €200-350/mese ⭐ **TARGET ATTUALE**

**Setup:**
- Hetzner 2× CPX41/CPX31 (€70-90)
- Ollama + OpenRouter fallback
- Database managed (Neon Pro €19)
- GitHub Team 3 utenti
- Monitoring basic paid (Sentry €27)

**Team:** 3-5 developer

---

### 12.3 Fase 3: Scale (5-20K utenti, anno 2)

**Budget:** €500-800/mese

**Setup:**
- Hetzner dedicated server (€100-200) o AWS Fargate (€400)
- Managed DB + Redis (€50-100)
- Ollama GPU optional (€184)
- CDN upgrade (Cloudflare Pro €20)
- Monitoring full stack (€50-100)

**Team:** 5-10 developer

---

### 12.4 Fase 4: Enterprise (20-100K utenti, anno 3+)

**Budget:** €1,500-3,000/mese

**Setup:**
- AWS/GCP multi-region
- Kubernetes cluster
- Managed everything
- Full observability stack
- 24/7 support contracts

**Team:** 10-30 developer

---

## 13. Conclusioni e Raccomandazioni

### 13.1 Setup Raccomandato per START (2-5K utenti)

**OPZIONE BALANCED** → ~**€220-280/mese** (~€2,640-3,360/anno)

**Stack:**
- **Hosting:** Hetzner Cloud 2× CPX (€70/mese)
- **Database:** Neon Serverless Pro (€19/mese) o self-hosted (€0)
- **AI/LLM:** Ollama CPU + OpenRouter fallback (€10-20/mese)
- **Email:** AWS SES (€2/mese)
- **Dominio/DNS:** .it + Cloudflare Free (€1/mese)
- **CDN:** Cloudflare Free (€0)
- **Backup:** Backblaze B2 (€1/mese)
- **Monitoring:** Self-hosted + Sentry Free (€0-27/mese)
- **GitHub:** Team 3 utenti (€12/mese)

**Totale Core:** ~€115-135/mese
**Totale con opzionali:** ~€220-280/mese

---

### 13.2 Breakdown Costi per Priorità

**ESSENZIALI (non comprimibili):**
- Hosting compute: €70-100
- Database storage: €10-20
- Dominio: €1
- **Subtotale:** €81-121/mese

**IMPORTANTI (necessari per produzione):**
- AI/LLM: €10-30
- Email transazionale: €0-5
- Backup: €1-5
- GitHub (team): €12
- **Subtotale:** €23-52/mese

**OPZIONALI (nice-to-have):**
- Error tracking paid: €27
- Analytics paid: €9
- Uptime monitoring paid: €10
- Redis managed: €10
- **Subtotale:** €0-56/mese

**TOTALE:** €104-229/mese (a seconda di essenziale vs full-featured)

---

### 13.3 Confronto con Analisi Originale

**Analisi originale (10K+ MAU, full production):**
- Setup: 29 vCPU, 61.5 GB RAM, 15 servizi
- Costo: €1,000-2,500/mese

**Analisi ottimizzata (2-5K utenti):**
- Setup: 10 vCPU, 26 GB RAM, 10 servizi core
- Costo: €220-350/mese

**Risparmio:** ~€750-2,150/mese (~**70-85% meno**) 💰

---

### 13.4 ROI e Break-Even

**Assumendo monetizzazione:**
- Piano Free: €0 (acquisizione utenti)
- Piano Paid: €5-10/utente/mese

**Break-even con setup €280/mese:**
- 28 utenti paganti × €10/mese = €280
- **Target:** 1-2% conversion → serve 1,400-2,800 utenti totali

**Con 5,000 utenti e 2% conversion:**
- 100 utenti × €10 = **€1,000/mese revenue**
- Costi: €280/mese
- **Profitto:** €720/mese (~**72% margin**)

---

### 13.5 Next Steps

**Settimana 1:**
1. ✅ Registrare dominio .it su Cloudflare (€9/anno)
2. ✅ Setup Hetzner Cloud 1× CPX31 per MVP (€23.59/mese)
3. ✅ Deploy stack Docker ridotto (API, Web, DB, Redis, Ollama)
4. ✅ Configurare Cloudflare DNS + SSL

**Settimana 2-4:**
1. ✅ Testare Ollama Llama 3.1 8B Q4 su CPU
2. ✅ Setup OpenRouter fallback con rate limiting
3. ✅ Configurare AWS SES per email
4. ✅ Setup backup automatico Backblaze B2

**Mese 2-3:**
1. ✅ Monitorare costi reali vs stimati
2. ✅ Ottimizzare query LLM (caching, batching)
3. ✅ Aggiungere secondo server Hetzner se necessario
4. ✅ Setup Sentry Free per error tracking

**Mese 4-6:**
1. ✅ Scalare a Hetzner CPX41 se serve (€46.41)
2. ✅ Valutare managed DB (Neon Pro) se load aumenta
3. ✅ GitHub Team plan se team cresce a 3+
4. ✅ Analytics (Plausible/Umami) per metriche crescita

---

## 14. Fonti Complete

### Infrastruttura Cloud
- [Hetzner Cloud Pricing](https://www.hetzner.com/cloud)
- [DigitalOcean Pricing](https://www.digitalocean.com/pricing)
- [AWS Fargate Pricing](https://aws.amazon.com/fargate/pricing/)
- [AWS RDS PostgreSQL Pricing](https://aws.amazon.com/rds/postgresql/pricing/)
- [Neon Serverless Postgres](https://neon.tech/pricing)

### AI/LLM
- [Ollama Multi-User Cost Analysis](https://markaicode.com/ollama-multi-user-cost-analysis/)
- [Ollama Hardware Requirements](https://www.arsturn.com/blog/hardware-requirements-for-running-ollama)
- [Hetzner GPU Servers](https://www.hetzner.com/dedicated-rootserver/matrix-gpu/)
- [RunPod GPU Pricing](https://www.runpod.io/pricing)
- [Vast.ai GPU Marketplace](https://vast.ai/)
- [OpenRouter Pricing](https://openrouter.ai/docs#models)
- [LLM Inference Optimization](https://developer.nvidia.com/blog/mastering-llm-techniques-inference-optimization/)
- [GGUF vs GPTQ Quantization](https://newsletter.maartengrootendorst.com/p/which-quantization-method-is-right)

### Dominio, DNS, Email
- [Cloudflare Domain Pricing](https://cfdomainpricing.com/)
- [Cloudflare Plans](https://www.cloudflare.com/plans/)
- [AWS SES vs SendGrid](https://www.courier.com/integrations/compare/amazon-ses-vs-sendgrid)
- [Transactional Email Pricing](https://www.notificationapi.com/blog/transactional-email-apis)

### CDN, Storage, Backup
- [BunnyCDN Pricing](https://bunny.net/pricing/)
- [Backblaze B2 Pricing](https://www.backblaze.com/b2/cloud-storage-pricing.html)
- [AWS S3 Pricing](https://aws.amazon.com/s3/pricing/)

### Monitoring
- [Sentry Alternatives](https://rollbar.com/blog/sentry-alternatives-for-error-tracking/)
- [Sentry vs Rollbar](https://trackjs.com/compare/sentry-vs-rollbar/)

### GitHub
- [GitHub Pricing](https://github.com/pricing)
- [GitHub Actions Billing](https://docs.github.com/en/billing/managing-billing-for-github-actions/about-billing-for-github-actions)

---

**Documento preparato da:** Claude (Anthropic)
**Per:** MeepleAI Engineering Team
**Ultimo aggiornamento:** 2025-11-22
**Versione:** 2.0 (Ottimizzata per 2-5K utenti)
