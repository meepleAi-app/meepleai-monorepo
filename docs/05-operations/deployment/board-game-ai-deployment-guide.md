# MeepleAI Deployment Guide

**Status**: Approved for Phase 1-2 Implementation
**Version**: 1.0
**Date**: 2025-01-15
**Owner**: DevOps & Engineering Team

---

## Deployment Strategy Overview

MeepleAI utilizes a **progressive deployment strategy** evolving across phases:

- **Phase 1 (MVP)**: Docker Compose on DigitalOcean App Platform (simple, fast iteration)
- **Phase 2 (Production)**: Kubernetes on AWS EKS or DigitalOcean (scalable, HA)
- **Phase 3+**: Multi-region, auto-scaling, advanced observability

---

## Phase 1: Docker Compose Deployment (MVP)

### Prerequisites

- Docker 24+ and Docker Compose 2.20+
- DigitalOcean account (or equivalent: Heroku, Render, Railway)
- Domain name (optional for MVP: app.meepleai.dev)
- OpenAI API key, Claude API key (Phase 2)

### Local Development Setup

**1. Clone Repository**:
```bash
git clone https://github.com/meepleai/meepleai-mono repo.git
cd meepleai-monorepo
```

**2. Environment Configuration**:
```bash
# Copy example env files
cp infra/env/.env.example infra/env/.env.dev

# Edit .env.dev with your API keys
vim infra/env/.env.dev
```

**`.env.dev` Content**:
```bash
# API Keys
OPENROUTER_API_KEY=sk-or-v1-...

# Database
POSTGRES_URL=postgresql://meepleai:secret@meepleai-postgres:5432/meepleai
POSTGRES_USER=meepleai
POSTGRES_PASSWORD=secret
POSTGRES_DB=meepleai

# Redis
REDIS_URL=redis://meepleai-redis:6379

# Weaviate
WEAVIATE_URL=http://weaviate:8080

# Application
API_BASE_URL=http://localhost:8000
FRONTEND_BASE_URL=http://localhost:3000
LOG_LEVEL=INFO
ENVIRONMENT=development

# Monitoring (Phase 2)
SEQ_URL=http://seq:5341
JAEGER_ENDPOINT=http://jaeger:14268/api/traces
```

**3. Start Services**:
```bash
cd infra
docker compose up -d

# Check service health
docker compose ps

# View logs
docker compose logs -f meepleai-api
```

**4. Verify Services**:
```bash
# API health
curl http://localhost:8000/health

# Expected: {"status": "healthy", "timestamp": "..."}

# Frontend
open http://localhost:3000

# Weaviate
curl http://localhost:8080/v1/.well-known/ready

# PostgreSQL
docker compose exec meepleai-postgres psql -U meepleai -d meepleai -c "SELECT version();"
```

---

### Staging Deployment (DigitalOcean App Platform)

**1. Prepare Repo**:
```bash
# Create production Docker Compose
cp docker-compose.yml docker-compose.prod.yml

# Edit for production settings (remove dev volumes, add health checks)
vim docker-compose.prod.yml
```

**2. Create DigitalOcean App**:
```bash
# Install doctl (DigitalOcean CLI)
brew install doctl  # macOS
# or: snap install doctl  # Linux

# Authenticate
doctl auth init

# Create app from spec
doctl apps create --spec infra/digitalocean/app.yaml
```

**`app.yaml` Example**:
```yaml
name: meepleai-staging
region: fra  # Frankfurt (closest to Italy)

services:
  - name: api
    github:
      repo: meepleai/meepleai-monorepo
      branch: main
      deploy_on_push: true
    dockerfile_path: apps/api/Dockerfile
    health_check:
      http_path: /health
    envs:
      - key: OPENROUTER_API_KEY
        scope: RUN_TIME
        type: SECRET
        value: ${OPENROUTER_API_KEY}
      - key: POSTGRES_URL
        scope: RUN_TIME
        value: ${db.DATABASE_URL}  # Auto-injected from DB component
    http_port: 8000
    instance_count: 2
    instance_size_slug: basic-xs  # $5/month per instance

  - name: web
    github:
      repo: meepleai/meepleai-monorepo
      branch: main
    dockerfile_path: apps/web/Dockerfile
    envs:
      - key: NEXT_PUBLIC_API_BASE
        value: https://api-meepleai-staging.ondigitalocean.app
    http_port: 3000
    instance_count: 1
    instance_size_slug: basic-xs

databases:
  - name: db
    engine: PG
    version: "16"
    size: db-s-1vcpu-1gb  # $15/month

  - name: redis
    engine: REDIS
    version: "7"
    size: db-s-1vcpu-1gb  # $15/month
```

