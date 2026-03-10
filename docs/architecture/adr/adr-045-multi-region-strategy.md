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
| A1 | In-memory circuit breaker state (Interlocked fields) | `CircuitBreakerState` | `KnowledgeBase/Domain/Services/LlmManagement/CircuitBreakerState.cs` | Move to Redis — pods currently disagree on circuit status |
| A2 | Local Redis with no namespace partitioning | `FreeModelQuotaTracker`, `OpenRouterRateLimitTracker` | `KnowledgeBase/Application/Services/FreeModelQuotaTracker.cs`, `KnowledgeBase/Application/Services/OpenRouterRateLimitTracker.cs` | Namespace keys by region or share single Redis with latency budget |
| A3 | Single PostgreSQL instance | All 15 bounded contexts | `Infrastructure/MeepleAiDbContext.cs` | Read replicas + PgBouncer for connection pooling |
| A4 | Local Qdrant (Docker volume) | Vector search | Qdrant client configuration | Qdrant Cloud or cross-region replication (50ms local vs 200ms+ cross-region) |
| A5 | Co-located Ollama | `OllamaLlmClient` | `Services/LlmClients/OllamaLlmClient.cs` | GPU node pools on K8s, cold start mitigation (30-60s) |
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

## 4. Prerequisites per Phase

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

- [ ] **Region detection** strategy implemented (see Section 5)
- [ ] **G1**: `UserRegion` field in `LlmRoutingDecision` (already implemented — Issue #107)
- [ ] **Region-aware routing** in `HybridLlmService` / `ILlmRoutingStrategy`
- [ ] Cross-region PostgreSQL replication (or managed DB with read replicas per region)
- [ ] Qdrant Cloud or self-managed cross-region replication
- [ ] Regional Ollama instances with GPU scheduling
- [ ] CDN-based API routing (Cloudflare Workers or similar)
- [ ] Embedding model consistency guarantee (same model version across regions)
- [ ] Data residency compliance per region (GDPR for EU, other regulations)

### Phase 3 -> Phase 4 (Edge)

Advanced distributed systems:

- [ ] Edge computing for latency-sensitive operations
- [ ] Distributed Qdrant with eventual consistency model
- [ ] Regional Ollama with model version pinning
- [ ] Global load balancer with health-based geo-routing
- [ ] Cross-region event bus for eventual consistency

## 5. Region Detection Strategy Candidates

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

## 6. Terraform/Pulumi Infrastructure Sketches

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

## 7. Operational Complexity Assessment

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

## 8. Decision

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

## 9. References

| Reference | Description |
|-----------|-------------|
| ADR-007 | Hybrid LLM Architecture — defines provider routing |
| ADR-043 | LLM Subsystem NFRs — latency/cost/availability targets |
| Issue #107 | G1: UserRegion field in LlmRoutingDecision |
| Issue #26 | Epic: Multi-Region Preparation |
| PRD | `docs/plans/2026-03-08-llm-system-improvement-prd.md` (Epic G) |
| Qdrant Cloud | https://cloud.qdrant.io — managed vector DB with multi-region |
| Cloudflare CDN | https://developers.cloudflare.com/cache/ — edge caching |
