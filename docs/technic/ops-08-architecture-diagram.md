# OPS-08 Self-Hosted Runners Architecture Diagrams

**Related**: [OPS-08 Technical Analysis](./ops-08-self-hosted-runners-analysis.md)

This document provides visual architecture diagrams for self-hosted GitHub Actions runners deployment options.

---

## Table of Contents

1. [Security Architecture Overview](#security-architecture-overview)
2. [Option A: Docker-Based Architecture](#option-a-docker-based-architecture)
3. [Option B: Kubernetes (ARC) Architecture](#option-b-kubernetes-arc-architecture)
4. [Option C: AWS Auto Scaling Architecture](#option-c-aws-auto-scaling-architecture)
5. [Workflow Routing Decision Tree](#workflow-routing-decision-tree)
6. [Security Boundaries and Isolation](#security-boundaries-and-isolation)

---

## Security Architecture Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                         GitHub.com (SaaS)                           │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              GitHub Actions Workflow                        │   │
│  │  ┌────────────────────────────────────────────────────────┐ │   │
│  │  │  on: pull_request                                      │ │   │
│  │  │  jobs:                                                 │ │   │
│  │  │    build:                                              │ │   │
│  │  │      runs-on: self-hosted                             │ │   │
│  │  │      steps: [...]                                      │ │   │
│  │  └────────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────────┘   │
└───────────────────────────┬─────────────────────────────────────────┘
                            │
                            │ HTTPS (443)
                            │ Webhook Event
                            │ ✅ Authenticated
                            │ ✅ Encrypted
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Self-Hosted Infrastructure                       │
│                  (Private VPC / Kubernetes Cluster)                 │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │           Runner Controller / Orchestrator                  │   │
│  │                                                                │   │
│  │  ┌──────────────────────────────────────────────────────┐   │   │
│  │  │  • Receives webhook from GitHub                      │   │   │
│  │  │  • Validates job request                             │   │   │
│  │  │  • Spawns ephemeral runner (pod/instance/container) │   │   │
│  │  │  • Monitors job execution                            │   │   │
│  │  │  • Destroys runner after job completion              │   │   │
│  │  └──────────────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                            │                                          │
│                            │ Spawns                                   │
│                            ▼                                          │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │             Ephemeral Runner (Isolated)                     │   │
│  │                                                                │   │
│  │  ┌──────────────────────────────────────────────────────┐   │   │
│  │  │  Runner Process (GitHub Actions agent)               │   │   │
│  │  │  ├─ Pulls workflow code from GitHub                  │   │   │
│  │  │  ├─ Executes job steps (build, test, deploy)         │   │   │
│  │  │  ├─ Reports status back to GitHub                    │   │   │
│  │  │  └─ Self-destructs after job completion              │   │   │
│  │  └──────────────────────────────────────────────────────┘   │   │
│  │                                                                │   │
│  │  Security Boundaries Applied:                                 │   │
│  │  ✅ Network Policy (egress only to GitHub + npm/nuget)       │   │
│  │  ✅ No Docker-in-Docker (use Kaniko or Kubernetes jobs)      │   │
│  │  ✅ Short-lived AWS credentials (OIDC, no static keys)       │   │
│  │  ✅ Non-root container (runAsNonRoot: true, UID 1000)        │   │
│  │  ✅ Read-only root filesystem (where possible)               │   │
│  │  ✅ Limited capabilities (drop all, add only required)       │   │
│  │  ✅ Resource limits (CPU: 2, Memory: 4Gi)                    │   │
│  │  ✅ Destroyed after job (no persistence)                     │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘
```

---

## Option A: Docker-Based Architecture

**Best for**: Development, testing, proof-of-concept

```
┌─────────────────────────────────────────────────────────────────────┐
│                         GitHub.com                                   │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ Webhook
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    Cloud Instance / VM                               │
│                  (AWS EC2 t3.medium, $30/month)                      │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │                    Docker Engine                             │   │
│  │                                                                │   │
│  │  ┌──────────────────────────────────────────────────────┐   │   │
│  │  │       GitHub Runner Container (Persistent)           │   │   │
│  │  │                                                        │   │   │
│  │  │  ┌──────────────────────────────────────────────┐   │   │   │
│  │  │  │  Runner Binary (v2.311.0)                    │   │   │   │
│  │  │  │  • Registration token (from GitHub)          │   │   │   │
│  │  │  │  • Listens for jobs                          │   │   │   │
│  │  │  │  • Executes workflow steps                   │   │   │   │
│  │  │  └──────────────────────────────────────────────┘   │   │   │
│  │  │                                                        │   │   │
│  │  │  Volumes Mounted:                                     │   │   │
│  │  │  • /var/run/docker.sock (⚠️ Docker-in-Docker)        │   │   │
│  │  │  • /actions-runner/_work (workspace)                 │   │   │
│  │  └──────────────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                       │
│  Security Concerns:                                                  │
│  ❌ Persistent runner (vulnerable to compromise)                    │
│  ❌ Docker-in-Docker requires privileged mode                       │
│  ❌ Single point of failure (no redundancy)                         │
│  ❌ Manual scaling (no auto-scaling)                                │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘

Cost Breakdown:
├─ EC2 t3.medium (24/7): $30.37/month
├─ EBS storage (50 GB): $5.00/month
├─ Data transfer: $0.45/month
└─ Total: $35.82/month
```

---

## Option B: Kubernetes (ARC) Architecture

**Best for**: Production, auto-scaling, high availability

```
┌─────────────────────────────────────────────────────────────────────┐
│                         GitHub.com                                   │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ Webhook
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│               Kubernetes Cluster (AWS EKS)                           │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │     Namespace: actions-runner-system                        │   │
│  │                                                                │   │
│  │  ┌──────────────────────────────────────────────────────┐   │   │
│  │  │  Actions Runner Controller (ARC)                     │   │   │
│  │  │  ┌────────────────────────────────────────────────┐ │   │   │
│  │  │  │  • Watches GitHub Actions API                  │ │   │   │
│  │  │  │  • Detects queued jobs                         │ │   │   │
│  │  │  │  • Creates runner pods on-demand               │ │   │   │
│  │  │  │  • Manages pod lifecycle                       │ │   │   │
│  │  │  └────────────────────────────────────────────────┘ │   │   │
│  │  └──────────────────────────────────────────────────────┘   │   │
│  │                                                                │   │
│  │  ┌──────────────────────────────────────────────────────┐   │   │
│  │  │  HorizontalRunnerAutoscaler (HRA)                    │   │   │
│  │  │  • Min replicas: 0 (scale to zero when idle)        │   │   │
│  │  │  • Max replicas: 10 (burst capacity)                │   │   │
│  │  │  • Metric: GitHub queue depth                       │   │   │
│  │  └──────────────────────────────────────────────────────┘   │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                            │                                          │
│                            │ Creates                                  │
│                            ▼                                          │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │          Ephemeral Runner Pods (Auto-Scaled)                │   │
│  │                                                                │   │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐            │   │
│  │  │ Runner 1   │  │ Runner 2   │  │ Runner 3   │ ...        │   │
│  │  │ (Job A)    │  │ (Job B)    │  │ (Job C)    │            │   │
│  │  │            │  │            │  │            │            │   │
│  │  │ • Isolated │  │ • Isolated │  │ • Isolated │            │   │
│  │  │ • Ephemeral│  │ • Ephemeral│  │ • Ephemeral│            │   │
│  │  │ • Non-root │  │ • Non-root │  │ • Non-root │            │   │
│  │  │ • Limited  │  │ • Limited  │  │ • Limited  │            │   │
│  │  │   network  │  │   network  │  │   network  │            │   │
│  │  └────────────┘  └────────────┘  └────────────┘            │   │
│  │       ▲               ▲               ▲                        │   │
│  │       └───────────────┴───────────────┘                        │   │
│  │               Destroyed after job                              │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │             Network Policy (Egress Control)                  │   │
│  │  ┌────────────────────────────────────────────────────────┐ │   │
│  │  │  Allowed:                                              │ │   │
│  │  │  ✅ github.com (443) - GitHub API                      │ │   │
│  │  │  ✅ registry.npmjs.org (443) - npm packages           │ │   │
│  │  │  ✅ nuget.org (443) - NuGet packages                  │ │   │
│  │  │  ✅ kube-dns (53) - DNS resolution                    │ │   │
│  │  │                                                         │ │   │
│  │  │  Blocked:                                              │ │   │
│  │  │  ❌ Internal services (VPC peering blocked)           │ │   │
│  │  │  ❌ AWS metadata endpoint (169.254.169.254)           │ │   │
│  │  │  ❌ Other pods in different namespaces                │ │   │
│  │  └────────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘

Cost Breakdown (AWS EKS):
├─ EKS cluster control plane: $72.00/month
├─ 2x t3.medium nodes (24/7): $60.74/month
├─ EBS storage (100 GB GP3): $10.00/month
├─ NAT gateway: $32.40/month
├─ Data transfer: $5.00/month
└─ Total: $180.14/month

Scaling Behavior:
• 0 jobs queued → 0 runner pods (min: 0)
• 1-3 jobs queued → 1-3 runner pods (startup: ~30-60s)
• 4-10 jobs queued → 4-10 runner pods (max: 10)
• Job completes → Pod destroyed immediately
```

---

## Option C: AWS Auto Scaling Architecture

**Best for**: AWS-centric environments, simpler than Kubernetes

```
┌─────────────────────────────────────────────────────────────────────┐
│                         GitHub.com                                   │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ Webhook
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                    AWS Lambda (Webhook Handler)                      │
│  ┌────────────────────────────────────────────────────────────────┐ │
│  │  • Receives GitHub webhook event                               │ │
│  │  • Parses job queue depth                                      │ │
│  │  • Triggers Auto Scaling Group to scale up                     │ │
│  └────────────────────────────────────────────────────────────────┘ │
└───────────────────────────┬─────────────────────────────────────────┘
                            │ Invokes
                            ▼
┌─────────────────────────────────────────────────────────────────────┐
│                  AWS Auto Scaling Group (ASG)                        │
│                                                                       │
│  Configuration:                                                      │
│  • Min size: 0 (scale to zero when idle)                            │
│  • Max size: 10 (burst capacity)                                    │
│  • Desired: Dynamic (based on queue depth)                          │
│  • Health check: GitHub runner status                               │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │              Launch Template                                 │   │
│  │  • AMI: Custom GitHub runner image                           │   │
│  │  • Instance type: t3.medium                                  │   │
│  │  • IAM role: GitHubRunnerRole (OIDC)                         │   │
│  │  • User data: Register runner on boot                        │   │
│  │  • Security group: Egress to GitHub only                     │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                            │                                          │
│                            │ Launches                                 │
│                            ▼                                          │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │          Ephemeral EC2 Instances (Auto-Scaled)              │   │
│  │                                                                │   │
│  │  ┌────────────┐  ┌────────────┐  ┌────────────┐            │   │
│  │  │Instance 1  │  │Instance 2  │  │Instance 3  │ ...        │   │
│  │  │(Job A)     │  │(Job B)     │  │(Job C)     │            │   │
│  │  │            │  │            │  │            │            │   │
│  │  │ Runner     │  │ Runner     │  │ Runner     │            │   │
│  │  │ Process    │  │ Process    │  │ Process    │            │   │
│  │  │            │  │            │  │            │            │   │
│  │  │ Lifecycle: │  │ Lifecycle: │  │ Lifecycle: │            │   │
│  │  │ 1. Launch  │  │ 1. Launch  │  │ 1. Launch  │            │   │
│  │  │ 2. Register│  │ 2. Register│  │ 2. Register│            │   │
│  │  │ 3. Run job │  │ 3. Run job │  │ 3. Run job │            │   │
│  │  │ 4. Terminate│ │ 4. Terminate│ │ 4. Terminate│           │   │
│  │  └────────────┘  └────────────┘  └────────────┘            │   │
│  │       ▲               ▲               ▲                        │   │
│  │       └───────────────┴───────────────┘                        │   │
│  │         Terminated after job (ephemeral)                       │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                       │
│  ┌─────────────────────────────────────────────────────────────┐   │
│  │          Security Groups (Network Firewall)                  │   │
│  │  ┌────────────────────────────────────────────────────────┐ │   │
│  │  │  Egress Rules:                                         │ │   │
│  │  │  ✅ github.com:443 - GitHub API                        │ │   │
│  │  │  ✅ registry.npmjs.org:443 - npm packages             │ │   │
│  │  │  ✅ nuget.org:443 - NuGet packages                    │ │   │
│  │  │  ✅ s3.amazonaws.com:443 - Artifacts                  │ │   │
│  │  │  ❌ All other traffic blocked by default              │ │   │
│  │  └────────────────────────────────────────────────────────┘ │   │
│  └─────────────────────────────────────────────────────────────┘   │
│                                                                       │
└───────────────────────────────────────────────────────────────────────┘

Cost Breakdown (AWS Spot Instances):
├─ 2x t3.medium spot (50% uptime): $15.19/month
├─ EBS storage (50 GB GP3): $5.00/month
├─ NAT gateway: $32.40/month
├─ Lambda invocations: $0.20/month
├─ Data transfer: $5.00/month
└─ Total: $57.79/month

Scaling Behavior:
• 0 jobs queued → 0 instances (min: 0)
• 1-3 jobs queued → 1-3 instances (startup: ~60-90s)
• 4-10 jobs queued → 4-10 instances (max: 10)
• Job completes → Instance terminated immediately
```

---

## Workflow Routing Decision Tree

Decision logic for routing CI jobs to self-hosted vs GitHub-hosted runners:

```
                    ┌─────────────────────────┐
                    │   GitHub Actions        │
                    │   Workflow Triggered    │
                    └────────────┬────────────┘
                                 │
                                 ▼
                    ┌─────────────────────────┐
                    │  Check Event Type       │
                    └────────────┬────────────┘
                                 │
                ┌────────────────┼────────────────┐
                │                │                │
                ▼                ▼                ▼
        ┌─────────────┐  ┌──────────────┐  ┌──────────────┐
        │pull_request │  │     push     │  │   schedule   │
        └──────┬──────┘  └──────┬───────┘  └──────┬───────┘
               │                │                  │
               ▼                ▼                  ▼
        ┌─────────────┐  ┌──────────────┐  ┌──────────────┐
        │ Self-Hosted │  │   Check      │  │ GitHub-Hosted│
        │   Runner    │  │   Branch     │  │    Runner    │
        │             │  └──────┬───────┘  │              │
        │ Why: Fast   │         │          │ Why: Safety  │
        │ feedback    │    ┌────┴────┐     │ (scheduled   │
        │ for devs    │    │  main   │     │  jobs are    │
        └─────────────┘    └────┬────┘     │  low volume) │
                                │          └──────────────┘
                           ┌────┴────┐
                           │ branch  │
                           └────┬────┘
                                │
                    ┌───────────┼───────────┐
                    │                       │
                    ▼                       ▼
        ┌─────────────────────┐  ┌──────────────────┐
        │   GitHub-Hosted     │  │   Self-Hosted    │
        │      Runner         │  │     Runner       │
        │                     │  │                  │
        │ Why: Safety for     │  │ Why: Feature     │
        │ main branch         │  │ branches get     │
        │ (production)        │  │ fast feedback    │
        └─────────────────────┘  └──────────────────┘

Workflow Configuration Example:
```yaml
# .github/workflows/ci.yml
jobs:
  ci-api:
    # Route based on event type
    runs-on: ${{
      github.event_name == 'pull_request' && 'self-hosted' ||
      github.event_name == 'push' && github.ref == 'refs/heads/main' && 'ubuntu-latest' ||
      'self-hosted'
    }}

  ci-web:
    # Route based on file changes (web-only changes use GitHub-hosted)
    runs-on: ${{
      needs.changes.outputs.api == 'true' && 'self-hosted' ||
      'ubuntu-latest'
    }}
```

---

## Security Boundaries and Isolation

Visual representation of security layers protecting self-hosted runners:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                          External Threats                                │
│  • Malicious pull requests                                              │
│  • Compromised dependencies                                             │
│  • Supply chain attacks                                                 │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │ Mitigated by ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                    Layer 1: Access Control                               │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  • Runner Groups: Repository-level access control                  │ │
│  │  • GitHub App: Scoped permissions (no org admin)                   │ │
│  │  • Runner Labels: Workflow-level targeting                         │ │
│  │  ✅ Prevents unauthorized repositories from using runners          │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │ Passes to ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                    Layer 2: Network Isolation                            │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  • Dedicated VPC/Subnet: Isolated from other services             │ │
│  │  • Security Groups/Network Policies: Egress-only to GitHub        │ │
│  │  • No VPC Peering: Cannot access internal corporate resources     │ │
│  │  ✅ Blocks lateral movement and data exfiltration                  │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │ Executes within ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                    Layer 3: Container/VM Isolation                       │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  • Ephemeral Runtime: Destroyed after each job                     │ │
│  │  • Non-Root: Runs as UID 1000, no privilege escalation            │ │
│  │  • Read-Only Root FS: Cannot modify system files                  │ │
│  │  • Resource Limits: CPU/memory caps prevent resource exhaustion   │ │
│  │  ✅ Limits blast radius of compromised runner                      │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │ Uses ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                    Layer 4: Credential Management                        │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  • OIDC: Short-lived AWS credentials (no static keys)             │ │
│  │  • GITHUB_TOKEN: Auto-generated, expires after job                │ │
│  │  • Secrets: Kubernetes secrets or AWS Secrets Manager             │ │
│  │  • Token Rotation: Runner tokens rotated quarterly                │ │
│  │  ✅ Prevents credential theft and replay attacks                   │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────┬───────────────────────────────────────┘
                                  │ Monitored by ↓
┌─────────────────────────────────────────────────────────────────────────┐
│                    Layer 5: Observability & Alerting                     │
│  ┌────────────────────────────────────────────────────────────────────┐ │
│  │  • CloudWatch/Prometheus: CPU, memory, network metrics            │ │
│  │  • Alerts: High egress, abnormal CPU, job failures                │ │
│  │  • Audit Logs: All runner activity logged to Seq/CloudWatch       │ │
│  │  • Incident Response: Automated runner termination on alert       │ │
│  │  ✅ Detects and responds to anomalous behavior                     │ │
│  └────────────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────────────┘

Defense in Depth: Multiple layers ensure that if one layer is breached,
others provide continued protection. No single layer is sufficient alone.
```

---

## Runner Lifecycle State Machine

Visual representation of runner lifecycle from creation to destruction:

```
                    START
                      │
                      ▼
        ┌─────────────────────────┐
        │   PROVISIONING          │
        │  • Create pod/instance  │
        │  • Pull runner image    │
        │  • Apply security       │
        │    policies             │
        └────────────┬────────────┘
                     │ ~30-90s startup
                     ▼
        ┌─────────────────────────┐
        │   REGISTERING           │
        │  • Fetch registration   │
        │    token from GitHub    │
        │  • Register with repo   │
        │  • Apply runner labels  │
        └────────────┬────────────┘
                     │ ~5-10s registration
                     ▼
        ┌─────────────────────────┐
        │   IDLE (Listening)      │◄───────┐
        │  • Polling GitHub API   │        │
        │  • Waiting for job      │        │
        │  • Max idle: 5 minutes  │        │
        └────────────┬────────────┘        │
                     │                     │
         Job queued  │                     │ No jobs
                     ▼                     │
        ┌─────────────────────────┐        │
        │   RUNNING JOB           │        │
        │  • Pull workflow code   │        │
        │  • Execute steps        │        │
        │  • Report status        │        │
        │  • Upload artifacts     │        │
        └────────────┬────────────┘        │
                     │                     │
        Job complete │                     │
                     ▼                     │
        ┌─────────────────────────┐        │
        │   CLEANUP               │        │
        │  • Flush logs           │        │
        │  • Remove workspace     │        │
        │  • Deregister runner    │        │
        └────────────┬────────────┘        │
                     │                     │
                     ▼                     │
        ┌─────────────────────────┐        │
        │   TERMINATING           │        │
        │  • Delete pod/instance  │        │
        │  • Release resources    │        │
        │  • Audit log cleanup    │        │
        └────────────┬────────────┘        │
                     │                     │
                     ▼                     │
        ┌─────────────────────────┐        │
        │   TERMINATED            │        │
        │  • Runner destroyed     │        │
        │  • Resources freed      │        │
        │  • Ephemeral (no state) │        │
        └─────────────────────────┘        │
                                            │
                                            │
        ┌─────────────────────────┐         │
        │   IDLE TIMEOUT          │─────────┘
        │  • No jobs for 5 min    │
        │  • Auto-scale down      │
        │  • Terminate gracefully │
        └─────────────────────────┘

Key Principles:
• Ephemeral: No state persists between jobs
• Time-Limited: Max idle time prevents cost overruns
• Auto-Termination: Graceful shutdown after job completion
• Security: Deregistration prevents runner reuse
```

---

## Cost Comparison Chart

Visual representation of monthly costs across all options:

```
Monthly Cost ($)
│
300 ┤                                          ┌──────────────┐
    │                                          │ Kubernetes   │
250 ┤                                          │ (EKS + ARC)  │
    │                                          │   $180.14    │
200 ┤                                          └──────────────┘
    │
150 ┤
    │
100 ┤        ┌──────────────┐
    │        │ AWS ASG      │
 50 ┤        │ (Spot)       │                  ┌──────────────┐
    │        │  $57.79      │                  │ GitHub       │
  0 ┼────────┴──────────────┴──────────────────┤ Larger (4c)  │
    │ Docker  AWS ASG  Kubernetes  GitHub-Free │ $80-400/mo   │
    │ $35.82  (Spot)    (EKS)       $0/mo      │ (variable)   │
    │        $57.79     $180.14                 └──────────────┘

Break-Even Analysis (vs GitHub Larger Runners @ $0.08/min):
┌──────────────┬───────────────┬─────────────────────────────┐
│ Option       │ Monthly Cost  │ Break-Even (minutes/month)  │
├──────────────┼───────────────┼─────────────────────────────┤
│ Docker (A)   │ $35.82        │ 448 minutes                 │
│ AWS ASG (C)  │ $57.79        │ 722 minutes                 │
│ Kubernetes   │ $180.14       │ 2,252 minutes               │
└──────────────┴───────────────┴─────────────────────────────┘

Current Usage: ~600-800 minutes/month
✅ Docker: Saves $12-29/month vs larger runners
✅ AWS ASG: Marginal savings ($6-7/month)
❌ Kubernetes: NOT cost-effective (need 2252+ min/month)

Note: Does NOT include maintenance time (~2-4 hours/month = $100-200)
When including maintenance, all options cost MORE than GitHub larger runners!
```

---

## Performance Comparison Chart

Visual representation of CI execution time across runners:

```
CI Execution Time (minutes)
│
15 ┤ ┌──────────────┐
   │ │ GitHub-      │
12 ┤ │ Hosted       │
   │ │ (Standard)   │
10 ┤ │   10-12 min  │
   │ └──────────────┘
 8 ┤           ┌──────────────┐
   │           │ Self-Hosted  │
 6 ┤           │ (t3.medium)  │
   │           │   5-7 min    │
 4 ┤           └──────────────┘
   │                     ┌──────────────┐
 2 ┤                     │ Self-Hosted  │
   │                     │ (t3.large)   │
 0 ┼─────────────────────│   3-4 min    │
   │ GitHub   Self-Hosted│              │
   │ Standard (t3.medium)└──────────────┘
   │ 10-12min   5-7min    Self-Hosted
   │                     (t3.large)
   │                      3-4min

Performance Factors:
┌────────────────────┬──────────────┬────────────────┐
│ Factor             │ GitHub-Hosted│ Self-Hosted    │
├────────────────────┼──────────────┼────────────────┤
│ CPU Performance    │ Baseline     │ 1.5-2x faster  │
│ Disk I/O           │ Networked    │ Local SSD      │
│ Cache Restoration  │ Network      │ Local (fast)   │
│ Docker Operations  │ Shared       │ Dedicated      │
│ Startup Overhead   │ ~10s         │ ~30-90s        │
└────────────────────┴──────────────┴────────────────┘

Speedup Breakdown (Self-Hosted t3.medium):
• Build: 1.5x faster (better CPU)
• Tests: 1.3x faster (local disk I/O)
• Cache: 3-5x faster (no network)
• Docker: 2-3x faster (local Docker socket)
• Overall: 1.5-2x faster (10-12 min → 5-7 min)
```

---

**Last Updated**: 2025-10-25
**Related Documentation**: [OPS-08 Technical Analysis](./ops-08-self-hosted-runners-analysis.md)
