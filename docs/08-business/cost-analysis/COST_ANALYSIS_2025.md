# MeepleAI - Analisi Costi Infrastruttura 2025

**Data Analisi:** 2025-11-22
**Versione:** 1.0
**Ambito:** Repository privato GitHub + Infrastruttura Docker (Dev locale, Staging, Production)

---

## Executive Summary

### Costi Mensili Stimati

| Ambiente | Costi Mensili (EUR) | Costi Annuali (EUR) |
|----------|---------------------|---------------------|
| **GitHub** | €4 - €84/utente | €48 - €1,008/utente |
| **Dev (Locale)** | €0 | €0 |
| **Staging** | €150 - €280 | €1,800 - €3,360 |
| **Production** | €450 - €850 | €5,400 - €10,200 |
| **TOTALE (1 utente)** | ~€604 - €1,214 | ~€7,248 - €14,568 |
| **TOTALE (5 utenti)** | ~€620 - €1,270 | ~€7,440 - €15,240 |

**Note:**
- Costi in EUR stimati con conversione da USD (1 USD ≈ 0.92 EUR, gennaio 2025)
- Range basato su provider cloud (AWS/Azure/GCP) e configurazioni
- Non include costi di banda/traffico e storage addizionale

---

## 1. GitHub - Repository Privato & CI/CD

### 1.1 Piani GitHub Disponibili

| Piano | Costo/utente | GitHub Actions Minutes | Caratteristiche Principali |
|-------|--------------|------------------------|---------------------------|
| **Free** | €0 | 2,000 min/mese | Repo privati illimitati, collaboratori illimitati |
| **Team** | €4/mese | 3,000 min/mese | Protected branches, code owners, web support |
| **Enterprise** | €21/mese | 50,000 min/mese | SAML SSO, Advanced security, audit logs |

