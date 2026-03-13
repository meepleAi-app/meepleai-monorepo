# ADR-045: Multi-Region Scaling Strategy

**Status**: Accepted
**Date**: 2026-03-10
**Issue**: #108 (Epic #26: Multi-Region Preparation)
**Decision Makers**: Engineering Lead
**References**: ADR-007 (Hybrid LLM), ADR-043 (LLM NFR), `docs/plans/2026-03-08-llm-system-improvement-prd.md` (Epic G)

---

## 1. Context & Current State

MeepleAI operates as a single-node Docker Compose deployment hosted in Italy. The system serves an AI-powered board game assistant with RAG, multi-agent orchestration, and living documentation.

**Current facts** (as of 2026-03-10):
- **Traffic**: 0% non-EU. No business pressure for multi-region.
- **Infrastructure**: Single VPS running Docker Compose (~$30-50/mo)
- **Stack**: PostgreSQL 16 + Redis + Qdrant + Ollama + .NET 9 API + Next.js frontend
- **Data residency**: All data in EU (Italy), GDPR-compliant by default

This ADR documents the phased scaling strategy **before** it's needed, so that when triggers are hit, the team has a clear playbook requiring <1 sprint of routing modifications.

## 2. Current Architecture Assumptions (Single-Region Constraints)

These implicit locality assumptions **will break** under multi-region deployment and must be resolved before scaling beyond Phase 1.

