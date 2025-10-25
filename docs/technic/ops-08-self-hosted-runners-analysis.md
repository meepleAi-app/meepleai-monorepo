# CI Performance Enhancement: Self-Hosted GitHub Actions Runners (OPS-08)

**Status**: Research & Documentation Complete
**Date**: 2025-10-25
**Related Issues**: OPS-06 (CI Optimization)

## Executive Summary

This document analyzes self-hosted GitHub Actions runners as an alternative to GitHub's paid larger runners for improving CI/CD performance in the MeepleAI monorepo. After OPS-06 optimizations, current CI pipeline runs in approximately 10-12 minutes on GitHub's Free tier (2-core, 8GB runners). This analysis explores whether self-hosted infrastructure can provide performance improvements and cost savings.

**Key Findings**:
- ✅ **Performance**: Self-hosted runners on equivalent hardware can be 2x faster than GitHub-hosted
- ⚠️ **Security**: Requires significant hardening; NOT recommended for public repositories
- 💰 **Cost**: Break-even at ~15+ minute CI times with 24/7 runner availability
- 🎯 **Recommendation**: **Document and defer implementation** until validated need

### Current State (Post-OPS-06)

| Metric | Value | Status |
|--------|-------|--------|
| **Total Pipeline Time** | ~10-12 minutes | ✅ Near target |
| **API Job** | ~7-9 minutes | ✅ Acceptable |
| **Web Job** | ~2-3 minutes | ✅ Good |
| **Free Tier Limit** | 2000 min/month | ✅ Sufficient |
| **Cache Hit Rate** | To be measured | ⏳ Pending |

### Decision: Wait and Measure

**Rationale**:
1. **OPS-06 just deployed** - Need to collect performance data over 10+ CI runs
2. **Free tier sufficient** - 2000 minutes/month likely covers current usage
3. **Security complexity** - Self-hosted runners require significant security infrastructure
4. **Management overhead** - Infrastructure requires ongoing maintenance and updates

**Trigger for Reconsideration**:
- CI consistently exceeds 15 minutes after OPS-06 optimizations stabilize
- Free tier limits become a bottleneck (monthly usage >1800 minutes)
- Team has dedicated DevOps capacity for infrastructure management
- Security review approves proposed architecture

---

## Table of Contents