**3. Deploy**:
```bash
# Push to main branch triggers auto-deploy
git push origin main

# Monitor deployment
doctl apps logs <app-id> --follow

# Check app status
doctl apps get <app-id>
```

**4. Domain Setup** (Optional):
```bash
# Add custom domain
doctl apps create-domain <app-id> --domain staging.meepleai.dev

# Point DNS (Cloudflare/Namecheap)
# A record: staging.meepleai.dev → <DO app IP>
```

---

## Phase 2: Kubernetes Deployment (Production)

### Prerequisites

- Kubernetes cluster (AWS EKS, DigitalOcean Kubernetes, GKE)
- kubectl installed and configured
- Helm 3+ installed
- Terraform 1.5+ (for IaC)
- Domain + SSL certificate (Let's Encrypt via cert-manager)

### Infrastructure as Code (Terraform)

**1. AWS EKS Setup**:
```hcl
# infra/terraform/main.tf
terraform {
  required_version = ">= 1.5"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
  backend "s3" {
    bucket = "meepleai-terraform-state"
    key    = "production/terraform.tfstate"
    region = "eu-central-1"
  }
}

provider "aws" {
  region = "eu-central-1"  # Frankfurt (EU, low latency to Italy)
}

module "vpc" {
  source  = "terraform-aws-modules/vpc/aws"
  version = "5.1.0"

  name = "meepleai-production-vpc"
  cidr = "10.0.0.0/16"

  azs             = ["eu-central-1a", "eu-central-1b", "eu-central-1c"]
  private_subnets = ["10.0.1.0/24", "10.0.2.0/24", "10.0.3.0/24"]
  public_subnets  = ["10.0.101.0/24", "10.0.102.0/24", "10.0.103.0/24"]

  enable_nat_gateway = true
  enable_dns_hostnames = true

  tags = {
    Environment = "production"
    Project     = "meepleai"
  }
}

module "eks" {
  source  = "terraform-aws-modules/eks/aws"
  version = "19.16.0"

  cluster_name    = "meepleai-production"
  cluster_version = "1.28"

  vpc_id     = module.vpc.vpc_id
  subnet_ids = module.vpc.private_subnets

  eks_managed_node_groups = {
    general = {
      desired_size = 3
      min_size     = 2
      max_size     = 10

      instance_types = ["t3.medium"]  # 2 vCPU, 4 GB RAM
      capacity_type  = "ON_DEMAND"
    }
  }

  tags = {
    Environment = "production"
    Project     = "meepleai"
  }
}

module "rds" {
  source = "terraform-aws-modules/rds/aws"

  identifier = "meepleai-production-db"

  engine               = "postgres"
  engine_version       = "16.1"
  instance_class       = "db.t3.medium"
  allocated_storage    = 100
  max_allocated_storage = 500  # Auto-scaling up to 500 GB

  db_name  = "meepleai"
  username = "meepleai"
  password = var.db_password  # From Secrets Manager

  multi_az               = true  # High availability
  backup_retention_period = 7
  backup_window          = "03:00-04:00"
  maintenance_window     = "Mon:04:00-Mon:05:00"

  vpc_security_group_ids = [aws_security_group.rds.id]
  db_subnet_group_name   = aws_db_subnet_group.rds.name

  tags = {
    Environment = "production"
  }
}

module "elasticache_redis" {
  source = "terraform-aws-modules/elasticache/aws"

  cluster_id           = "meepleai-redis"
  engine               = "redis"
  engine_version       = "7.0"
  node_type            = "cache.t3.medium"
  num_cache_nodes      = 3  # Cluster mode
  parameter_group_name = "default.redis7.cluster.on"

  subnet_ids = module.vpc.private_subnets
  vpc_id     = module.vpc.vpc_id

  tags = {
    Environment = "production"
  }
}
```

**2. Deploy Infrastructure**:
```bash
cd infra/terraform

# Initialize
terraform init

# Plan
terraform plan -out=tfplan

# Apply
terraform apply tfplan

# Get kubeconfig
aws eks update-kubeconfig --region eu-central-1 --name meepleai-production
```

---

### Kubernetes Manifests

**1. API Deployment**:
```yaml
# k8s/api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: meepleai-api
  namespace: meepleai
spec:
  replicas: 3
  selector:
    matchLabels:
      app: meepleai-api
  template:
    metadata:
      labels:
        app: meepleai-api
        version: v1.2.0
    spec:
      containers:
      - name: api
        image: meepleai/api:v1.2.0
        ports:
        - containerPort: 8000
        env:
        - name: POSTGRES_URL
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: postgres-url
        - name: OPENROUTER_API_KEY
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: openrouter-api-key
        - name: REDIS_URL
          value: "redis://meepleai-redis-cluster:6379"
        - name: WEAVIATE_URL
          value: "http://weaviate:8080"
        - name: LOG_LEVEL
          value: "INFO"
        - name: ENVIRONMENT
          value: "production"
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "2Gi"
            cpu: "2000m"
        livenessProbe:
          httpGet:
            path: /health/live
            port: 8000
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 8000
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 2
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
          - weight: 100
            podAffinityTerm:
              labelSelector:
                matchExpressions:
                - key: app
                  operator: In
                  values:
                  - meepleai-api
              topologyKey: kubernetes.io/hostname
---
apiVersion: v1
kind: Service
metadata:
  name: meepleai-api
  namespace: meepleai
spec:
  selector:
    app: meepleai-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8000
  type: ClusterIP
```

**2. Weaviate StatefulSet**:
```yaml
# k8s/weaviate-statefulset.yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: weaviate
  namespace: meepleai
spec:
  serviceName: weaviate
  replicas: 3
  selector:
    matchLabels:
      app: weaviate
  template:
    metadata:
      labels:
        app: weaviate
    spec:
      containers:
      - name: weaviate
        image: semitechnologies/weaviate:1.23.0
        ports:
        - containerPort: 8080
        env:
        - name: PERSISTENCE_DATA_PATH
          value: "/var/lib/weaviate"
        - name: QUERY_DEFAULTS_LIMIT
          value: "100"
        - name: AUTHENTICATION_ANONYMOUS_ACCESS_ENABLED
          value: "false"
        - name: AUTHENTICATION_APIKEY_ENABLED
          value: "true"
        - name: AUTHENTICATION_APIKEY_ALLOWED_KEYS
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: weaviate-api-key
        volumeMounts:
        - name: weaviate-data
          mountPath: /var/lib/weaviate
        resources:
          requests:
            memory: "4Gi"
            cpu: "1000m"
          limits:
            memory: "16Gi"
            cpu: "4000m"
  volumeClaimTemplates:
  - metadata:
      name: weaviate-data
    spec:
      accessModes: ["ReadWriteOnce"]
      storageClassName: gp3  # AWS EBS gp3 (faster than gp2)
      resources:
        requests:
          storage: 100Gi
---
apiVersion: v1
kind: Service
metadata:
  name: weaviate
  namespace: meepleai
spec:
  selector:
    app: weaviate
  ports:
  - protocol: TCP
    port: 8080
    targetPort: 8080
  clusterIP: None  # Headless service for StatefulSet
```

**3. Ingress (NGINX + cert-manager)**:
```yaml
# k8s/ingress.yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: meepleai-ingress
  namespace: meepleai
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/rate-limit: "100"  # 100 req/min per IP
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - api.meepleai.dev
    - app.meepleai.dev
    secretName: meepleai-tls
  rules:
  - host: api.meepleai.dev
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: meepleai-api
            port:
              number: 80
  - host: app.meepleai.dev
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: meepleai-web
            port:
              number: 3000
```

**4. Horizontal Pod Autoscaler**:
```yaml
# k8s/hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: meepleai-api-hpa
  namespace: meepleai
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: meepleai-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleUp:
      stabilizationWindowSeconds: 60
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Pods
        value: 1
        periodSeconds: 60
```

---

### Deploy to Kubernetes

```bash
# Create namespace
kubectl create namespace meepleai

# Create secrets
kubectl create secret generic meepleai-secrets \
  --from-literal=postgres-url='postgresql://...' \
  --from-literal=openai-api-key='sk-proj-...' \
  --from-literal=claude-api-key='sk-ant-...' \
  --from-literal=weaviate-api-key='...' \
  -n meepleai

# Apply manifests
kubectl apply -f k8s/weaviate-statefulset.yaml
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/web-deployment.yaml
kubectl apply -f k8s/hpa.yaml
kubectl apply -f k8s/ingress.yaml

# Check deployment status
kubectl get pods -n meepleai
kubectl get svc -n meepleai
kubectl get ingress -n meepleai

# View logs
kubectl logs -f deployment/meepleai-api -n meepleai
```

---

## Monitoring & Observability (Phase 2)

### Prometheus + Grafana

**1. Install Prometheus Operator via Helm**:
```bash
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm repo update

helm install prometheus prometheus-community/kube-prometheus-stack \
  --namespace monitoring \
  --create-namespace \
  --set prometheus.prometheusSpec.retention=30d \
  --set prometheus.prometheusSpec.storageSpec.volumeClaimTemplate.spec.resources.requests.storage=100Gi
```

**2. Custom ServiceMonitor**:
```yaml
# k8s/monitoring/servicemonitor.yaml
apiVersion: monitoring.coreos.com/v1
kind: ServiceMonitor
metadata:
  name: meepleai-api
  namespace: monitoring
spec:
  selector:
    matchLabels:
      app: meepleai-api
  endpoints:
  - port: http
    path: /metrics
    interval: 30s
```

**3. Grafana Dashboards**:
- Import dashboard ID 14282 (Kubernetes cluster monitoring)
- Custom dashboard: `docs/monitoring/grafana-dashboard.json`
- Access: `kubectl port-forward -n monitoring svc/prometheus-grafana 3000:80`

---

### Alerting (PagerDuty Integration)

**1. Create PagerDuty Service**:
- Login to PagerDuty → Services → New Service
- Integration: "Prometheus" → Copy integration key

**2. Configure Alertmanager**:
```yaml
# k8s/monitoring/alertmanager-config.yaml
apiVersion: v1
kind: Secret
metadata:
  name: alertmanager-config
  namespace: monitoring
stringData:
  alertmanager.yml: |
    global:
      resolve_timeout: 5m
    route:
      group_by: ['alertname', 'cluster']
      group_wait: 10s
      group_interval: 10s
      repeat_interval: 12h
      receiver: 'pagerduty'
    receivers:
    - name: 'pagerduty'
      pagerduty_configs:
      - service_key: '<your-pagerduty-integration-key>'
        description: '{{ .CommonAnnotations.summary }}'
```

**3. Apply**:
```bash
kubectl apply -f k8s/monitoring/alertmanager-config.yaml
```

---

## CI/CD Pipeline (GitHub Actions)

**`.github/workflows/deploy.yml`**:
```yaml
name: Deploy to Production

on:
  push:
    branches: [main]
    tags: ['v*']

jobs:
  build-and-push:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Configure AWS credentials
        uses: aws-actions/configure-aws-credentials@v2
        with:
          aws-access-key-id: ${{ secrets.AWS_ACCESS_KEY_ID }}
          aws-secret-access-key: ${{ secrets.AWS_SECRET_ACCESS_KEY }}
          aws-region: eu-central-1

      - name: Login to Amazon ECR
        id: login-ecr
        uses: aws-actions/amazon-ecr-login@v1

      - name: Build and push API image
        env:
          ECR_REGISTRY: ${{ steps.login-ecr.outputs.registry }}
          IMAGE_TAG: ${{ github.sha }}
        run: |
          docker build -t $ECR_REGISTRY/meepleai-api:$IMAGE_TAG apps/api
          docker push $ECR_REGISTRY/meepleai-api:$IMAGE_TAG
          docker tag $ECR_REGISTRY/meepleai-api:$IMAGE_TAG $ECR_REGISTRY/meepleai-api:latest
          docker push $ECR_REGISTRY/meepleai-api:latest

  deploy:
    needs: build-and-push
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Configure kubectl
        run: |
          aws eks update-kubeconfig --region eu-central-1 --name meepleai-production

      - name: Deploy to Kubernetes
        env:
          IMAGE_TAG: ${{ github.sha }}
        run: |
          kubectl set image deployment/meepleai-api \
            api=${{ steps.login-ecr.outputs.registry }}/meepleai-api:$IMAGE_TAG \
            -n meepleai
          kubectl rollout status deployment/meepleai-api -n meepleai --timeout=5m

      - name: Notify Slack
        if: always()
        uses: slackapi/slack-github-action@v1
        with:
          payload: |
            {
              "text": "Deployment ${{ job.status }}: ${{ github.sha }}"
            }
        env:
          SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_URL }}
```

---

## Rollback Strategy

### Manual Rollback

```bash
# List deployment history
kubectl rollout history deployment/meepleai-api -n meepleai

# Rollback to previous version
kubectl rollout undo deployment/meepleai-api -n meepleai

# Rollback to specific revision
kubectl rollout undo deployment/meepleai-api --to-revision=3 -n meepleai

# Verify rollback
kubectl rollout status deployment/meepleai-api -n meepleai
```

### Automated Rollback (Phase 3)

**Canary Deployment with Argo Rollouts**:
```yaml
# k8s/rollouts/api-rollout.yaml
apiVersion: argoproj.io/v1alpha1
kind: Rollout
metadata:
  name: meepleai-api
spec:
  replicas: 10
  strategy:
    canary:
      steps:
      - setWeight: 10  # 10% traffic to new version
      - pause: {duration: 5m}
      - setWeight: 50
      - pause: {duration: 10m}
      - setWeight: 100
      analysis:
        templates:
        - templateName: error-rate
        args:
        - name: service-name
          value: meepleai-api
```

---

## Security Best Practices

1. **Secrets Management**: AWS Secrets Manager, never commit secrets
2. **Network Policies**: Restrict pod-to-pod communication
3. **RBAC**: Principle of least privilege for service accounts
4. **Image Scanning**: Trivy in CI/CD, block critical vulnerabilities
5. **TLS Everywhere**: mTLS for internal, HTTPS for external
6. **Audit Logging**: Enable Kubernetes audit logs, ship to S3

---

## Cost Optimization

**Phase 2 Estimated Monthly Costs (AWS)**:
- EKS cluster: $73 (control plane)
- EC2 nodes (3x t3.medium): ~$90
- RDS PostgreSQL (t3.medium Multi-AZ): ~$80
- ElastiCache Redis (3x cache.t3.medium): ~$120
- EBS volumes (500 GB gp3): ~$40
- Data transfer: ~$50
- **Total**: ~$450/month

**Optimization Strategies**:
- Reserved Instances (1-year): 30-40% savings on EC2/RDS
- Spot instances for non-critical workloads: 60-90% savings
- S3 Intelligent-Tiering for backups: Auto cost optimization
- Right-sizing: Monitor actual usage, downsize if over-provisioned

---

**Document Metadata**:
- **Version**: 1.0
- **Last Updated**: 2025-01-15
- **Next Review**: 2025-04-15
- **Maintainer**: DevOps Team
- **Status**: APPROVED for Phase 1-2