| # | Assumption | Component | File(s) | Resolution Required |
|---|-----------|-----------|---------|---------------------|
| A1 | In-memory circuit breaker state (Interlocked fields) | `CircuitBreakerState` | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Services/LlmManagement/CircuitBreakerState.cs` | Move to Redis — pods currently disagree on circuit status |
| A2 | Local Redis with no namespace partitioning | `FreeModelQuotaTracker`, `OpenRouterRateLimitTracker` | `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/FreeModelQuotaTracker.cs`, `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Services/OpenRouterRateLimitTracker.cs` | Namespace keys by region or share single Redis with latency budget |
| A3 | Single PostgreSQL instance | All 15 bounded contexts | `apps/api/src/Api/Infrastructure/MeepleAiDbContext.cs` | Read replicas + PgBouncer for connection pooling |
| A4 | Local Qdrant (Docker volume) | Vector search | Qdrant client configuration | Qdrant Cloud or cross-region replication (50ms local vs 200ms+ cross-region) |
| A5 | Co-located Ollama | `OllamaLlmClient` | `apps/api/src/Api/Services/LlmClients/OllamaLlmClient.cs` | GPU node pools on K8s, cold start mitigation (30-60s) |
| A6 | No cross-pod state sharing | Circuit breaker + rate limits | Multiple services across KnowledgeBase BC | Redis-backed distributed state for all shared counters |

### Impact Assessment

| Assumption | Phase 1 Impact | Phase 2 Impact | Phase 3+ Impact |
|-----------|---------------|---------------|-----------------|
| A1 (Circuit breaker) | None | **CRITICAL** — pods disagree | **CRITICAL** |
| A2 (Redis keys) | None | Medium — split rate limits | **HIGH** — cross-region splits |
| A3 (Single DB) | None | **HIGH** — connection exhaustion | **CRITICAL** — latency |
| A4 (Local Qdrant) | None | Medium — persistent storage | **CRITICAL** — RAG unusable |
| A5 (Ollama GPU) | None | **HIGH** — GPU scheduling | **HIGH** — regional instances |
| A6 (Cross-pod state) | None | **CRITICAL** — same as A1 | **CRITICAL** |

## 3. Scaling Phases with Triggers & Cost Estimates

| Phase | Trigger | Infrastructure | Est. Monthly Cost | Key Changes |
|-------|---------|---------------|-------------------|-------------|
| **Current** | — | Docker Compose single-node (Italy VPS) | ~$30-50 | — |
| **1** | >1K MAU | + Cloudflare CDN for frontend | +$20-50 | Frontend edge caching, API unchanged |
| **2** | >5K MAU (with >=1 AI interaction) | K8s single-cluster EU + managed DB/Redis | +$200-500 | Horizontal scaling, connection pooling |
| **3** | >20K MAU OR >30% non-EU traffic | Multi-region K8s + Qdrant Cloud + OpenRouter primary | +$800-2,000 | Region-aware routing, data replication |
| **4** | >100K MAU | Edge computing + distributed Qdrant + regional Ollama | +$3,000+ | Full edge deployment |

**MAU definition**: Monthly Active Users with >=1 AI interaction (chat, RAG query, agent invocation) — not just page views.

**Cost estimate basis**: Hetzner (EU), Cloudflare (CDN), managed PostgreSQL pricing from major cloud providers as of 2026-03. Order-of-magnitude accuracy only.

### Per-Service Cost Breakdown

| Service | Current | Phase 1 (+CDN) | Phase 2 (K8s EU) | Phase 3 (Multi-Region) | Phase 4 (Edge) |
|---------|---------|----------------|------------------|------------------------|----------------|
| **VPS / K8s nodes** | $15-30 | $15-30 | $150-300 (2-5 nodes) | $400-800 (2-5 nodes × 2 regions) | $800-1,500 |
| **Cloudflare (CDN + DNS)** | $1 (DNS only) | $20-50 (Pro plan) | $20-50 | $20-50 | $50-200 (Workers) |
| **PostgreSQL** | included in VPS | included | $30-80 (managed) | $80-200 (primary + replica/region) | $150-400 |
| **Redis** | included in VPS | included | $15-30 (managed) | $30-60 (per region) | $60-120 |
| **Qdrant** | included in VPS | included | $0 (self-hosted on K8s) | $50-150 (Qdrant Cloud multi-region) | $100-300 |
| **GPU (Ollama)** | $0 (CPU-only) | $0 | $0-100 (optional) | $100-400 (per region) | $200-600 |
| **Embedding/Reranker** | included in VPS | included | included in K8s nodes | +$50-100 (regional instances) | +$100-200 |
| **Cross-region bandwidth** | $0 | $0 | $0 | $20-100 (DB replication + vector sync) | $50-200 |
| **Observability** | included in VPS | included | $20-50 (managed metrics) | $50-100 (per region) | $100-200 |
| **TOTAL** | **~$20-50** | **~$40-100** | **~$250-650** | **~$850-2,050** | **~$1,650-3,700** |

> **Note**: Estimates are based on 2026-03 pricing from Hetzner, Cloudflare, and Qdrant Cloud. GPU costs vary significantly by provider and availability. Actual costs will depend on provider selection (evaluated at Phase 2 trigger) and real traffic patterns.

## 4. AI Service Placement Strategy

Each AI/ML service has different resource requirements and latency sensitivities that drive placement decisions across scaling phases.

### Service Placement Matrix

| Service | Current | Phase 1 (CDN) | Phase 2 (K8s EU) | Phase 3 (Multi-Region) | Phase 4 (Edge) |
|---------|---------|---------------|------------------|------------------------|----------------|
| **Ollama** (LLM) | Co-located, CPU-only | Unchanged | K8s pod, optional GPU node pool | Regional GPU pools, model pre-loading | Edge GPU per region |
| **Embedding** (sentence-transformers) | Co-located (port 8000) | Unchanged | K8s pod, GPU optional | Regional instances (same model version) | Regional, pinned version |
| **Reranker** (cross-encoder) | Co-located (port 8003) | Unchanged | K8s pod, CPU sufficient | Regional instances | Regional |
| **Orchestration** (LangGraph) | Co-located (port 8004) | Unchanged | K8s pod | Primary region only (stateful workflows) | Primary region |
| **Unstructured PDF** | Co-located (port 8001) | Unchanged | K8s pod | Primary region only (async processing) | Primary region |
| **SmolDocling** (VLM) | Co-located (port 8002) | Unchanged | K8s pod, GPU beneficial | Primary region only (fallback processor) | Primary region |

### Placement Rationale

**Regional services** (Ollama, Embedding, Reranker): These are on the critical path for user-facing RAG queries. Cross-region latency (100-200ms) is unacceptable for interactive chat. Each region must have local instances.

**Primary-region-only services** (Orchestration, Unstructured, SmolDocling): These handle async or batch workloads (PDF processing, multi-agent workflows). Users upload PDFs infrequently and processing is not latency-sensitive. Running these in a single region simplifies state management and reduces GPU costs.

### Critical Constraint: Embedding Model Consistency

All regions **MUST** run the same embedding model version (`intfloat/multilingual-e5-large`, 1024 dimensions). A version mismatch would produce incompatible vectors, breaking cross-region RAG queries. Mitigation:
- Pin model version in container image (not downloaded at runtime)
- Include model hash in health check response
- Block deployment if model hash differs from primary region

### GPU Resource Strategy

| Phase | GPU Usage | Cost Impact |
|-------|-----------|-------------|
| **Current** | None (CPU-only Ollama) | $0 |
| **Phase 2** | Optional GPU node pool for Ollama | +$0-100/mo (enable when Ollama P95 >3s on CPU) |
| **Phase 3** | GPU per region for Ollama + Embedding | +$100-400/mo per region |
| **Phase 4** | GPU per edge location | +$200-600/mo per location |

**GPU activation trigger**: Enable GPU node pool when Ollama P95 latency exceeds 3,000ms on CPU-only nodes under sustained load (>50 concurrent requests).

## 5. Prerequisites per Phase

### Phase 0 -> Phase 1 (CDN)

Low effort, independent of application changes:

- [ ] Cloudflare DNS + proxy setup for `meepleai.com`
- [ ] Cache rules for static assets (`/_next/static/*`, images)
- [ ] Origin server keep-alive and cache headers
- [ ] SSL/TLS mode: Full (Strict)

### Phase 1 -> Phase 2 (K8s Single-Cluster)

Significant infrastructure migration:

- [ ] **A1**: Circuit breaker state -> Redis (resolve cross-pod disagreement)
- [ ] **A6**: All shared counters -> Redis-backed distributed state
- [ ] **A3**: PgBouncer for connection pooling (PostgreSQL)
- [ ] **A4**: Qdrant migration (Docker volume -> persistent volumes with backup)
- [ ] **A5**: Ollama containerization with health checks, resource limits, GPU node selector
- [ ] Horizontal Pod Autoscaler (HPA) configuration
- [ ] Secret management: Docker `.secret` files -> K8s Secrets (or HashiCorp Vault)
- [ ] Ingress controller with TLS termination (Traefik or nginx)
- [ ] Monitoring: Prometheus + Grafana on K8s (existing recording rules portable)
- [ ] CI/CD: GitHub Actions -> K8s deployment (rolling updates)

### Phase 2 -> Phase 3 (Multi-Region)

Major architectural changes:

- [ ] **Region detection** strategy implemented (see Section 6)
- [ ] **G1**: `UserRegion` field in `LlmRoutingDecision` (already implemented — Issue #107)
- [ ] **Region-aware routing** in `HybridLlmService` / `ILlmRoutingStrategy`
- [ ] Cross-region PostgreSQL replication (or managed DB with read replicas per region)
- [ ] Qdrant Cloud or self-managed cross-region replication
- [ ] Regional Ollama instances with GPU scheduling
- [ ] CDN-based API routing (Cloudflare Workers or similar)
- [ ] Embedding model consistency guarantee (same model version across regions)
- [ ] Data residency compliance per region (GDPR for EU, other regulations)

### Phase 3 -> Phase 4 (Edge)

> **Note**: Phase 4 is speculative. The architecture below will be designed as a separate ADR when Phase 3 is mature and the >100K MAU trigger is approached. The items below capture directional thinking, not commitments.

Advanced distributed systems:

- [ ] Edge computing for latency-sensitive operations (Cloudflare Workers or Deno Deploy for API gateway)
- [ ] Distributed Qdrant with eventual consistency model (shard-per-region, async replication, staleness budget <5s)
- [ ] Regional Ollama with model version pinning (container image includes model weights, no runtime download)
- [ ] Global load balancer with health-based geo-routing (Cloudflare Load Balancing with active health checks)
- [ ] Cross-region event bus for eventual consistency (NATS JetStream or Kafka with geo-aware partitioning)
- [ ] Edge-local embedding + reranker instances (reduce round-trip for RAG queries to <50ms)
- [ ] Write-local-read-global pattern for user data (CRDTs or last-write-wins for non-critical data)

**Key architectural decisions deferred to Phase 4 ADR**:
- Multi-master PostgreSQL vs. write-forwarding to primary
- Edge cache invalidation strategy (event-driven vs TTL)
- Model update coordination across edge locations (blue-green per region)
- Cost optimization: spot/preemptible GPU instances for Ollama

## 6. Region Detection Strategy Candidates

| Strategy | Accuracy | Privacy | Latency | Recommendation |
|----------|----------|---------|---------|----------------|
| GeoIP middleware | High | GDPR concern: IP = PII | ~1ms | Best for accuracy, requires legitimate interest or consent |
| CDN edge header (`CF-IPCountry`) | High | Same GDPR concern (Cloudflare processes IP) | 0ms (pre-computed) | Best if already using Cloudflare |
| User profile setting | Perfect | Explicit consent | 0ms (cached) | Privacy-friendly, requires UI |
| `Accept-Language` header | Low | No PII | 0ms | Unreliable, supplement only |

**Decision**: Defer implementation to Epic F (GDPR review). Recommended approach: **CDN edge header as primary** (Cloudflare `CF-IPCountry`) + **user profile override** for explicit preference.

**GDPR notes**:
- GeoIP/CDN headers derive from IP addresses which are PII under GDPR
- Cloudflare's DPA covers IP processing for CDN services
- User profile setting provides lawful basis via explicit consent (Art. 6(1)(a))
- Country-level geolocation (not city/coordinates) has lower privacy impact

## 7. Terraform/Pulumi Infrastructure Sketches

> **Note**: These are directional sketches — not production-ready IaC. They illustrate the infrastructure shape for each phase.

### Phase 1 — Cloudflare CDN (Terraform)

```hcl
# Phase 1: CDN for frontend static assets
# Provider: Cloudflare (~$20-50/mo on Pro plan)

terraform {
  required_providers {
    cloudflare = {
      source  = "cloudflare/cloudflare"
      version = "~> 4.0"
    }
  }
}

variable "zone_id" {
  description = "Cloudflare zone ID for meepleai.com"
  type        = string
}

variable "origin_ip" {
  description = "Origin server IP (Italy VPS)"
  type        = string
}

# DNS record pointing to origin
resource "cloudflare_record" "api" {
  zone_id = var.zone_id
  name    = "api"
  content = var.origin_ip
  type    = "A"
  proxied = true # Enable Cloudflare proxy (CDN + DDoS protection)
  ttl     = 1    # Auto when proxied
}

resource "cloudflare_record" "web" {
  zone_id = var.zone_id
  name    = "@"
  content = var.origin_ip
  type    = "A"
  proxied = true
  ttl     = 1
}

# Cache rules for Next.js static assets
resource "cloudflare_ruleset" "cache_static" {
  zone_id = var.zone_id
  name    = "Cache static assets"
  kind    = "zone"
  phase   = "http_request_cache_settings"

  rules {
    action = "set_cache_settings"
    action_parameters {
      cache = true
      edge_ttl {
        mode    = "override_origin"
        default = 86400 # 1 day for static assets
      }
      browser_ttl {
        mode    = "override_origin"
        default = 3600 # 1 hour browser cache
      }
    }
    expression = "(http.request.uri.path matches \"^/_next/static/\")"
    enabled    = true
  }
}

# SSL/TLS: Full (Strict) — origin has valid cert
resource "cloudflare_zone_settings_override" "tls" {
  zone_id = var.zone_id
  settings {
    ssl              = "strict"
    min_tls_version  = "1.2"
    always_use_https = "on"
  }
}
```

### Phase 2 — K8s Single Cluster EU (Terraform)

```hcl
# Phase 2: Kubernetes single-cluster (EU)
# Provider: Hetzner Cloud or similar (~$200-500/mo)
# NOTE: Resource names below are illustrative pseudocode — actual Terraform
# resource types vary by provider (e.g., hcloud_server for Hetzner, google_container_cluster for GKE).

# --- Managed Kubernetes Cluster ---
resource "hcloud_kubernetes_cluster" "meepleai" {
  name     = "meepleai-eu"
  location = "nbg1" # Nuremberg, Germany (EU)

  # Control plane
  kubernetes_version = "1.30"

  # Worker pool: general workloads
  node_pool {
    name       = "general"
    server_type = "cpx31" # 4 vCPU, 8GB RAM
    min_nodes  = 2
    max_nodes  = 5
    autoscaling = true
  }

  # Worker pool: GPU for Ollama (optional — can use OpenRouter only)
  # node_pool {
  #   name        = "gpu"
  #   server_type = "gpu-a100-40" # Example — check provider availability
  #   min_nodes   = 0
  #   max_nodes   = 2
  #   autoscaling = true
  #   labels = {
  #     "workload" = "gpu"
  #   }
  #   taints = [{
  #     key    = "nvidia.com/gpu"
  #     value  = "true"
  #     effect = "NoSchedule"
  #   }]
  # }
}

# --- Managed PostgreSQL ---
resource "hcloud_managed_database" "postgres" {
  name         = "meepleai-db"
  engine       = "postgresql"
  version      = "16"
  plan         = "basic-2" # 2 vCPU, 4GB RAM, 80GB SSD
  location     = "nbg1"

  # Connection pooling (PgBouncer built into managed DB)
  pgbouncer_enabled = true
  pool_mode         = "transaction"
  max_connections   = 100
}

# --- Managed Redis ---
resource "hcloud_managed_database" "redis" {
  name     = "meepleai-redis"
  engine   = "redis"
  version  = "7"
  plan     = "basic-1" # 1 vCPU, 2GB RAM
  location = "nbg1"
}

# --- Qdrant StatefulSet (Helm) ---
# Qdrant doesn't have a managed offering on most providers —
# deploy as StatefulSet with persistent volumes
resource "helm_release" "qdrant" {
  name       = "qdrant"
  repository = "https://qdrant.github.io/qdrant-helm"
  chart      = "qdrant"
  namespace  = "meepleai"

  set {
    name  = "persistence.size"
    value = "50Gi"
  }

  set {
    name  = "resources.requests.memory"
    value = "2Gi"
  }

  set {
    name  = "resources.requests.cpu"
    value = "1"
  }

  set {
    name  = "replicaCount"
    value = "1" # Single replica for Phase 2
  }
}

# --- Ingress (Traefik) ---
resource "helm_release" "traefik" {
  name       = "traefik"
  repository = "https://traefik.github.io/charts"
  chart      = "traefik"
  namespace  = "traefik"

  set {
    name  = "ports.web.redirectTo.port"
    value = "websecure"
  }

  set {
    name  = "providers.kubernetesIngress.enabled"
    value = "true"
  }
}
```

### Phase 3 — Multi-Region (Directional Pulumi/TypeScript)

```typescript
// Phase 3: Multi-region K8s (~$800-2000/mo)
// Directional sketch — illustrates architecture shape

import * as pulumi from "@pulumi/pulumi";
import * as k8s from "@pulumi/kubernetes";

const regions = ["eu-central", "us-east"] as const;

// One K8s cluster per region
const clusters = regions.map(region => ({
  region,
  cluster: new k8s.Cluster(`meepleai-${region}`, {
    // Provider-specific cluster config
    // e.g., GKE Autopilot, EKS, AKS
  }),
}));

// PostgreSQL: Primary in EU, read replica in US
// (Use managed DB with cross-region replication)
const dbPrimary = {
  region: "eu-central",
  role: "primary",
  // Write traffic only
};

const dbReplica = {
  region: "us-east",
  role: "read-replica",
  // Read traffic for non-EU users
  // Replication lag target: <1s
};

// Qdrant Cloud: Multi-region collection
// (Qdrant Cloud handles replication automatically)
const qdrantConfig = {
  provider: "qdrant-cloud",
  clusters: regions.map(region => ({
    region,
    shards: 2,
    replicas: 1,
  })),
};

// Global load balancer with geo-routing
// Routes users to nearest region
const globalLb = {
  provider: "cloudflare", // Or AWS Global Accelerator
  routing: "geo",
  rules: [
    { region: "eu-*", target: "eu-central" },
    { region: "us-*", target: "us-east" },
    { region: "*", target: "eu-central" }, // Default to EU
  ],
};

// Region-aware Ollama deployment
// GPU nodes in each region with model pre-loading
clusters.forEach(({ region, cluster }) => {
  new k8s.apps.v1.Deployment(`ollama-${region}`, {
    metadata: { namespace: "meepleai" },
    spec: {
      replicas: 1,
      template: {
        spec: {
          nodeSelector: { "workload": "gpu" },
          containers: [{
            name: "ollama",
            image: "ollama/ollama:latest",
            resources: {
              limits: { "nvidia.com/gpu": "1" },
              requests: { memory: "8Gi", cpu: "4" },
            },
            // Pre-load models on startup
            lifecycle: {
              postStart: {
                exec: {
                  command: ["ollama", "pull", "llama3:8b"],
                },
              },
            },
          }],
        },
      },
    },
  }, { provider: cluster });
});
```

## 8. Operational Complexity Assessment

| Phase | Team Skill Required | Ops Burden | Risk | Rollback Difficulty |
|-------|-------------------|------------|------|---------------------|
| **Current** | Docker basics | Low | Low | N/A |
| **1 (CDN)** | + CDN configuration, DNS | Low | Low | Easy — remove proxy |
| **2 (K8s)** | + K8s administration, managed DB ops, CI/CD | Medium | Medium | Moderate — revert to Docker Compose |
| **3 (Multi-region)** | + Multi-region networking, data replication, consistency | High | High | Difficult — data sync issues |
| **4 (Edge)** | + Edge computing, distributed systems, CAP theorem | Very High | Very High | Very difficult |

**Team readiness**: K8s expertise is available. Phase 1-2 are feasible with current team. Phase 3+ may require additional distributed systems expertise.

**Risk mitigation**:
- Each phase is independently deployable — no big-bang migration
- Phase triggers provide natural go/no-go checkpoints
- Can stay at any phase indefinitely if growth plateaus
- OpenRouter as primary LLM provider reduces GPU management complexity

## 9. SLO Targets per Phase

Each phase must meet minimum service-level objectives before the next phase trigger is acted on. These targets validate that the current infrastructure is performing as expected and justify the investment in scaling.

| Metric | Current | Phase 1 | Phase 2 | Phase 3 | Phase 4 |
|--------|---------|---------|---------|---------|---------|
| **API P95 latency** | <500ms | <500ms | <200ms | <200ms (regional) | <100ms (edge) |
| **RAG query P95** | <3s | <3s | <2s | <2s (regional) | <1s |
| **Ollama P95** | <5s (CPU) | <5s | <3s (GPU optional) | <2s (GPU) | <1s |
| **Availability** | 95% (single node) | 97% (+CDN failover) | 99.5% (K8s self-healing) | 99.9% (multi-region) | 99.95% |
| **RPO** (data loss tolerance) | 24h (daily backup) | 24h | 1h (managed DB WAL) | <5min (streaming replication) | <1min |
| **RTO** (recovery time) | 4h (manual restore) | 4h | 30min (K8s restart) | 5min (failover to other region) | <1min |
| **CDN cache hit ratio** | N/A | >80% static | >80% | >85% | >90% |
| **Cross-region latency** | N/A | N/A | N/A | <150ms (EU↔US) | <50ms (edge, reads only) |

**Measurement**: All latency SLOs measured at application layer (not network). Availability measured as successful API responses / total requests (excluding planned maintenance). Phase 4 latency targets apply to read-only, edge-cached responses — write paths still route to EU primary and are bounded by Phase 3 targets.

**Escalation**: If a phase fails to meet its SLO targets for >7 consecutive days under normal load, trigger investigation before considering next phase transition.

## 10. Data Consistency Model

As the system scales beyond a single node, explicit consistency guarantees must be defined per data store to prevent developers from making conflicting assumptions.

### CAP Positioning per Service

| Service | CAP Position | Consistency Model | Acceptable Staleness | Write Strategy |
|---------|-------------|-------------------|---------------------|----------------|
| **PostgreSQL** | CP (consistency + partition tolerance) | Strong consistency via primary writes | Read replicas: <1s lag target, <5s hard limit | All writes to primary region; reads from nearest replica |
| **Qdrant** | CP within single cluster (Raft consensus); AP when configured for cross-region replication | Eventual consistency across regions only | <10s for new vectors; stale search results acceptable for up to 30s | Writes to nearest region, async replication to others |
| **Redis** | AP (availability preferred) | Eventual consistency acceptable | Rate limits: region-local (no cross-region sync needed) | Region-local writes; no cross-region replication |
| **Ollama** | N/A (stateless inference) | N/A | N/A | Region-local; model version must match globally |
| **Embedding service** | N/A (stateless inference) | N/A | N/A | Region-local; model version must match globally |

### Write Routing Rules (Phase 3+)

| Operation | Write Target | Read Target | Rationale |
|-----------|-------------|-------------|-----------|
| User registration/login | EU primary | Nearest replica | GDPR: user PII stored in EU |
| Game CRUD | EU primary | Nearest replica | Low write frequency, strong consistency needed |
| Chat messages | EU primary | Nearest replica | Audit trail requires consistency |
| PDF upload & processing | EU primary | EU primary | Async processing, single-region service |
| Vector indexing (Qdrant) | Nearest region | Nearest region | Eventual consistency acceptable for search |
| Rate limit counters (Redis) | Region-local | Region-local | Per-region enforcement, no global sync |
| Circuit breaker state (Redis) | Region-local | Region-local | Regional health independent per provider endpoint |

### GDPR Data Residency Constraint

User PII (email, name, auth tokens) **MUST** remain in EU primary. Non-EU read replicas may cache non-PII data (game catalog, public knowledge base content). This constraint shapes the write routing: all user-facing writes go to EU primary regardless of user location.

## 11. Failure Category Matrix

Failure categories per phase with mitigation direction. Detailed runbooks will be created at each phase transition, not in advance.

### Phase 1 (CDN) Failures

| Failure | Impact | Probability | Mitigation |
|---------|--------|-------------|------------|
| CDN cache poisoning | Stale/wrong static assets served | Low | Cache purge via Cloudflare API; short browser TTL (1h) |
| CDN outage | Frontend degraded (no caching) | Very Low | Cloudflare SLA 100% uptime; origin still serves directly |
| Origin unreachable from CDN | Full outage | Low | Health checks + alerting; same as current (single VPS) |

### Phase 2 (K8s) Failures

| Failure | Impact | Probability | Mitigation |
|---------|--------|-------------|------------|
| Pod crash loop | Service degradation | Medium | K8s restart policy + HPA; PodDisruptionBudget (min 1 ready) |
| Managed DB failover | ~30s downtime | Low | Managed DB handles automatically; connection retry in app |
| Qdrant volume corruption | Vector search unavailable | Low | Persistent volume snapshots; re-index from PostgreSQL source |
| GPU node unavailable | Ollama fallback to CPU (slower) | Medium | Tolerate CPU fallback; OpenRouter as alternative |
| K8s node exhaustion | New pods cannot schedule | Medium | Cluster autoscaler + resource quotas + alerts at 80% |
| Secret rotation failure | Service auth breaks | Low | Gradual rotation (old + new valid simultaneously) |

### Phase 3 (Multi-Region) Failures

| Failure | Impact | Probability | Mitigation |
|---------|--------|-------------|------------|
| Cross-region DB replication lag >5s | Stale reads for non-EU users | Medium | Monitor lag; circuit break to primary-only reads if >5s |
| Regional Qdrant outage | RAG unavailable in one region | Low | Route to other region (higher latency) until recovery |
| Regional Ollama GPU exhaustion | LLM unavailable in one region | Medium | OpenRouter fallback (cloud LLM); cross-region Ollama as last resort |
| Split-brain (network partition) | Inconsistent state across regions | Very Low | PostgreSQL primary arbitrates; Qdrant eventual consistency by design |
| Embedding model version mismatch | Incompatible vectors across regions | Low | Block deployment if model hash differs; health check validation |
| Global load balancer misconfiguration | Traffic routed to wrong region | Low | Health-based routing; automated failover; manual override capability |

### Rollback Procedures

| Phase Transition | Rollback Approach | Data Handling | Estimated Time |
|-----------------|-------------------|---------------|----------------|
| Phase 1 → Current | Remove Cloudflare proxy, revert DNS | No data impact | <1h |
| Phase 2 → Phase 1 | Export managed DB → restore to Docker PostgreSQL | pg_dump/pg_restore; Qdrant snapshot restore | 2-4h |
| Phase 3 → Phase 2 | Drain non-EU region, consolidate to EU cluster | Stop replication; merge any region-local data | 4-8h (planned) |

## 12. Phase 2 Provider Evaluation Criteria

When the Phase 2 trigger (>5K MAU) is approaching, evaluate cloud providers using these criteria. Document the evaluation as a separate ADR.

### Evaluation Dimensions

| Dimension | Weight | Criteria | Notes |
|-----------|--------|----------|-------|
| **Managed K8s quality** | 25% | Control plane SLA, upgrade process, node pool flexibility, autoscaling | Hetzner K8s vs GKE Autopilot vs EKS |
| **Managed PostgreSQL** | 20% | Built-in PgBouncer, automated backups, read replica support, pgvector extension | Must support pgvector for future hybrid search |
| **GPU availability** | 15% | GPU node types, spot/preemptible pricing, availability in EU | For Ollama; optional if OpenRouter-primary strategy |
| **Cost efficiency** | 15% | Total cost at 5K MAU projected workload, egress pricing, storage pricing | Compare against per-service cost table (§3) |
| **Data residency** | 10% | EU datacenter availability, GDPR DPA, data sovereignty guarantees | Must have EU region; GDPR compliance mandatory |
| **Vendor lock-in risk** | 10% | Proprietary services used, migration difficulty, standard K8s compatibility | Prefer standard K8s APIs; avoid provider-specific CRDs |
| **Observability integration** | 5% | Prometheus/Grafana compatibility, managed monitoring options, log aggregation | Must support existing Prometheus recording rules |

### Provider Shortlist (Evaluate at Trigger)

| Provider | Strengths | Risks |
|----------|-----------|-------|
| **Hetzner Cloud** | Lowest cost, EU-native, current relationship | Limited managed services, no GPU (as of 2026-03) |
| **Google Cloud (GKE)** | Best managed K8s, GPU availability, Autopilot | Higher cost, vendor lock-in (GKE-specific features) |
| **AWS (EKS)** | Broadest service catalog, global presence | Complex pricing, steeper learning curve |
| **DigitalOcean (DOKS)** | Simple K8s, developer-friendly | Limited GPU, smaller EU presence |

### Vendor Lock-in Assessment

| Decision Point | Lock-in Risk | Mitigation |
|---------------|-------------|------------|
| Managed PostgreSQL | Low — standard PostgreSQL, pg_dump portable | Use standard extensions only (pgvector is open-source) |
| Managed Redis | Low — standard Redis protocol | No provider-specific commands |
| Qdrant Cloud | Medium — proprietary managed service | Self-hosted Qdrant Helm chart as fallback |
| K8s provider | Low — standard K8s APIs | Avoid provider-specific CRDs; use Helm charts |
| Cloudflare CDN | Low — standard DNS + HTTP caching | Can switch to Fastly/CloudFront with DNS change |
| GPU instances | Medium — availability varies by provider | OpenRouter as GPU-free fallback for LLM |

## 13. Decision

**Adopt phased scaling approach with explicit triggers.** No infrastructure action until Phase 1 trigger (>1K MAU).

Preparation work completed now:
1. **G1** (Issue #107): `UserRegion` field added to `LlmRoutingDecision` — prepares data model
2. **G3** (this ADR): Scaling playbook documented — serves as single reference

**What this ADR does NOT decide**:
- Specific cloud provider (evaluate at Phase 2 trigger)
- Region detection implementation (deferred to Epic F — GDPR review)
- Region-aware routing logic (deferred to Epic B — HybridLlmService refactoring)
- Production IaC code (sketches here are directional only)

**Scaling decision authority**: Phase 1-2 can be initiated by engineering lead. Phase 3+ requires business stakeholder approval (cost + complexity increase).

## 14. References

| Reference | Description |
|-----------|-------------|
| ADR-007 | Hybrid LLM Architecture — defines provider routing |
| ADR-043 | LLM Subsystem NFRs — latency/cost/availability targets |
| Issue #107 | G1: UserRegion field in LlmRoutingDecision |
| Issue #26 | Epic: Multi-Region Preparation |
| PRD | `docs/plans/2026-03-08-llm-system-improvement-prd.md` (Epic G) |
| Qdrant Cloud | https://cloud.qdrant.io — managed vector DB with multi-region |
| Cloudflare CDN | https://developers.cloudflare.com/cache/ — edge caching |