1. [GitHub Runners Comparison](#github-runners-comparison)
2. [Self-Hosted Options Analysis](#self-hosted-options-analysis)
3. [Security Architecture](#security-architecture)
4. [Cost-Benefit Analysis](#cost-benefit-analysis)
5. [Implementation Options](#implementation-options)
6. [Decision Framework](#decision-framework)
7. [Implementation Roadmap](#implementation-roadmap)
8. [Maintenance Considerations](#maintenance-considerations)
9. [References](#references)

---

## GitHub Runners Comparison

### GitHub-Hosted Runners (Standard - FREE)

**Specifications** (ubuntu-latest):
- **CPU**: 2 cores (x86_64)
- **Memory**: 8 GB RAM
- **Storage**: 14 GB SSD
- **Network**: Azure-hosted, ~100 Mbps
- **Cost**: Free (2000 min/month limit)
- **Availability**: On-demand, automatic scaling

**Pros**:
- ✅ Zero setup and maintenance
- ✅ Automatic updates and security patches
- ✅ Unlimited concurrent jobs (within limits)
- ✅ Pre-installed software (Node, .NET, Docker, etc.)
- ✅ Built-in caching mechanisms

**Cons**:
- ❌ Limited to 2000 minutes/month on Free tier
- ❌ General-purpose hardware (not optimized for CI)
- ❌ Slower disk I/O (networked storage)
- ❌ Variable performance (shared infrastructure)
- ❌ No control over environment

### GitHub Larger Runners (PAID)

**Available Configurations**:

| Size | vCPU | RAM | Storage | Cost |
|------|------|-----|---------|------|
| **4-core** | 4 | 16 GB | 150 GB SSD | $0.08/min |
| **8-core** | 8 | 32 GB | 300 GB SSD | $0.16/min |
| **16-core** | 16 | 64 GB | 600 GB SSD | $0.32/min |
| **32-core** | 32 | 128 GB | 1200 GB SSD | $0.64/min |
| **64-core** | 64 | 256 GB | 2040 GB SSD | $1.28/min |

**Pros**:
- ✅ Significantly more powerful hardware
- ✅ Still zero maintenance
- ✅ Better disk I/O than standard runners
- ✅ Pre-installed software maintained by GitHub

**Cons**:
- ❌ Expensive at scale ($0.08-1.28/min)
- ❌ Requires GitHub Team or Enterprise plan
- ❌ Still on shared infrastructure
- ❌ Limited control over software versions

**Cost Example** (4-core runner):
- 10-minute CI run: $0.80
- 100 runs/month: $80/month
- 500 runs/month: $400/month

---

## Self-Hosted Options Analysis

### Option A: Docker-Based Self-Hosted Runner (Simplest)

**Architecture**:
- Single Docker container running GitHub Actions runner
- Deployed on VM, bare metal, or cloud instance
- Direct GitHub registration with repository/organization

**Specifications** (Example: AWS EC2 t3.medium):
- **CPU**: 2 vCPU (Intel Xeon, 3.1 GHz base)
- **Memory**: 4 GB RAM
- **Storage**: 30-100 GB GP3 SSD
- **Network**: Up to 5 Gbps
- **Cost**: ~$0.0416/hour ($30/month) + storage ($3-10/month)

**Performance vs GitHub-hosted**:
- ⚡ **Build time**: 1.5-2x faster (better CPU, local disk)
- ⚡ **Cache restoration**: 3-5x faster (no network I/O)
- ⚡ **Docker operations**: 2-3x faster (local Docker socket)

**Pros**:
- ✅ Simple setup (Docker Compose)
- ✅ Low cost (~$35-40/month)
- ✅ Full control over software versions
- ✅ Can run on existing infrastructure

**Cons**:
- ❌ Single point of failure (no HA)
- ❌ Manual scaling (no auto-scaling)
- ❌ Persistent runner (security risk if compromised)
- ❌ Requires manual updates and maintenance

**Security Risks**:
- 🔴 **CRITICAL**: Docker-in-Docker requires privileged mode
- 🔴 **HIGH**: Persistent runner can be compromised by malicious code
- 🟡 **MEDIUM**: Network access to host system

### Option B: Kubernetes + Actions Runner Controller (ARC) (Production-Ready)

**Architecture**:
- Kubernetes cluster with Actions Runner Controller (ARC) Helm chart
- Ephemeral runners (destroyed after each job)
- Auto-scaling based on workflow queue depth
- Pod-based isolation with network policies

**Specifications** (Example: AWS EKS):
- **Cluster**: EKS managed Kubernetes
- **Nodes**: 2-4x t3.medium instances (auto-scaling)
- **Storage**: GP3 EBS volumes
- **Network**: VPC with private subnets
- **Cost**: ~$75/month (cluster) + ~$40-120/month (nodes) = **$115-195/month**

**Performance vs GitHub-hosted**:
- ⚡ **Build time**: 2-3x faster (dedicated resources)
- ⚡ **Startup time**: 30-60s (pod creation overhead)
- ⚡ **Scaling**: Automatic (based on queue depth)

**Pros**:
- ✅ Ephemeral runners (destroyed after each job)
- ✅ Auto-scaling (cost-effective for variable load)
- ✅ High availability (multi-node cluster)
- ✅ Network isolation (Kubernetes network policies)
- ✅ Production-grade security

**Cons**:
- ❌ Complex setup (Kubernetes expertise required)
- ❌ Higher cost (~$115-195/month minimum)
- ❌ Cluster management overhead
- ❌ Slower startup (30-60s pod creation)

**Security Benefits**:
- ✅ Ephemeral runners (no persistence)
- ✅ Network policies (isolation from other pods)
- ✅ Pod security policies (non-root, limited capabilities)
- ✅ Secrets management (Kubernetes secrets)

### Option C: AWS EC2 Auto Scaling (Cloud-Native)

**Architecture**:
- EC2 Auto Scaling Group with GitHub runner AMI
- Ephemeral instances (terminated after job completion)
- AWS OIDC for credential management
- CloudWatch monitoring and alarms

**Specifications**:
- **Instances**: t3.medium or t3.large
- **Scaling**: Based on GitHub Actions webhook events
- **Storage**: GP3 EBS with fast snapshots
- **Network**: Private VPC with NAT gateway
- **Cost**: ~$40-80/month (instances) + ~$20/month (NAT) = **$60-100/month**

**Performance vs GitHub-hosted**:
- ⚡ **Build time**: 2-2.5x faster
- ⚡ **Startup time**: 60-90s (instance launch + runner registration)
- ⚡ **Scaling**: Event-driven (GitHub webhook)

**Pros**:
- ✅ Ephemeral instances (destroyed after each job)
- ✅ AWS-native (integrates with existing infrastructure)
- ✅ Cost-effective (pay only for running instances)
- ✅ Simple architecture (no Kubernetes)

**Cons**:
- ❌ AWS-specific (not portable)
- ❌ Slower startup than Kubernetes pods
- ❌ Requires AWS expertise
- ❌ NAT gateway cost (~$30/month)

**Security Benefits**:
- ✅ Ephemeral instances (no persistence)
- ✅ AWS IAM roles (short-lived credentials)
- ✅ VPC isolation (private subnets)
- ✅ AWS security groups (network firewall)

### Comparison Matrix

| Feature | GitHub-Hosted | GitHub Larger | Docker (Option A) | Kubernetes (Option B) | AWS ASG (Option C) |
|---------|---------------|---------------|-------------------|----------------------|--------------------|
| **Setup Complexity** | None | None | Low | High | Medium |
| **Monthly Cost** | $0 (Free tier) | $80-400+ | $35-40 | $115-195 | $60-100 |
| **Performance** | Baseline (1x) | 1.5-2x | 1.5-2x | 2-3x | 2-2.5x |
| **Security** | Managed | Managed | ⚠️ Risky | ✅ Strong | ✅ Strong |
| **Maintenance** | None | None | High | Medium | Medium |
| **Auto-Scaling** | Yes | Yes | No | Yes | Yes |
| **Ephemeral** | Yes | Yes | No | Yes | Yes |
| **Startup Time** | ~10s | ~10s | ~5s | ~30-60s | ~60-90s |

---

## Security Architecture

### Critical Security Requirements

⚠️ **NEVER use self-hosted runners on public repositories!**

Anyone can submit a malicious pull request to a public repository, gaining code execution on your infrastructure. Self-hosted runners are ONLY safe for private repositories with trusted contributors.

### Security Best Practices

#### 1. Ephemeral Runners (MANDATORY)

**Why**: Persistent runners can be compromised by malicious code and retain state between jobs.

**Implementation**:
- **Kubernetes (ARC)**: Runners are destroyed after each job automatically
- **AWS Auto Scaling**: Instances terminated after job completion
- **Docker**: NOT recommended (persistent by default)

**Configuration**:
```yaml
# Kubernetes ARC example
spec:
  ephemeral: true  # Destroy pod after job
  maxRunners: 10   # Scale up to 10 concurrent runners
  minRunners: 0    # Scale down to zero when idle
```

#### 2. Network Isolation (MANDATORY)

**Why**: Runners should NOT have access to sensitive internal resources.

**Implementation**:
- Dedicated VPC/subnet for runners (AWS)
- Network policies restricting outbound access (Kubernetes)
- Security groups allowing only GitHub webhooks and artifact storage

**Configuration**:
```yaml
# Kubernetes NetworkPolicy example
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: github-runners
spec:
  podSelector:
    matchLabels:
      app: github-runner
  policyTypes:
  - Egress
  egress:
  - to:
    - podSelector: {}
  - to:  # Allow GitHub API
    - namespaceSelector: {}
    ports:
    - protocol: TCP
      port: 443
```

#### 3. Runner Groups with Repository-Level Access (MANDATORY)

**Why**: Prevent runners from being used by unauthorized repositories.

**Implementation**:
- Create runner group in GitHub organization settings
- Assign specific repositories to runner group
- Use runner labels for additional granularity

**Configuration** (GitHub UI):
1. Settings → Actions → Runner groups
2. Create "MeepleAI-Private" runner group
3. Assign only `meepleai/monorepo` repository
4. Configure runner with label: `runs-on: [self-hosted, meepleai]`

#### 4. Disable Docker-in-Docker (RECOMMENDED)

**Why**: Docker-in-Docker requires privileged mode, which grants root access to host.

**Alternatives**:
- Use Kubernetes jobs for container builds (ARC feature)
- Use `docker:dind` service container (isolated)
- Use Kaniko for rootless container builds
- Avoid GitHub Actions that require Docker

**Configuration** (Kubernetes ARC with Kaniko):
```yaml
# Use Kaniko for rootless container builds
- name: Build Docker image
  uses: aevea/action-kaniko@master
  with:
    image: myapp
    tag: latest
    build_file: Dockerfile
```

#### 5. Short-Lived Credentials (MANDATORY)

**Why**: Static secrets can be exfiltrated if runner is compromised.

**Implementation**:
- AWS: Use OIDC with IAM roles (no static AWS keys)
- GitHub: Use `GITHUB_TOKEN` (auto-generated, expires after job)
- Secrets: Use Kubernetes secrets or AWS Secrets Manager

**Configuration** (GitHub OIDC with AWS):
```yaml
# .github/workflows/ci.yml
jobs:
  build:
    runs-on: self-hosted
    permissions:
      id-token: write  # Required for OIDC
      contents: read
    steps:
      - name: Configure AWS Credentials
        uses: aws-actions/configure-aws-credentials@v4
        with:
          role-to-assume: arn:aws:iam::123456789012:role/GitHubActionsRole
          aws-region: us-east-1
```

#### 6. Runner Updates and Security Patches (MANDATORY)

**Why**: Outdated runners have known vulnerabilities.

**Implementation**:
- **Kubernetes (ARC)**: Auto-updates runner version via Helm chart upgrades
- **AWS ASG**: Rebuild AMI monthly with latest runner + OS patches
- **Docker**: Manual updates required (use Dependabot for Docker images)

**Schedule**:
- Security patches: Weekly
- Runner version updates: Monthly
- OS upgrades: Quarterly

#### 7. Monitoring and Alerting (RECOMMENDED)

**Why**: Detect compromised runners or abnormal behavior.

**Metrics to Monitor**:
- Runner job queue time (should be <60s)
- Runner failure rate (should be <5%)
- Network egress volume (detect data exfiltration)
- CPU/memory usage (detect cryptominers)

**Alerting Rules**:
```yaml
# Prometheus alert example
- alert: SuspiciousRunnerNetworkEgress
  expr: rate(container_network_transmit_bytes_total{pod=~"runner-.*"}[5m]) > 100000000  # 100 MB/s
  for: 5m
  annotations:
    summary: "Runner {{ $labels.pod }} has high network egress"
    description: "Possible data exfiltration detected"
```

### Security Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                     GitHub Actions                          │
│                   (Workflow Trigger)                        │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      │ HTTPS (443)
                      │ Webhook
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                  Runner Controller                          │
│              (ARC or AWS Auto Scaling)                      │
└─────────────────────┬───────────────────────────────────────┘
                      │
                      │ Creates ephemeral runner
                      ▼
┌─────────────────────────────────────────────────────────────┐
│                 Isolated Runner Pod/Instance                │
│  ┌──────────────────────────────────────────────────────┐  │
│  │  Runner Process (ephemeral)                          │  │
│  │  - Pulls workflow code from GitHub                   │  │
│  │  - Executes job steps                                │  │
│  │  - Reports results back to GitHub                    │  │
│  └──────────────────────────────────────────────────────┘  │
│                                                              │
│  Security Boundaries:                                       │
│  ├─ Network Policy (egress only to GitHub + artifacts)     │
│  ├─ No Docker-in-Docker (use Kaniko or K8s jobs)           │
│  ├─ Short-lived AWS credentials (OIDC)                     │
│  ├─ Non-root container (runAsNonRoot: true)                │
│  └─ Destroyed after job completion                         │
└─────────────────────────────────────────────────────────────┘
```

---

## Cost-Benefit Analysis

### Current Costs (GitHub Free Tier)

- **Monthly Limit**: 2000 minutes/month
- **Cost**: $0/month
- **Current Usage** (estimated): ~600-800 minutes/month
  - Assuming 2-3 CI runs/day × 10-12 minutes/run × 30 days = 600-1080 min/month
- **Headroom**: 1200-1400 minutes remaining

**Conclusion**: Free tier is sufficient for current usage.

### Self-Hosted Infrastructure Costs

#### Option A: Docker on AWS EC2 t3.medium

| Component | Monthly Cost |
|-----------|-------------|
| EC2 instance (t3.medium, 24/7) | $30.37 |
| EBS storage (50 GB GP3) | $5.00 |
| Data transfer (5 GB/month) | $0.45 |
| **Total** | **$35.82/month** |

**Break-Even Analysis**:
- GitHub larger runners (4-core): $0.08/min
- Break-even: $35.82 / $0.08 = 448 minutes/month
- **Current usage**: 600-800 min/month → ✅ Self-hosted saves $12-29/month
- **BUT**: Does NOT account for maintenance time (~2-4 hours/month = $100-200 opportunity cost)

**Conclusion**: NOT cost-effective when accounting for maintenance overhead.

#### Option B: Kubernetes (EKS) + ARC

| Component | Monthly Cost |
|-----------|-------------|
| EKS cluster | $72.00 |
| 2x t3.medium nodes (24/7) | $60.74 |
| EBS storage (100 GB GP3) | $10.00 |
| NAT gateway | $32.40 |
| Data transfer | $5.00 |
| **Total** | **$180.14/month** |

**Break-Even Analysis**:
- GitHub larger runners (4-core): $0.08/min
- Break-even: $180.14 / $0.08 = 2,252 minutes/month
- **Current usage**: 600-800 min/month → ❌ NOT cost-effective

**Conclusion**: Only cost-effective if CI time >20 minutes consistently AND high job volume.

#### Option C: AWS Auto Scaling (Spot Instances)

| Component | Monthly Cost |
|-----------|-------------|
| 2x t3.medium spot (50% uptime) | $15.19 |
| EBS storage (50 GB GP3) | $5.00 |
| NAT gateway | $32.40 |
| Data transfer | $5.00 |
| **Total** | **$57.59/month** |

**Break-Even Analysis**:
- GitHub larger runners (4-core): $0.08/min
- Break-even: $57.59 / $0.08 = 720 minutes/month
- **Current usage**: 600-800 min/month → 🟡 Marginal savings

**Conclusion**: Slight cost savings but requires AWS expertise and maintenance.

### Total Cost of Ownership (TCO)

| Cost Factor | Docker (A) | Kubernetes (B) | AWS ASG (C) |
|-------------|------------|----------------|-------------|
| **Infrastructure** | $36/mo | $180/mo | $58/mo |
| **Maintenance** (2-4 hrs/mo @ $50/hr) | $100-200/mo | $100-200/mo | $100-200/mo |
| **Security Reviews** (quarterly) | $50/mo | $50/mo | $50/mo |
| **Incident Response** (estimated) | $25/mo | $25/mo | $25/mo |
| **Total TCO** | **$211-311/mo** | **$355-455/mo** | **$233-333/mo** |

**GitHub Larger Runners Equivalent**:
- 600-800 min/mo @ $0.08/min = $48-64/mo
- **Conclusion**: Self-hosted is 3-7x more expensive when accounting for maintenance!

### Performance ROI

**Scenario**: CI time reduced from 10 min → 5 min with self-hosted runners

**Developer Time Savings**:
- 2-3 CI runs/day/developer × 5 min savings = 10-15 min/day/developer
- 5 developers × 10-15 min/day = 50-75 min/day = **17-25 hours/month**
- Value @ $50/hr = $850-1,250/month

**ROI Calculation**:
- TCO: $211-455/month (depending on option)
- Time savings value: $850-1,250/month
- **Net benefit**: $395-1,039/month ✅ POSITIVE ROI

**Conclusion**: Self-hosted runners are cost-effective IF:
1. CI time can be reduced by 50%+ (10 min → 5 min)
2. Team has ≥5 developers making frequent CI runs
3. Maintenance overhead is minimized (e.g., using managed Kubernetes)

### Recommendation

**Current State** (Post-OPS-06):
- CI time: 10-12 minutes (acceptable)
- Free tier: Sufficient (2000 min/month)
- Team size: Unknown (assume 2-5 developers)

**Recommendation**: **Wait and measure**

**Action Items**:
1. **Measure OPS-06 impact** over next 10 CI runs
2. **Track CI usage** to validate free tier headroom
3. **Monitor developer wait times** for CI feedback
4. **Revisit OPS-08** if CI time >15 minutes consistently

**Trigger for Implementation**:
- CI time >15 minutes AND free tier exhausted AND team has DevOps capacity

---

## Implementation Options

### Option A: Docker-Based Runner (Quick Start)

**Use Case**: Proof-of-concept, development testing, single team

**Architecture**:
```
VM/Cloud Instance (t3.medium)
├── Docker Engine
└── GitHub Runner Container
    ├── Runner binary
    ├── Docker socket (for builds)
    └── Workspace volume
```

**Setup Steps**:

1. **Create VM** (AWS EC2, DigitalOcean, etc.):
   ```bash
   # AWS example
   aws ec2 run-instances \
     --image-id ami-0c55b159cbfafe1f0 \
     --instance-type t3.medium \
     --key-name my-key \
     --security-group-ids sg-12345678 \
     --subnet-id subnet-12345678 \
     --tag-specifications 'ResourceType=instance,Tags=[{Key=Name,Value=github-runner}]'
   ```

2. **Install Docker**:
   ```bash
   sudo apt-get update
   sudo apt-get install -y docker.io docker-compose
   sudo usermod -aG docker $USER
   ```

3. **Create Dockerfile**:
   ```dockerfile
   FROM ubuntu:22.04

   # Install dependencies
   RUN apt-get update && apt-get install -y \
       curl \
       git \
       jq \
       libicu-dev \
       sudo \
       && rm -rf /var/lib/apt/lists/*

   # Create runner user
   RUN useradd -m -s /bin/bash runner \
       && usermod -aG sudo runner \
       && echo "runner ALL=(ALL) NOPASSWD:ALL" >> /etc/sudoers

   # Install GitHub Actions runner
   WORKDIR /actions-runner
   RUN curl -o actions-runner-linux-x64-2.311.0.tar.gz -L \
       https://github.com/actions/runner/releases/download/v2.311.0/actions-runner-linux-x64-2.311.0.tar.gz \
       && tar xzf ./actions-runner-linux-x64-2.311.0.tar.gz \
       && chown -R runner:runner /actions-runner

   USER runner
   ENTRYPOINT ["/actions-runner/run.sh"]
   ```

4. **Configure Runner**:
   ```bash
   # Get registration token from GitHub
   # Organization: https://github.com/organizations/YOUR_ORG/settings/actions/runners/new
   # Repository: https://github.com/YOUR_ORG/YOUR_REPO/settings/actions/runners/new

   ./config.sh \
     --url https://github.com/YOUR_ORG/YOUR_REPO \
     --token YOUR_REGISTRATION_TOKEN \
     --name docker-runner-01 \
     --labels self-hosted,linux,x64,docker \
     --work _work \
     --ephemeral  # IMPORTANT: Destroy after each job
   ```

5. **Run Runner**:
   ```bash
   docker-compose up -d
   ```

**Docker Compose Example**:
```yaml
version: '3.8'
services:
  github-runner:
    build: .
    container_name: github-runner
    restart: unless-stopped
    environment:
      - RUNNER_NAME=docker-runner-01
      - RUNNER_WORKDIR=/actions-runner/_work
      - RUNNER_ALLOW_RUNASROOT=false
    volumes:
      - /var/run/docker.sock:/var/run/docker.sock  # Docker-in-Docker
      - runner-work:/actions-runner/_work
    security_opt:
      - no-new-privileges:true
    read_only: false

volumes:
  runner-work:
```

**Security Hardening**:
```bash
# 1. Disable root login
sudo passwd -l root

# 2. Configure firewall
sudo ufw default deny incoming
sudo ufw default allow outgoing
sudo ufw allow 22/tcp  # SSH only from your IP
sudo ufw enable

# 3. Install fail2ban
sudo apt-get install -y fail2ban
sudo systemctl enable fail2ban

# 4. Enable automatic security updates
sudo apt-get install -y unattended-upgrades
sudo dpkg-reconfigure -plow unattended-upgrades
```

**Pros**:
- ✅ Quick setup (~1 hour)
- ✅ Low cost (~$36/month)
- ✅ Simple troubleshooting

**Cons**:
- ❌ Persistent runner (security risk)
- ❌ No auto-scaling
- ❌ Single point of failure

---

### Option B: Kubernetes + Actions Runner Controller (ARC)

**Use Case**: Production workloads, auto-scaling, high availability

**Architecture**:
```
Kubernetes Cluster (EKS/GKE/AKS)
├── ARC Controller (manages runner pods)
├── Runner Scale Set (auto-scales based on queue)
└── Ephemeral Runner Pods
    ├── Pod 1 (job 1)
    ├── Pod 2 (job 2)
    └── Pod 3 (job 3)
```

**Setup Steps**:

1. **Create Kubernetes Cluster** (AWS EKS example):
   ```bash
   eksctl create cluster \
     --name github-runners \
     --region us-east-1 \
     --nodegroup-name runner-nodes \
     --node-type t3.medium \
     --nodes 2 \
     --nodes-min 1 \
     --nodes-max 5 \
     --managed
   ```

2. **Install Actions Runner Controller**:
   ```bash
   # Add Helm repo
   helm repo add actions-runner-controller \
     https://actions-runner-controller.github.io/actions-runner-controller

   # Create namespace
   kubectl create namespace actions-runner-system

   # Install cert-manager (required dependency)
   kubectl apply -f \
     https://github.com/cert-manager/cert-manager/releases/download/v1.13.0/cert-manager.yaml

   # Install ARC
   helm install actions-runner-controller \
     actions-runner-controller/actions-runner-controller \
     --namespace actions-runner-system \
     --set authSecret.github_token=YOUR_GITHUB_PAT
   ```

3. **Create Runner Scale Set**:
   ```yaml
   # runner-scale-set.yaml
   apiVersion: actions.summerwind.dev/v1alpha1
   kind: RunnerDeployment
   metadata:
     name: meepleai-runners
     namespace: actions-runner-system
   spec:
     template:
       spec:
         repository: meepleai/monorepo
         labels:
           - self-hosted
           - kubernetes
           - meepleai
         ephemeral: true  # Destroy pod after job
         dockerEnabled: false  # Disable Docker-in-Docker
         resources:
           requests:
             cpu: "1"
             memory: "2Gi"
           limits:
             cpu: "2"
             memory: "4Gi"
         securityContext:
           runAsNonRoot: true
           runAsUser: 1000
           fsGroup: 1000
   ---
   apiVersion: actions.summerwind.dev/v1alpha1
   kind: HorizontalRunnerAutoscaler
   metadata:
     name: meepleai-runners-autoscaler
     namespace: actions-runner-system
   spec:
     scaleTargetRef:
       name: meepleai-runners
     minReplicas: 0  # Scale to zero when idle
     maxReplicas: 10
     metrics:
       - type: TotalNumberOfQueuedAndInProgressWorkflowRuns
         repositoryNames:
           - meepleai/monorepo
   ```

4. **Apply Configuration**:
   ```bash
   kubectl apply -f runner-scale-set.yaml
   ```

5. **Monitor Runners**:
   ```bash
   # Check runner pods
   kubectl get pods -n actions-runner-system

   # Check autoscaler status
   kubectl get hra -n actions-runner-system

   # View logs
   kubectl logs -f -n actions-runner-system \
     -l app.kubernetes.io/name=actions-runner-controller
   ```

**Security Configuration**:
```yaml
# network-policy.yaml
apiVersion: networking.k8s.io/v1
kind: NetworkPolicy
metadata:
  name: runner-network-policy
  namespace: actions-runner-system
spec:
  podSelector:
    matchLabels:
      actions-runner-controller/inject-registration-token: "true"
  policyTypes:
  - Egress
  egress:
  - to:  # Allow GitHub API
    - namespaceSelector: {}
    ports:
    - protocol: TCP
      port: 443
  - to:  # Allow DNS
    - namespaceSelector:
        matchLabels:
          name: kube-system
    ports:
    - protocol: UDP
      port: 53
```

**Pros**:
- ✅ Ephemeral runners (security)
- ✅ Auto-scaling (cost-effective)
- ✅ High availability
- ✅ Production-grade

**Cons**:
- ❌ Complex setup (requires Kubernetes expertise)
- ❌ Higher cost (~$180/month)
- ❌ Slower startup (~30-60s)

---

### Option C: AWS EC2 Auto Scaling

**Use Case**: AWS-centric environments, simpler than Kubernetes

**Architecture**:
```
GitHub Webhook → Lambda → Auto Scaling Group
                          ├── EC2 Instance 1 (ephemeral)
                          ├── EC2 Instance 2 (ephemeral)
                          └── EC2 Instance 3 (ephemeral)
```

**Setup Steps**:

1. **Create Runner AMI**:
   ```bash
   # Launch base instance
   aws ec2 run-instances \
     --image-id ami-0c55b159cbfafe1f0 \
     --instance-type t3.medium \
     --key-name my-key

   # SSH into instance and install runner
   ssh ubuntu@instance-ip

   # Install dependencies
   sudo apt-get update
   sudo apt-get install -y curl git jq

   # Install GitHub runner
   mkdir actions-runner && cd actions-runner
   curl -o actions-runner-linux-x64-2.311.0.tar.gz -L \
     https://github.com/actions/runner/releases/download/v2.311.0/actions-runner-linux-x64-2.311.0.tar.gz
   tar xzf ./actions-runner-linux-x64-2.311.0.tar.gz

   # Create startup script
   sudo cat > /usr/local/bin/start-runner.sh <<'EOF'
   #!/bin/bash
   cd /actions-runner
   TOKEN=$(curl -X POST -H "Authorization: token $GITHUB_TOKEN" \
     https://api.github.com/repos/meepleai/monorepo/actions/runners/registration-token | jq -r .token)
   ./config.sh --url https://github.com/meepleai/monorepo --token $TOKEN --ephemeral
   ./run.sh
   EOF
   sudo chmod +x /usr/local/bin/start-runner.sh

   # Create systemd service
   sudo cat > /etc/systemd/system/github-runner.service <<'EOF'
   [Unit]
   Description=GitHub Actions Runner
   After=network.target

   [Service]
   Type=simple
   User=ubuntu
   WorkingDirectory=/actions-runner
   ExecStart=/usr/local/bin/start-runner.sh
   Restart=always

   [Install]
   WantedBy=multi-user.target
   EOF

   sudo systemctl enable github-runner

   # Create AMI
   aws ec2 create-image \
     --instance-id i-1234567890abcdef0 \
     --name github-runner-ami \
     --description "GitHub Actions Runner AMI"
   ```

2. **Create Auto Scaling Group**:
   ```bash
   # Create launch template
   aws ec2 create-launch-template \
     --launch-template-name github-runner-template \
     --version-description "v1" \
     --launch-template-data '{
       "ImageId": "ami-YOUR_RUNNER_AMI",
       "InstanceType": "t3.medium",
       "KeyName": "my-key",
       "SecurityGroupIds": ["sg-12345678"],
       "UserData": "IyEvYmluL2Jhc2gKc3VkbyBzeXN0ZW1jdGwgc3RhcnQgZ2l0aHViLXJ1bm5lcg==",
       "IamInstanceProfile": {
         "Name": "GitHubRunnerRole"
       },
       "TagSpecifications": [{
         "ResourceType": "instance",
         "Tags": [{"Key": "Name", "Value": "github-runner"}]
       }]
     }'

   # Create Auto Scaling Group
   aws autoscaling create-auto-scaling-group \
     --auto-scaling-group-name github-runners \
     --launch-template "LaunchTemplateName=github-runner-template,Version=1" \
     --min-size 0 \
     --max-size 10 \
     --desired-capacity 0 \
     --vpc-zone-identifier "subnet-12345678,subnet-87654321"
   ```

3. **Create Scaling Webhook** (Lambda):
   ```python
   # lambda_function.py
   import boto3
   import json

   autoscaling = boto3.client('autoscaling')

   def lambda_handler(event, context):
       # Parse GitHub webhook
       body = json.loads(event['body'])
       action = body.get('action')

       # Scale up when workflow queued
       if action == 'queued':
           autoscaling.set_desired_capacity(
               AutoScalingGroupName='github-runners',
               DesiredCapacity=1,
               HonorCooldown=False
           )

       return {'statusCode': 200}
   ```

**Pros**:
- ✅ Ephemeral instances (security)
- ✅ AWS-native (integrates with existing infra)
- ✅ Cost-effective (spot instances)

**Cons**:
- ❌ AWS-specific (not portable)
- ❌ Complex setup (Lambda, ASG, IAM)
- ❌ Slower startup (~60-90s)

---

## Decision Framework

Use this framework to decide whether to implement self-hosted runners:

### Decision Tree

```
START: Evaluate self-hosted runners
│
├─ Is CI time >15 minutes consistently? ───NO──> STOP: Stay on GitHub-hosted
│                                                        (Free tier sufficient)
└─ YES
   │
   ├─ Is free tier exhausted (>2000 min/month)? ───NO──> STOP: Stay on GitHub-hosted
   │                                                             (No cost pressure)
   └─ YES
      │
      ├─ Does team have DevOps capacity? ───NO──> CONSIDER: GitHub larger runners
      │                                                      (Less maintenance)
      └─ YES
         │
         ├─ Are repositories private? ───NO──> STOP: NEVER use self-hosted on public repos
         │                                            (Security risk)
         └─ YES
            │
            └─> IMPLEMENT: Self-hosted runners
                ├─ Simple setup → Option A (Docker)
                ├─ Production-grade → Option B (Kubernetes)
                └─ AWS-native → Option C (EC2 Auto Scaling)
```

### Evaluation Checklist

Use this checklist to validate readiness for self-hosted runners:

#### Performance Requirements ✅/❌

- [ ] CI time consistently >15 minutes
- [ ] Free tier exhausted (>2000 min/month)
- [ ] Developer feedback indicates CI is a bottleneck
- [ ] OPS-06 optimizations measured and validated

#### Security Requirements ✅/❌

- [ ] All repositories are private (or public with approval)
- [ ] Security review completed and approved
- [ ] Team trained on self-hosted runner security risks
- [ ] Incident response plan documented

#### Infrastructure Requirements ✅/❌

- [ ] Team has DevOps/SRE capacity (2-4 hours/month maintenance)
- [ ] Cloud infrastructure available (AWS/GCP/Azure or on-prem)
- [ ] Monitoring and alerting infrastructure ready
- [ ] Backup and disaster recovery plan

#### Cost Requirements ✅/❌

- [ ] Budget approved for infrastructure costs ($35-200/month)
- [ ] Budget approved for maintenance time (~$100-200/month opportunity cost)
- [ ] ROI analysis completed and positive
- [ ] Management buy-in secured

### Score Your Readiness

| Category | Weight | Score (0-10) | Weighted Score |
|----------|--------|--------------|----------------|
| **Performance Need** | 30% | ___ | ___ |
| **Security Readiness** | 30% | ___ | ___ |
| **Infrastructure Capacity** | 20% | ___ | ___ |
| **Cost Justification** | 20% | ___ | ___ |
| **Total** | 100% | | ___ |

**Interpretation**:
- **0-4**: Not ready (stay on GitHub-hosted)
- **5-6**: Marginal (consider GitHub larger runners)
- **7-8**: Ready (proceed with implementation)
- **9-10**: Highly ready (expedite implementation)

---

## Implementation Roadmap

If the decision framework indicates readiness, follow this phased roadmap:

### Phase 1: Proof of Concept (2 weeks)

**Goal**: Validate performance improvement and identify issues

**Tasks**:
1. **Setup test environment**:
   - Option A (Docker): Single VM with GitHub runner
   - Private test repository (NOT production)
   - Basic monitoring (CloudWatch or Prometheus)

2. **Run benchmarks**:
   - Execute 10 identical CI runs on GitHub-hosted
   - Execute 10 identical CI runs on self-hosted
   - Compare average execution time
   - Document any failures or issues

3. **Security review**:
   - Validate network isolation
   - Test ephemeral runner destruction
   - Review audit logs
   - Penetration testing (optional)

4. **Document findings**:
   - Performance metrics
   - Cost actuals vs estimates
   - Security concerns identified
   - Maintenance effort required

**Success Criteria**:
- ✅ Self-hosted runners are 1.5-2x faster than GitHub-hosted
- ✅ No security vulnerabilities identified
- ✅ Zero unplanned downtime
- ✅ Team comfortable with maintenance procedures

**Go/No-Go Decision**: Proceed to Phase 2 only if all success criteria met.

---

### Phase 2: Security Hardening (2 weeks)

**Goal**: Production-grade security before rollout

**Tasks**:
1. **Network isolation**:
   - Dedicated VPC/subnet for runners
   - Security groups allowing only required traffic
   - Network policies (Kubernetes) or NACLs (AWS)

2. **Access control**:
   - Runner groups with repository-level access
   - IAM roles with least-privilege permissions
   - OIDC for short-lived credentials

3. **Monitoring and alerting**:
   - CloudWatch/Prometheus metrics for runners
   - Alerts for high network egress, CPU spikes, failures
   - Log aggregation to Seq or Elasticsearch

4. **Incident response**:
   - Document runbook for compromised runner
   - Automated runner rotation (daily/weekly)
   - Backup runner registration tokens

5. **Security audit**:
   - Third-party security review (optional)
   - Compliance check (SOC 2, ISO 27001, etc.)
   - Management sign-off

**Success Criteria**:
- ✅ Security review approved
- ✅ All alerts functional and tested
- ✅ Incident response runbook documented
- ✅ Management approval secured

---

### Phase 3: Production Deployment (2 weeks)

**Goal**: Gradual rollout to production repository

**Tasks**:
1. **Staged rollout**:
   - Week 1: 25% of CI runs on self-hosted (use workflow conditionals)
   - Week 2: 50% of CI runs on self-hosted
   - Week 3: 75% of CI runs on self-hosted
   - Week 4: 100% of CI runs on self-hosted

2. **Workflow configuration**:
   ```yaml
   # .github/workflows/ci.yml
   jobs:
     ci-api:
       runs-on: ${{ github.event_name == 'pull_request' && 'self-hosted' || 'ubuntu-latest' }}
       # Use self-hosted for PRs, GitHub-hosted for main branch (safety)
   ```

3. **Monitor closely**:
   - Daily review of CI failures
   - Weekly cost tracking
   - Weekly performance metrics review

4. **Optimize**:
   - Tune auto-scaling parameters
   - Adjust runner resources (CPU/memory)
   - Optimize Docker caching

**Success Criteria**:
- ✅ CI failure rate <5%
- ✅ Average CI time reduced by 30-50%
- ✅ Cost within budget
- ✅ Zero security incidents

---

### Phase 4: Optimization & Maintenance (Ongoing)

**Goal**: Continuous improvement and stable operations

**Monthly Tasks**:
1. **Security updates**:
   - Update runner version (monthly)
   - Apply OS security patches (weekly)
   - Rotate runner registration tokens (quarterly)

2. **Cost optimization**:
   - Review actual costs vs budget
   - Tune auto-scaling (min/max runners)
   - Consider spot instances (AWS) for cost savings

3. **Performance tuning**:
   - Analyze slow CI runs (>15 minutes)
   - Optimize Docker layer caching
   - Adjust runner resources based on usage

4. **Capacity planning**:
   - Track CI volume trends
   - Plan for scale-up (more runners)
   - Budget for infrastructure growth

**Quarterly Tasks**:
- Security audit and penetration testing
- Disaster recovery drill (restore from backup)
- Team training on runner operations
- Management review of ROI

---

## Maintenance Considerations

### Ongoing Maintenance (Monthly Effort: 2-4 hours)

#### 1. Runner Version Updates (1 hour/month)

**Why**: GitHub frequently releases runner updates with security patches and bug fixes.

**Tasks**:
- Check for new runner versions: https://github.com/actions/runner/releases
- Update Docker image or AMI with new runner version
- Test in non-production environment
- Roll out to production

**Frequency**: Monthly (or immediately for critical security updates)

#### 2. OS Security Patches (30 min/week)

**Why**: Vulnerabilities in base OS can compromise runners.

**Tasks**:
- **Docker**: Rebuild images with updated base image
  ```bash
  docker pull ubuntu:22.04
  docker build -t github-runner:latest .
  ```
- **Kubernetes**: Use automated image update tools (e.g., Renovate)
- **AWS ASG**: Rebuild AMI with latest OS patches

**Frequency**: Weekly

#### 3. Monitoring and Alerting (1 hour/month)

**Why**: Detect issues before they impact CI/CD.

**Tasks**:
- Review alert history (false positives, missed alerts)
- Tune alert thresholds based on observed metrics
- Add new alerts for emerging patterns
- Test alert delivery (PagerDuty, Slack, email)

**Frequency**: Monthly review

#### 4. Cost Tracking (30 min/month)

**Why**: Prevent cost overruns and identify optimization opportunities.

**Tasks**:
- Review AWS/GCP/Azure billing
- Compare actual costs vs budget
- Identify cost anomalies (e.g., instance left running)
- Optimize resource allocation

**Tools**:
- AWS Cost Explorer
- CloudWatch billing alarms
- Kubernetes cost attribution tools (e.g., Kubecost)

**Frequency**: Monthly

#### 5. Capacity Planning (1 hour/quarter)

**Why**: Ensure sufficient runner capacity for growing CI/CD demand.

**Tasks**:
- Analyze CI volume trends (jobs/day, minutes/day)
- Forecast future capacity needs
- Plan for scale-up (more runners, larger instances)
- Budget for infrastructure growth

**Frequency**: Quarterly

### Incident Response

#### Scenario 1: Runner Failure (MTTR: 5-10 minutes)

**Symptoms**:
- CI jobs queued but not starting
- Alert: "No available runners"

**Resolution**:
1. Check runner status: `kubectl get pods -n actions-runner-system` (Kubernetes) or AWS ASG dashboard
2. Restart failed runners: `kubectl delete pod <pod-name>` (Kubernetes) or terminate instance (AWS)
3. If persistent, check runner logs for errors
4. Escalate to GitHub support if issue is with GitHub Actions API

#### Scenario 2: Compromised Runner (MTTR: 15-30 minutes)

**Symptoms**:
- Alert: "Suspicious network egress"
- Alert: "High CPU usage on runner"
- Unusual activity in audit logs

**Resolution** (CRITICAL):
1. **Immediate**: Terminate compromised runner
   ```bash
   # Kubernetes
   kubectl delete pod <pod-name> -n actions-runner-system

   # AWS
   aws ec2 terminate-instances --instance-ids i-1234567890abcdef0
   ```
2. **Isolate**: Remove runner from GitHub organization/repository
3. **Investigate**: Review audit logs, network traffic, process list
4. **Rotate secrets**: Rotate all secrets accessible to runner (API keys, tokens)
5. **Post-mortem**: Document incident, improve security controls

#### Scenario 3: High Cost Alert (MTTR: 1-2 hours)

**Symptoms**:
- AWS billing alert: "Cost exceeded $X"
- More runners than expected

**Resolution**:
1. Identify cause: Check CloudWatch for instance count, Auto Scaling Group activity
2. Verify auto-scaling is functioning correctly (scale-down after jobs complete)
3. Check for stuck runners (instances not terminating)
4. Manually scale down if necessary
5. Review auto-scaling configuration and tune parameters

### Automation Opportunities

Reduce maintenance burden with automation:

#### Automated Runner Updates
```yaml
# Renovate configuration for Docker image updates
{
  "extends": ["config:base"],
  "dockerfile": {
    "enabled": true
  },
  "labels": ["dependencies", "runner-update"],
  "assignees": ["@devops-team"]
}
```

#### Automated Security Patching (AWS ASG)
```bash
# EventBridge rule to rebuild AMI weekly
aws events put-rule \
  --name weekly-ami-rebuild \
  --schedule-expression "cron(0 2 ? * SUN *)" \
  --state ENABLED

# Lambda function to rebuild AMI
def rebuild_ami(event, context):
    # Launch instance with latest Ubuntu AMI
    # Install runner and dependencies
    # Create new AMI
    # Update Auto Scaling Group launch template
```

#### Automated Cost Optimization
```bash
# Lambda function to analyze runner usage and recommend instance type changes
def analyze_runner_usage(event, context):
    # Query CloudWatch metrics
    # Calculate average CPU/memory utilization
    # Recommend smaller/larger instance types
    # Send report to Slack
```

---

## References

### Official Documentation

- **GitHub Actions Self-Hosted Runners**: https://docs.github.com/en/actions/hosting-your-own-runners
- **Actions Runner Controller (ARC)**: https://github.com/actions/actions-runner-controller
- **GitHub Larger Runners**: https://docs.github.com/en/actions/using-github-hosted-runners/about-larger-runners
- **GitHub Actions Security**: https://docs.github.com/en/actions/security-guides/security-hardening-for-github-actions

### Security Best Practices

- **LegitSecurity: Self-Hosted Runner Dangers**: https://www.legitsecurity.com/blog/securing-your-ci/cd-pipeline-exploring-the-dangers-of-self-hosted-agents
- **Praetorian: Self-Hosted Runners as Backdoors**: https://www.praetorian.com/blog/self-hosted-github-runners-are-backdoors/
- **GitHub Security Guides**: https://github.com/dduzgun-security/github-self-hosted-runners

### Performance & Cost Analysis

- **WarpBuild: Self-Hosting Guide**: https://www.warpbuild.com/blog/self-hosting-github-actions
- **AWS Best Practices**: https://aws.amazon.com/blogs/devops/best-practices-working-with-self-hosted-github-action-runners-at-scale-on-aws/
- **RunsOn: CPU Performance Benchmarks**: https://runs-on.com/benchmarks/github-actions-cpu-performance/

### MeepleAI Internal Documentation

- **OPS-06: CI Optimization**: `docs/issue/ops-06-ci-optimization.md`
- **CI Workflow**: `.github/workflows/ci.yml`
- **Observability**: `docs/observability.md`
- **CLAUDE.md**: Project development guide

---

## Conclusion

Self-hosted GitHub Actions runners offer significant performance improvements (2-3x faster) but come with substantial security risks and maintenance overhead. For MeepleAI's current state (post-OPS-06), **the recommendation is to document and defer implementation** until there is validated need (CI time >15 minutes consistently AND free tier exhausted).

**Key Takeaways**:
1. ✅ **Document now**: Preserve research for future decision-making
2. ⏳ **Measure first**: Collect OPS-06 performance data over 10+ runs
3. 🎯 **Revisit later**: Only implement if clear performance or cost need emerges
4. 🔒 **Security first**: Never compromise security for marginal performance gains

**Next Steps**:
1. Monitor OPS-06 impact on CI time
2. Track free tier usage to validate headroom
3. Revisit OPS-08 in Q1 2026 with actual usage data
4. Consider GitHub larger runners as intermediate step before self-hosted

---

**Last Updated**: 2025-10-25
**Author**: Claude Code (system-architect + technical-writer agents)
**Version**: 1.0