**Fonte:** [GitHub Pricing](https://github.com/pricing)

### 1.2 Analisi GitHub Actions Usage

Basato sull'analisi del workflow CI (`ci.yml`):

**Job principali:**
- `ci-web-unit`: ~5-8 minuti
- `ci-web-e2e`: ~15-20 minuti
- `ci-api-unit-integration`: ~12-20 minuti
- `ci-api-quality`: ~15-20 minuti
- `ci-api-smoke`: ~8-12 minuti
- `ci-web-a11y`: ~10-15 minuti
- `validate-schemas`: ~2-3 minuti
- `validate-api-codegen`: ~5-8 minuti
- `validate-observability-configs`: ~2-3 minuti
- `ci-api-llm-integration` (nightly): ~25-30 minuti

**Stima per Pull Request:** ~60-80 minuti Linux
**Stima mensile (20 PR + 30 nightly):** ~2,950 minuti

**Moltiplicatori per runner:**
- Linux: 1x
- Windows: 2x
- macOS: 10x

**Raccomandazione:**
- **Piano Free** sufficiente per sviluppo iniziale (2,000 min/mese)
- **Piano Team** consigliato per team produttivo (3,000 min/mese)
- **Costo minuti aggiuntivi:** $0.008/min Linux se si superano i limiti

**Costo stimato GitHub Actions aggiuntivi:**
- Con piano Free: ~950 min extra × $0.008 = **$7.60/mese** (~€7/mese)
- Con piano Team: **€0** (sotto soglia)

**Fonti:**
- [GitHub Actions Billing](https://docs.github.com/en/billing/managing-billing-for-github-actions/about-billing-for-github-actions)
- [GitHub Pricing Calculator](https://github.com/pricing/calculator)

---

## 2. Infrastruttura Docker - Analisi Risorse

### 2.1 Stack Completo (15 servizi)

Dal file `infra/compose.prod.yml`:

| Servizio | Categoria | CPU (vCPU) | RAM (GB) | Storage | Note |
|----------|-----------|------------|----------|---------|------|
| **postgres** | Database | 2 | 4 | ~20-100 GB | PostgreSQL 16, persistent |
| **qdrant** | Vector DB | 2 | 8 | ~10-50 GB | Vector embeddings storage |
| **redis** | Cache | 1 | 2 | ~1-5 GB | Session + cache |
| **ollama** | AI/ML | 4 | 8 | ~10-20 GB | LLM models |
| **embedding-service** | AI/ML | 2 | 4 | ~5-10 GB | Multilingual embeddings |
| **unstructured-service** | AI/ML | 2 | 4 | ~2-5 GB | PDF extraction (Stage 1) |
| **smoldocling-service** | AI/ML | 4 | 8 | ~5-10 GB | VLM 256M (Stage 2) |
| **api** | Application | 4 | 8 | ~2-5 GB | ASP.NET 9 API |
| **web** | Application | 2 | 4 | ~2-5 GB | Next.js 16 frontend |
| **seq** | Observability | 1 | 2 | ~10-30 GB | Structured logging |
| **jaeger** | Observability | 1 | 2 | ~5-20 GB | Distributed tracing |
| **prometheus** | Observability | 2 | 4 | ~50 GB | Metrics (90d retention) |
| **grafana** | Observability | 0.5 | 1 | ~2-5 GB | Dashboards |
| **alertmanager** | Observability | 0.5 | 0.5 | ~1 GB | Alerting |
| **n8n** | Workflow | 1 | 2 | ~2-5 GB | Automation |
| **TOTALE** | | **29 vCPU** | **61.5 GB** | **~127-270 GB** | |

**Note:**
- GPU non richiesta per dev/staging (CPU mode)
- GPU opzionale in production per AI/ML services (SmolDocling, Ollama, Embedding) → costi aggiuntivi significativi

---

## 3. Costi Cloud - Comparazione Provider

### 3.1 AWS (Amazon Web Services)

#### ECS Fargate - US East (N. Virginia)

**Pricing (Linux/x86):**
- vCPU: $0.04048/ora (~€0.037/ora)
- Memory: $0.004445/GB/ora (~€0.0041/GB/ora)
- Storage (EBS gp3): $0.115/GB/mese (~€0.106/GB/mese)

**Production (29 vCPU, 61.5 GB RAM, 270 GB storage):**
- Compute: (29 × $0.04048 + 61.5 × $0.004445) × 730 ore = **$1,057/mese** (~€972/mese)
- Storage: 270 GB × $0.115 = **$31/mese** (~€28.50/mese)
- **Totale Production AWS:** ~**€1,000/mese** (~**€12,000/anno**)

**Staging (50% risorse: 14.5 vCPU, 30 GB RAM, 135 GB storage):**
- Compute: (14.5 × $0.04048 + 30 × $0.004445) × 730 = **$528/mese** (~€486/mese)
- Storage: 135 GB × $0.115 = **$16/mese** (~€14.70/mese)
- **Totale Staging AWS:** ~**€500/mese** (~**€6,000/anno**)

**RDS PostgreSQL (alternativa a container Postgres):**
- db.m6g.large (2 vCPU, 8 GB): ~$0.156/ora × 730 = **$114/mese** (~€105/mese)
- Storage gp3 100 GB: **$11.50/mese** (~€10.60/mese)
- **Totale RDS:** ~**€115/mese**

**Fonti:**
- [AWS Fargate Pricing](https://aws.amazon.com/fargate/pricing/)
- [AWS RDS PostgreSQL Pricing](https://aws.amazon.com/rds/postgresql/pricing/)

---

### 3.2 Azure (Microsoft Azure)

#### Azure Container Instances - West Europe

**Pricing:**
- vCPU: ~$0.0000207/vCPU-second (~€0.000019/vCPU-sec)
- Memory: ~$0.0000023/GB-second (~€0.0000021/GB-sec)
- Storage (Managed Disks Premium SSD): ~$0.145/GB/mese (~€0.133/GB/mese)

**Production (29 vCPU, 61.5 GB RAM):**
- Compute: (29 × $0.0000207 + 61.5 × $0.0000023) × 2,592,000 sec/mese = **$1,923/mese** (~€1,769/mese)
- Storage: 270 GB × $0.145 = **$39/mese** (~€36/mese)
- **Totale Production Azure:** ~**€1,805/mese** (~**€21,660/anno**)

**Nota:** Azure Container Instances è significativamente più costoso per carichi 24/7. **Azure Container Apps** o **AKS** offrono prezzi migliori per production.

**Azure Container Apps (alternativa consigliata):**
- Consumption plan con ~40% risparmio su ACI
- **Stima Production:** ~**€1,050/mese** (~**€12,600/anno**)

**Fonti:**
- [Azure Container Instances Pricing](https://azure.microsoft.com/en-us/pricing/details/container-instances/)
- [Azure Container Apps Pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)

---

### 3.3 Google Cloud Platform (GCP)

#### Cloud Run - Tier 1 (us-central1, europe-west1)

**Pricing:**
- vCPU: $0.00002400/vCPU-second (~€0.000022/vCPU-sec)
- Memory: $0.00000250/GiB-second (~€0.0000023/GiB-sec)
- Requests: $0.40/million (primi 2M gratis)
- Storage (Persistent Disk SSD): $0.17/GB/mese (~€0.156/GB/mese)

**Production (29 vCPU, 61.5 GB RAM, always-on):**
- Compute: (29 × $0.00002400 + 61.5 × $0.00000250) × 2,592,000 sec/mese = **$2,203/mese** (~€2,027/mese)
- Storage: 270 GB × $0.17 = **$46/mese** (~€42/mese)
- **Totale Production GCP:** ~**€2,069/mese** (~**€24,828/anno**)

**Nota:** Cloud Run è ottimizzato per carichi intermittenti con auto-scaling. Per 24/7 sempre attivo, **GKE Autopilot** offre prezzi migliori.

**GKE Autopilot (alternativa consigliata):**
- vCPU: $0.058/ora (~€0.053/ora)
- Memory: $0.0065/GB/ora (~€0.006/GB/ora)
- **Stima Production:** (29 × $0.058 + 61.5 × $0.0065) × 730 = **$1,522/mese** (~**€1,400/mese**, ~**€16,800/anno**)

**Fonti:**
- [Google Cloud Run Pricing](https://cloud.google.com/run/pricing)
- [Google Cloud Pricing Calculator](https://cloud.google.com/products/calculator)

---

## 4. Comparazione Costi per Ambiente

### 4.1 Production (29 vCPU, 61.5 GB RAM, 270 GB storage)

| Provider | Servizio | Costo Mensile (EUR) | Costo Annuale (EUR) | Note |
|----------|----------|---------------------|---------------------|------|
| **AWS** | ECS Fargate | €972 | €11,664 | + €28.50 storage |
| **AWS** | Fargate Spot (70% discount) | €292 | €3,499 | Interrupt-tolerant |
| **Azure** | Container Apps | €1,050 | €12,600 | Consigliato su ACI |
| **GCP** | GKE Autopilot | €1,400 | €16,800 | Consigliato su Cloud Run |
| **Hetzner** | Dedicated Servers | €250-400 | €3,000-4,800 | Self-managed, EU |
| **DigitalOcean** | Droplets | €300-500 | €3,600-6,000 | Managed K8s disponibile |

**Raccomandazione Production:**
1. **AWS Fargate** (best balance costo/features): ~**€1,000/mese**
2. **Hetzner Dedicated** (lowest cost, self-managed): ~**€350/mese**
3. **DigitalOcean** (simple, predictable): ~€400/mese

---

### 4.2 Staging (50% Production: 14.5 vCPU, 30 GB RAM, 135 GB storage)

| Provider | Servizio | Costo Mensile (EUR) | Costo Annuale (EUR) |
|----------|----------|---------------------|---------------------|
| **AWS** | ECS Fargate | €486 | €5,832 |
| **Azure** | Container Apps | €525 | €6,300 |
| **GCP** | GKE Autopilot | €700 | €8,400 |
| **Hetzner** | Cloud Servers | €120-200 | €1,440-2,400 |
| **DigitalOcean** | Droplets | €150-250 | €1,800-3,000 |

**Raccomandazione Staging:**
1. **Hetzner Cloud** (best value): ~**€150/mese**
2. **DigitalOcean** (simple): ~€200/mese
3. **AWS Fargate** (production parity): ~€500/mese

---

### 4.3 Dev (Locale)

**Costo:** €0/mese

L'ambiente di sviluppo gira completamente in locale con Docker Compose (`docker-compose.yml`):
- Richiede workstation con minimo 16 GB RAM (32 GB consigliati)
- ~20-30 GB storage per Docker images/volumes
- No costi cloud

**Hardware consigliato developer:**
- CPU: 8+ cores (Intel i7/AMD Ryzen 7 o superiore)
- RAM: 32 GB
- Storage: 512 GB SSD (minimo 256 GB disponibili)

---

## 5. Costi Aggiuntivi da Considerare

### 5.1 Storage Persistente

#### Database PostgreSQL
- **Production:** 50-100 GB iniziali, crescita ~5-10 GB/mese
- **Costo (AWS gp3):** €0.106/GB/mese
- **Stima mensile:** €5-10/mese

#### Qdrant Vector Database
- **Production:** 20-50 GB iniziali, crescita ~2-5 GB/mese
- **Costo:** €0.106/GB/mese
- **Stima mensile:** €2-5/mese

#### Prometheus Metrics (90 giorni retention)
- **Production:** 50 GB (configurato max size)
- **Costo:** €5.30/mese

#### Logs (Seq)
- **Production:** 20-30 GB/mese
- **Costo:** €2-3/mese

**Totale Storage Aggiuntivo:** ~**€15-25/mese**

---

### 5.2 Banda e Traffico

**Stime basate su 10,000 MAU (utenti attivi mensili):**

#### AWS
- Primi 100 GB/mese: gratis
- Successivi 9.9 TB/mese: $0.085/GB (~€0.078/GB)
- **Stima (500 GB/mese):** 400 GB × €0.078 = **€31/mese**

#### Azure
- Primi 100 GB/mese: gratis
- Successivi: ~$0.08/GB (~€0.074/GB)
- **Stima:** ~**€30/mese**

#### GCP
- Primi 100 GB/mese: gratis
- Successivi (Premium Tier): $0.12/GB (~€0.11/GB)
- **Stima:** ~**€44/mese**

**Totale Banda Stimato:** ~**€30-45/mese** (per 10K MAU)

---

### 5.3 Servizi AI Esterni (OpenRouter)

Dal `CLAUDE.md`: il sistema usa OpenRouter per LLM multi-model.

**Modelli utilizzati:**
- GPT-4 Turbo: $0.01/1K input tokens, $0.03/1K output tokens
- Claude 3.5 Sonnet: $0.003/1K input tokens, $0.015/1K output tokens

**Stima per 10,000 query/mese (media 2K input, 1K output tokens):**
- GPT-4: 10K × (2 × $0.01 + 1 × $0.03) = **$500/mese** (~€460/mese)
- Claude: 10K × (2 × $0.003 + 1 × $0.015) = **$75/mese** (~€69/mese)

**Raccomandazione:** Usare mix di modelli con fallback per ottimizzare costi.

**Stima OpenRouter:** ~**€100-500/mese** (dipende da traffico e mix modelli)

**Fonte:** [OpenRouter Pricing](https://openrouter.ai/docs#models)

---

### 5.4 Backup e Disaster Recovery

#### AWS S3 Backup
- Database backups: ~100 GB
- S3 Standard: $0.023/GB/mese (~€0.021/GB/mese)
- **Costo:** €2.10/mese

#### Cross-Region Replication (opzionale)
- Per alta disponibilità (Multi-AZ/Multi-Region)
- **Costo aggiuntivo:** +50-100% costi compute

**Totale Backup:** ~**€2-5/mese** (single region)

---

### 5.5 Certificati SSL/TLS

**Opzioni:**
1. **Let's Encrypt:** Gratis (auto-renew con certbot)
2. **AWS Certificate Manager:** Gratis (per servizi AWS)
3. **Certificati commerciali:** €50-200/anno (opzionale)

**Raccomandazione:** Let's Encrypt / AWS ACM (€0/mese)

---

### 5.6 Monitoraggio e Alerting

**Servizi inclusi nello stack:**
- Prometheus: self-hosted (già conteggiato)
- Grafana: self-hosted (già conteggiato)
- Jaeger: self-hosted (già conteggiato)
- Seq: self-hosted (già conteggiato)

**Alerting esterno (opzionale):**
- PagerDuty: $21-41/utente/mese
- Opsgenie: $9-29/utente/mese

**Raccomandazione:** Usare Alertmanager self-hosted + Email/Slack (€0/mese)

---

## 6. Scenari di Costo Totali

### Scenario 1: Startup MVP (1-2 developer)

**GitHub:**
- Piano: Free
- Utenti: 2
- Actions: Free tier (2,000 min/mese)
- **Costo:** €0/mese

**Infrastruttura:**
- Dev: Locale (€0)
- Staging: DigitalOcean 2 Droplets ($24 + $48) (~€66/mese)
- Production: Hetzner Dedicated AX52 (€59/mese)
- Storage: Incluso
- **Costo:** €125/mese

**AI (OpenRouter):**
- Traffico basso (~1,000 query/mese)
- **Costo:** €10-20/mese

**TOTALE SCENARIO 1:** ~**€135-145/mese** (~**€1,620-1,740/anno**)

---

### Scenario 2: Scale-Up (5 developer, 10K MAU)

**GitHub:**
- Piano: Team
- Utenti: 5
- Actions: 3,000 min/mese inclusi
- **Costo:** €20/mese (5 × €4)

**Infrastruttura:**
- Dev: Locale (€0)
- Staging: Hetzner Cloud (~€150/mese)
- Production: AWS Fargate (~€1,000/mese)
- Storage aggiuntivo: €20/mese
- Banda: €35/mese
- **Costo:** €1,205/mese

**AI (OpenRouter):**
- Traffico medio (~10,000 query/mese)
- **Costo:** €100-200/mese

**Backup:**
- S3 + snapshot: €5/mese

**TOTALE SCENARIO 2:** ~**€1,330-1,430/mese** (~**€15,960-17,160/anno**)

---

### Scenario 3: Production Ready (10+ developer, 50K MAU)

**GitHub:**
- Piano: Team (o Enterprise per >50 utenti)
- Utenti: 10
- Actions: Team plan (possibili minuti aggiuntivi ~€20/mese)
- **Costo:** €60/mese (10 × €4 + €20 extra minutes)

**Infrastruttura:**
- Dev: Locale (€0)
- Staging: AWS Fargate ~€500/mese
- Production: AWS Fargate + RDS Multi-AZ (~€2,000/mese)
- Storage aggiuntivo: €50/mese
- Banda: €150/mese
- **Costo:** €2,700/mese

**AI (OpenRouter):**
- Traffico alto (~50,000 query/mese)
- **Costo:** €500-1,000/mese

**Backup + DR:**
- S3 cross-region + snapshot: €20/mese

**Monitoring:**
- PagerDuty (3 utenti): €90/mese

**TOTALE SCENARIO 3:** ~**€3,370-3,870/mese** (~**€40,440-46,440/anno**)

---

## 7. Ottimizzazioni Costi

### 7.1 Infrastruttura

1. **Usare Fargate Spot (AWS):** -70% su workload non critici
   - Risparmio: ~€700/mese su production

2. **Reserved Instances / Savings Plans:**
   - 1 anno commitment: -30-40%
   - 3 anni commitment: -50-60%
   - Risparmio: ~€300-600/mese su production

3. **Ridurre stack Observability in staging:**
   - Disabilitare Jaeger/Prometheus in staging
   - Risparmio: ~€50-100/mese

4. **Auto-scaling intelligente:**
   - Scale down notturno (50% risorse 18:00-06:00)
   - Risparmio: ~€200-300/mese

5. **Considerare Hetzner/OVH per EU:**
   - Dedicated servers con rapporto prezzo/performance migliore
   - Risparmio: ~€600-800/mese vs AWS

---

### 7.2 AI/LLM

1. **Self-hosted LLM per query semplici:**
   - Ollama con Llama 3.1 8B per FAQ/simple queries
   - Risparmio: ~€200-400/mese

2. **Caching aggressivo risposte:**
   - Redis cache con TTL 24h per query comuni
   - Risparmio: ~€100-200/mese

3. **Mix modelli intelligente:**
   - Claude Haiku ($0.00025/$0.00125) per query semplici
   - GPT-4 solo per query complesse
   - Risparmio: ~€150-300/mese

4. **Batch processing:**
   - Aggregare richieste simili
   - Risparmio: ~€50-100/mese

---

### 7.3 GitHub Actions

1. **Self-hosted runners:**
   - DigitalOcean Droplet $48/mese = illimitati minuti
   - Risparmio: €20-100/mese (su Team plan)

2. **Ottimizzare workflow:**
   - Cache più aggressivo (NuGet, npm, Docker layers)
   - Parallelizzazione job
   - Skip test su docs-only changes
   - Risparmio tempo: -20-30% execution time

3. **Scheduled jobs su self-hosted:**
   - Nightly builds su runner dedicato
   - Risparmio: €10-30/mese

---

### 7.4 Storage

1. **Lifecycle policies:**
   - S3 Glacier per backup >30 giorni
   - Risparmio: -80% su backup storage

2. **Compression:**
   - Brotli/Gzip per logs (già implementato)
   - Prometheus compaction
   - Risparmio: -40-60% storage

3. **Retention policies:**
   - Logs: 30 giorni invece di 90
   - Metrics: 60 giorni invece di 90
   - Risparmio: €10-20/mese

---

## 8. Raccomandazioni Finali

### 8.1 Setup Consigliato per Fase

#### Fase MVP/Alpha (Current)
- **GitHub:** Free plan (€0)
- **Dev:** Locale (€0)
- **Staging:** Hetzner Cloud CX31 (€10/mese)
- **Production:** Hetzner Dedicated AX52 (€59/mese) o AWS Fargate Spot (€300/mese)
- **AI:** OpenRouter mix models (€50-100/mese)
- **TOTALE:** ~**€120-170/mese**

#### Fase Beta (Pre-Production)
- **GitHub:** Team plan 3-5 utenti (€12-20/mese)
- **Dev:** Locale (€0)
- **Staging:** Hetzner Cloud / DigitalOcean (€150/mese)
- **Production:** AWS Fargate (€1,000/mese)
- **AI:** OpenRouter + Ollama fallback (€150-300/mese)
- **Monitoring:** Self-hosted (€0)
- **TOTALE:** ~**€1,312-1,470/mese**

#### Fase Production (10K MAU target)
- **GitHub:** Team plan 10 utenti (€40/mese)
- **Dev:** Locale (€0)
- **Staging:** AWS Fargate 50% (€500/mese)
- **Production:** AWS Fargate + RDS + ElastiCache (€1,500/mese)
- **AI:** OpenRouter ottimizzato + self-hosted (€200-500/mese)
- **Banda:** €50-100/mese
- **Backup:** €20/mese
- **TOTALE:** ~**€2,310-2,660/mese**

---

### 8.2 Priorità Ottimizzazioni

**Immediate (Fase MVP):**
1. ✅ Usare GitHub Free plan (già fatto)
2. ✅ Self-hosted observability (già implementato)
3. ⚠️ Considerare Hetzner per staging/production (valutare)

**Short-term (Fase Beta):**
1. Implementare Ollama fallback per LLM (riduce costi OpenRouter)
2. Cache aggressivo risposte RAG (Redis, già presente)
3. GitHub Team plan per protected branches

**Mid-term (Production):**
1. AWS Reserved Instances / Savings Plans (-30-40%)
2. Self-hosted runners GitHub Actions
3. Auto-scaling intelligente (orari off-peak)
4. Mix LLM models per costo/qualità

**Long-term (Scale):**
1. Multi-region deployment (backup/failover)
2. CDN per static assets (CloudFront/Cloudflare)
3. Database read replicas
4. Kubernetes cluster (migrazione da Fargate se scale >100K MAU)

---

## 9. Fonti e Riferimenti

### GitHub
- [GitHub Pricing](https://github.com/pricing)
- [GitHub Actions Billing](https://docs.github.com/en/billing/managing-billing-for-github-actions/about-billing-for-github-actions)
- [GitHub Pricing Calculator](https://github.com/pricing/calculator)

### AWS
- [AWS Fargate Pricing](https://aws.amazon.com/fargate/pricing/)
- [AWS RDS PostgreSQL Pricing](https://aws.amazon.com/rds/postgresql/pricing/)
- [AWS Fargate Pricing Explained (2025)](https://cloudchipr.com/blog/aws-fargate-pricing)

### Azure
- [Azure Container Instances Pricing](https://azure.microsoft.com/en-us/pricing/details/container-instances/)
- [Azure Container Apps Pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/)

### Google Cloud
- [Google Cloud Run Pricing](https://cloud.google.com/run/pricing)
- [Google Cloud Pricing Calculator](https://cloud.google.com/products/calculator)

### Alternative Provider
- [Hetzner Cloud Pricing](https://www.hetzner.com/cloud)
- [DigitalOcean Pricing](https://www.digitalocean.com/pricing)

---

## 10. Conclusioni

### Riepilogo Costi Repository Privato + Infrastruttura

| Fase | GitHub | Infra | AI | Totale Mensile | Totale Annuale |
|------|--------|-------|----|--------------:|---------------:|
| **MVP/Alpha** | €0-7 | €70-170 | €50-100 | **€120-277** | **€1,440-3,324** |
| **Beta** | €12-40 | €650-1,150 | €150-300 | **€812-1,490** | **€9,744-17,880** |
| **Production** | €40-100 | €2,000-2,500 | €200-500 | **€2,240-3,100** | **€26,880-37,200** |

### Key Takeaways

1. **Repository privato GitHub è incluso in TUTTI i piani** (anche Free)
2. **GitHub Actions** è il costo variabile principale per CI/CD:
   - Free plan: 2,000 min/mese (sufficiente per MVP)
   - Team plan: 3,000 min/mese a €4/utente (raccomandato)

3. **Infrastruttura Docker** è il costo principale:
   - AWS Fargate: best balance features/costo (~€1,000/mese production)
   - Hetzner: lowest cost (~€60-350/mese, self-managed)
   - Azure/GCP: più costosi per setup 24/7

4. **AI/LLM** può superare i costi infra con traffico elevato:
   - Ottimizzazione cruciale: self-hosted Ollama + OpenRouter fallback
   - Caching aggressivo riduce 40-60% chiamate LLM

5. **Ottimizzazioni possono ridurre costi del 40-70%:**
   - Reserved Instances / Savings Plans
   - Fargate Spot per workload non critici
   - Self-hosted runners GitHub Actions
   - Hetzner/OVH per EU deployment

### Next Steps

1. **Fase corrente (Alpha):** Mantenere setup attuale (€120-170/mese)
2. **Pre-Beta (2-3 mesi):** Valutare Hetzner staging + AWS production (€800-1,200/mese)
3. **Production (6 mesi):** AWS Fargate + Reserved Instances + ottimizzazioni (€1,800-2,500/mese)

---

**Documento preparato da:** Claude (Anthropic)
**Per:** MeepleAI Engineering Team
**Ultimo aggiornamento:** 2025-11-22
