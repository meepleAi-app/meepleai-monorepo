# Deployment Guide - MeepleAI

**Audience**: DevOps engineers, SREs, and infrastructure managers.

## 📋 Table of Contents

1. [Deployment Overview](#deployment-overview)
2. [Prerequisites](#prerequisites)
3. [Environment Setup](#environment-setup)
4. [Docker Deployment](#docker-deployment)
5. [Kubernetes Deployment](#kubernetes-deployment)
6. [Database Setup](#database-setup)
7. [Secrets Management](#secrets-management)
8. [CI/CD Pipeline](#cicd-pipeline)
9. [Monitoring & Observability](#monitoring--observability)
10. [Scaling](#scaling)
11. [Backup & Recovery](#backup--recovery)
12. [Security](#security)
13. [Troubleshooting](#troubleshooting)

## 🎯 Deployment Overview

### Deployment Architecture

```
┌─────────────────────────────────────┐
│         Load Balancer               │
│     (Nginx/Cloudflare/AWS ALB)      │
└────────┬───────────────┬────────────┘
         │               │
    ┌────▼────┐     ┌────▼────┐
    │   Web   │     │   API   │
    │ (3000)  │     │ (8080)  │
    └─────────┘     └────┬────┘
                         │
    ┌────────────────────┴────────────────────┐
    │                                         │
┌───▼────┐  ┌───────┐  ┌──────┐  ┌─────────┐
│   PG   │  │ Qdrant│  │ Redis│  │   n8n   │
│  5432  │  │  6333 │  │ 6379 │  │  5678   │
└────────┘  └───────┘  └──────┘  └─────────┘
    │           │          │          │
┌───▼───────────▼──────────▼──────────▼───┐
│        Observability Stack               │
│  Seq + Jaeger + Prometheus + Grafana    │
└──────────────────────────────────────────┘
```

### Deployment Environments

| Environment | Purpose | URL | Uptime SLA |
|-------------|---------|-----|------------|
| **Development** | Local dev | localhost | N/A |
| **Staging** | Pre-prod testing | staging.meepleai.dev | 95% |
| **Production** | Live users | meepleai.dev | 99.5% |

### Deployment Methods

1. **Docker Compose** - Local development, small deployments
2. **Kubernetes** - Production, high availability
3. **Managed Services** - AWS/Azure/GCP (recommended for production)

## ✅ Prerequisites

### Infrastructure Requirements

**Compute**:
- API: 2 vCPU, 4GB RAM (minimum per instance)
- Web: 1 vCPU, 2GB RAM (minimum per instance)
- PostgreSQL: 2 vCPU, 8GB RAM
- Qdrant: 2 vCPU, 4GB RAM
- Redis: 1 vCPU, 2GB RAM

**Storage**:
- PostgreSQL: 50GB SSD (minimum)
- Qdrant: 100GB SSD (for vectors)
- Redis: 10GB (ephemeral)
- Application logs: 20GB

**Network**:
- Bandwidth: 100Mbps minimum
- Latency: <50ms between services
- TLS/SSL certificates

### Required Tools

- **Docker**: 24.0+
- **Docker Compose**: 2.20+
- **Kubernetes**: 1.28+ (optional)
- **kubectl**: Latest
- **Helm**: 3.12+ (optional)

### External Services

- **OpenRouter**: API key for LLM access
- **SMTP**: Email service (SendGrid, SES, etc.)
- **Object Storage**: S3/Azure Blob/GCS for PDFs
- **DNS**: Domain management
- **SSL**: Certificate provider (Let's Encrypt, etc.)

## 🔧 Environment Setup

### Environment Variables

Create environment files for each environment:

**Development** (`infra/env/.env.dev`):
```env
# Application
ASPNETCORE_ENVIRONMENT=Development
ASPNETCORE_URLS=http://+:8080

# Database
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=meepleai_dev;Username=postgres;Password=postgres

# External Services
QDRANT_URL=http://localhost:6333
REDIS_URL=localhost:6379
SEQ_URL=http://localhost:8081
JAEGER_URL=http://localhost:16686

# AI Services
OPENROUTER_API_KEY=sk-or-v1-your-dev-key
EMBEDDING_SERVICE_URL=http://localhost:8000
UNSTRUCTURED_URL=http://localhost:8001
SMOLDOCLING_URL=http://localhost:8002

# Feature Flags
FEATURES__ENABLE_2FA=true
FEATURES__ENABLE_OAUTH=true
FEATURES__ENABLE_PDF_UPLOAD=true

# Initial Admin
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD=Admin123!

# Logging
SERILOG__MINIMUMLEVEL=Debug
```

**Production** (`infra/env/.env.prod`):
```env
# Application
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080

# Database (use managed service)
ConnectionStrings__Postgres=Host=prod-db.region.rds.amazonaws.com;Port=5432;Database=meepleai;Username=meepleai;Password=${DB_PASSWORD}

# External Services (managed)
QDRANT_URL=https://qdrant.prod.internal:6333
REDIS_URL=redis.prod.internal:6379
SEQ_URL=https://seq.prod.internal

# AI Services
OPENROUTER_API_KEY=${OPENROUTER_API_KEY}
EMBEDDING_SERVICE_URL=https://embedding.prod.internal:8000

# Security
USE_HTTPS_REDIRECTION=true
REQUIRE_HTTPS=true
HSTS_MAX_AGE=31536000

# Performance
HYBRID_CACHE__DEFAULT_EXPIRATION_MINUTES=5
CONNECTION_POOL__MIN=10
CONNECTION_POOL__MAX=100

# Observability
SERILOG__MINIMUMLEVEL=Information
OPENTELEMETRY__ENABLED=true

# Rate Limiting
RATE_LIMIT__REQUESTS_PER_MINUTE=60
RATE_LIMIT__REQUESTS_PER_HOUR=1000
```

**Frontend** (`apps/web/.env.production`):
```env
NEXT_PUBLIC_API_BASE=https://api.meepleai.dev
NEXT_TELEMETRY_DISABLED=1
```

### Secrets Management

**Never commit secrets to git!**

**Development**: Use `.env.local` files (gitignored)

**Production Options**:
1. **AWS Secrets Manager**
2. **Azure Key Vault**
3. **HashiCorp Vault**
4. **Kubernetes Secrets**
5. **Docker Secrets**

**Example with AWS Secrets Manager**:
```bash
# Store secret
aws secretsmanager create-secret \
  --name meepleai/prod/openrouter-api-key \
  --secret-string "sk-or-v1-..."

# Retrieve in startup script
export OPENROUTER_API_KEY=$(aws secretsmanager get-secret-value \
  --secret-id meepleai/prod/openrouter-api-key \
  --query SecretString --output text)
```

## 🐳 Docker Deployment

### Using Docker Compose

**1. Clone Repository**:
```bash
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo
```

**2. Configure Environment**:
```bash
cp infra/env/.env.example infra/env/.env.prod
nano infra/env/.env.prod  # Edit with production values
```

**3. Build Images**:
```bash
# Backend
cd apps/api
docker build -t meepleai/api:latest .

# Frontend
cd apps/web
docker build -t meepleai/web:latest .
```

**4. Start Services**:
```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d
```

**5. Verify Deployment**:
```bash
# Check services
docker compose ps

# Check API health
curl http://localhost:5080/health

# Check logs
docker compose logs -f api
```

### Production Docker Compose

**`docker-compose.prod.yml`**:
```yaml
version: '3.8'

services:
  api:
    image: meepleai/api:${VERSION:-latest}
    restart: always
    env_file:
      - ./env/.env.prod
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
    ports:
      - "8080:8080"
    depends_on:
      - postgres
      - qdrant
      - redis
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5080/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s
    deploy:
      replicas: 2
      resources:
        limits:
          cpus: '2'
          memory: 4G
        reservations:
          cpus: '1'
          memory: 2G

  web:
    image: meepleai/web:${VERSION:-latest}
    restart: always
    environment:
      - NEXT_PUBLIC_API_BASE=https://api.meepleai.dev
    ports:
      - "3000:3000"
    depends_on:
      - api
    deploy:
      replicas: 2
      resources:
        limits:
          cpus: '1'
          memory: 2G

  postgres:
    image: postgres:17
    restart: always
    environment:
      - POSTGRES_DB=meepleai
      - POSTGRES_USER=${DB_USER}
      - POSTGRES_PASSWORD=${DB_PASSWORD}
    volumes:
      - postgres_data:/var/lib/postgresql/data
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 8G
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U ${DB_USER}"]
      interval: 10s
      timeout: 5s
      retries: 5

  qdrant:
    image: qdrant/qdrant:latest
    restart: always
    volumes:
      - qdrant_data:/qdrant/storage
    deploy:
      resources:
        limits:
          cpus: '2'
          memory: 4G

  redis:
    image: redis:7-alpine
    restart: always
    command: redis-server --maxmemory 2gb --maxmemory-policy allkeys-lru
    volumes:
      - redis_data:/data
    deploy:
      resources:
        limits:
          cpus: '1'
          memory: 2G

  nginx:
    image: nginx:alpine
    restart: always
    ports:
      - "80:80"
      - "443:443"
    volumes:
      - ./nginx.conf:/etc/nginx/nginx.conf:ro
      - ./ssl:/etc/nginx/ssl:ro
    depends_on:
      - api
      - web

volumes:
  postgres_data:
  qdrant_data:
  redis_data:
```

### Nginx Configuration

**`infra/nginx.conf`**:
```nginx
upstream api {
    server api:8080;
}

upstream web {
    server web:3000;
}

server {
    listen 80;
    server_name meepleai.dev;
    return 301 https://$server_name$request_uri;
}

server {
    listen 443 ssl http2;
    server_name meepleai.dev;

    ssl_certificate /etc/nginx/ssl/fullchain.pem;
    ssl_certificate_key /etc/nginx/ssl/privkey.pem;

    # Security headers
    add_header Strict-Transport-Security "max-age=31536000; includeSubDomains" always;
    add_header X-Content-Type-Options "nosniff" always;
    add_header X-Frame-Options "DENY" always;
    add_header X-XSS-Protection "1; mode=block" always;

    # Frontend
    location / {
        proxy_pass http://web;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
    }

    # API
    location /api/ {
        proxy_pass http://api;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # CORS
        add_header Access-Control-Allow-Origin https://meepleai.dev;
        add_header Access-Control-Allow-Credentials true;
    }

    # Health check
    location /health {
        proxy_pass http://api/health;
        access_log off;
    }
}
```

## ☸️ Kubernetes Deployment

### Kubernetes Architecture

```
┌─────────────────────────────────────┐
│         Ingress (Nginx)             │
└────────┬───────────────┬────────────┘
         │               │
    ┌────▼────┐     ┌────▼────┐
    │   Web   │     │   API   │
    │  (Svc)  │     │  (Svc)  │
    └────┬────┘     └────┬────┘
         │               │
    ┌────▼────┐     ┌────▼────┐
    │   Web   │     │   API   │
    │  Pods   │     │  Pods   │
    │  (x2)   │     │  (x3)   │
    └─────────┘     └────┬────┘
                         │
    ┌────────────────────┴────────────┐
    │                                 │
┌───▼────┐  ┌───────┐  ┌──────┐     │
│   PG   │  │ Qdrant│  │ Redis│     │
│ StatefulSet │ StatefulSet │ StatefulSet │
└────────┘  └───────┘  └──────┘
```

### Namespace

**`k8s/namespace.yaml`**:
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: meepleai
```

### ConfigMap

**`k8s/configmap.yaml`**:
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: meepleai-config
  namespace: meepleai
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  QDRANT_URL: "http://qdrant:6333"
  REDIS_URL: "redis:6379"
  SEQ_URL: "http://seq:8081"
  FEATURES__ENABLE_2FA: "true"
  FEATURES__ENABLE_OAUTH: "true"
```

### Secrets

**`k8s/secrets.yaml`** (create from template, don't commit actual secrets):
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: meepleai-secrets
  namespace: meepleai
type: Opaque
data:
  # Base64 encoded values
  db-password: <base64-encoded-password>
  openrouter-api-key: <base64-encoded-key>
  jwt-secret: <base64-encoded-secret>
```

**Create from command**:
```bash
kubectl create secret generic meepleai-secrets \
  --from-literal=db-password='your-password' \
  --from-literal=openrouter-api-key='your-key' \
  -n meepleai
```

### API Deployment

**`k8s/api-deployment.yaml`**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: api
  namespace: meepleai
spec:
  replicas: 3
  selector:
    matchLabels:
      app: api
  template:
    metadata:
      labels:
        app: api
    spec:
      containers:
      - name: api
        image: meepleai/api:latest
        ports:
        - containerPort: 8080
        envFrom:
        - configMapRef:
            name: meepleai-config
        env:
        - name: ConnectionStrings__Postgres
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: connection-string
        - name: OPENROUTER_API_KEY
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: openrouter-api-key
        resources:
          requests:
            memory: "2Gi"
            cpu: "1"
          limits:
            memory: "4Gi"
            cpu: "2"
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 10
          periodSeconds: 5
---
apiVersion: v1
kind: Service
metadata:
  name: api
  namespace: meepleai
spec:
  selector:
    app: api
  ports:
  - port: 8080
    targetPort: 8080
  type: ClusterIP
```

### PostgreSQL StatefulSet

**`k8s/postgres-statefulset.yaml`**:
```yaml
apiVersion: apps/v1
kind: StatefulSet
metadata:
  name: postgres
  namespace: meepleai
spec:
  serviceName: postgres
  replicas: 1
  selector:
    matchLabels:
      app: postgres
  template:
    metadata:
      labels:
        app: postgres
    spec:
      containers:
      - name: postgres
        image: postgres:17
        ports:
        - containerPort: 5432
        env:
        - name: POSTGRES_DB
          value: meepleai
        - name: POSTGRES_USER
          value: meepleai
        - name: POSTGRES_PASSWORD
          valueFrom:
            secretKeyRef:
              name: meepleai-secrets
              key: db-password
        volumeMounts:
        - name: postgres-storage
          mountPath: /var/lib/postgresql/data
        resources:
          requests:
            memory: "4Gi"
            cpu: "2"
          limits:
            memory: "8Gi"
            cpu: "2"
  volumeClaimTemplates:
  - metadata:
      name: postgres-storage
    spec:
      accessModes: [ "ReadWriteOnce" ]
      resources:
        requests:
          storage: 50Gi
---
apiVersion: v1
kind: Service
metadata:
  name: postgres
  namespace: meepleai
spec:
  selector:
    app: postgres
  ports:
  - port: 5432
  clusterIP: None
```

### Ingress

**`k8s/ingress.yaml`**:
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: meepleai-ingress
  namespace: meepleai
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
    nginx.ingress.kubernetes.io/ssl-redirect: "true"
spec:
  tls:
  - hosts:
    - meepleai.dev
    - api.meepleai.dev
    secretName: meepleai-tls
  rules:
  - host: meepleai.dev
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: web
            port:
              number: 3000
  - host: api.meepleai.dev
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: api
            port:
              number: 8080
```

### Deploy to Kubernetes

```bash
# Create namespace
kubectl apply -f k8s/namespace.yaml

# Create ConfigMap
kubectl apply -f k8s/configmap.yaml

# Create Secrets
kubectl apply -f k8s/secrets.yaml

# Deploy services
kubectl apply -f k8s/postgres-statefulset.yaml
kubectl apply -f k8s/qdrant-statefulset.yaml
kubectl apply -f k8s/redis-deployment.yaml

# Deploy application
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/web-deployment.yaml

# Create Ingress
kubectl apply -f k8s/ingress.yaml

# Verify deployment
kubectl get all -n meepleai
kubectl get pods -n meepleai
kubectl logs -f deployment/api -n meepleai
```

### Helm Chart (Optional)

Create a Helm chart for easier deployment:

```bash
helm create meepleai
cd meepleai

# Edit values.yaml
# Edit templates/

# Install
helm install meepleai . -n meepleai --create-namespace

# Upgrade
helm upgrade meepleai . -n meepleai
```

## 🗄️ Database Setup

### PostgreSQL Initialization

**Migrations**: Auto-applied on API startup

**Manual Setup** (if needed):
```bash
# Connect to database
psql -h localhost -U postgres -d meepleai

# Create database
CREATE DATABASE meepleai;

# Create user
CREATE USER meepleai WITH PASSWORD 'secure-password';

# Grant permissions
GRANT ALL PRIVILEGES ON DATABASE meepleai TO meepleai;

# Enable extensions
\c meepleai
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pg_trgm";  -- For full-text search
```

### Database Backups

**Automated Backups**:
```bash
#!/bin/bash
# backup.sh

BACKUP_DIR="/backups/postgres"
DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="$BACKUP_DIR/meepleai_$DATE.sql.gz"

# Create backup
pg_dump -h localhost -U meepleai meepleai | gzip > $BACKUP_FILE

# Upload to S3
aws s3 cp $BACKUP_FILE s3://meepleai-backups/postgres/

# Retain only last 30 days
find $BACKUP_DIR -name "*.sql.gz" -mtime +30 -delete
```

**Schedule with Cron**:
```cron
# Daily backup at 2 AM
0 2 * * * /opt/meepleai/backup.sh
```

### Database Restore

```bash
# Download backup
aws s3 cp s3://meepleai-backups/postgres/meepleai_20251115_020000.sql.gz .

# Restore
gunzip < meepleai_20251115_020000.sql.gz | psql -h localhost -U meepleai meepleai
```

## 🔒 Secrets Management

See [Environment Setup](#environment-setup) section above.

## 🚀 CI/CD Pipeline

### GitHub Actions Workflow

**`.github/workflows/deploy.yml`**:
```yaml
name: Deploy

on:
  push:
    branches:
      - main
    tags:
      - 'v*'

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Set up Docker Buildx
        uses: docker/setup-buildx-action@v3

      - name: Login to Docker Hub
        uses: docker/login-action@v3
        with:
          username: ${{ secrets.DOCKER_USERNAME }}
          password: ${{ secrets.DOCKER_PASSWORD }}

      - name: Build and push API
        uses: docker/build-push-action@v5
        with:
          context: ./apps/api
          push: true
          tags: meepleai/api:${{ github.sha }},meepleai/api:latest

      - name: Build and push Web
        uses: docker/build-push-action@v5
        with:
          context: ./apps/web
          push: true
          tags: meepleai/web:${{ github.sha }},meepleai/web:latest

  deploy:
    needs: build
    runs-on: ubuntu-latest
    if: github.ref == 'refs/heads/main'
    steps:
      - name: Deploy to production
        uses: appleboy/ssh-action@master
        with:
          host: ${{ secrets.PROD_HOST }}
          username: ${{ secrets.PROD_USER }}
          key: ${{ secrets.PROD_SSH_KEY }}
          script: |
            cd /opt/meepleai
            docker compose pull
            docker compose up -d
            docker system prune -af
```

### Blue-Green Deployment

```bash
# Deploy to "blue" environment
kubectl set image deployment/api api=meepleai/api:v2 -n meepleai-blue

# Verify
kubectl get pods -n meepleai-blue

# Switch traffic
kubectl patch service api -n meepleai -p '{"spec":{"selector":{"version":"blue"}}}'

# Monitor
kubectl logs -f deployment/api -n meepleai-blue

# Rollback if needed
kubectl patch service api -n meepleai -p '{"spec":{"selector":{"version":"green"}}}'
```

## 📊 Monitoring & Observability

### Health Checks

**API Health Endpoint**: `/health`

```bash
curl http://localhost:5080/health
```

**Response**:
```json
{
  "status": "Healthy",
  "checks": {
    "postgres": "Healthy",
    "redis": "Healthy",
    "qdrant": "Healthy"
  },
  "duration": "00:00:00.123"
}
```

### Logging (Seq)

**Access**: `http://seq.meepleai.dev` or `http://localhost:8081`

**Query Examples**:
```sql
-- All errors
@Level = 'Error'

-- API errors
@Level = 'Error' AND Application = 'Api'

-- User actions
@MessageTemplate LIKE '%user%'

-- Slow requests
Duration > 2000
```

### Tracing (Jaeger)

**Access**: `http://jaeger.meepleai.dev` or `http://localhost:16686`

**Trace Search**:
- Service: `meepleai-api`
- Operation: `POST /api/v1/chat`
- Tags: `http.status_code=500`

### Metrics (Prometheus + Grafana)

**Prometheus**: `http://prometheus.meepleai.dev` or `http://localhost:9090`

**Grafana**: `http://grafana.meepleai.dev` or `http://localhost:3001`

**Key Metrics**:
- Request rate: `rate(http_requests_total[5m])`
- Error rate: `rate(http_requests_total{status=~"5.."}[5m])`
- Response time: `histogram_quantile(0.95, http_request_duration_seconds_bucket)`
- Database connections: `pg_stat_database_numbackends`

### Alerts (AlertManager)

**Access**: `http://alertmanager.meepleai.dev` or `http://localhost:9093`

**Alert Rules** (`prometheus/alerts.yml`):
```yaml
groups:
  - name: meepleai
    rules:
      - alert: HighErrorRate
        expr: rate(http_requests_total{status=~"5.."}[5m]) > 0.05
        for: 5m
        labels:
          severity: critical
        annotations:
          summary: "High error rate detected"

      - alert: SlowResponses
        expr: histogram_quantile(0.95, http_request_duration_seconds_bucket) > 2
        for: 5m
        labels:
          severity: warning
        annotations:
          summary: "P95 response time > 2s"

      - alert: DatabaseDown
        expr: up{job="postgres"} == 0
        for: 1m
        labels:
          severity: critical
        annotations:
          summary: "PostgreSQL is down"
```

## 📈 Scaling

### Horizontal Scaling

**Docker Compose**:
```bash
docker compose up -d --scale api=3 --scale web=2
```

**Kubernetes**:
```bash
# Manual scaling
kubectl scale deployment/api --replicas=5 -n meepleai

# Autoscaling
kubectl autoscale deployment/api --cpu-percent=70 --min=2 --max=10 -n meepleai
```

**HPA (Horizontal Pod Autoscaler)**:
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: api-hpa
  namespace: meepleai
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: api
  minReplicas: 2
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
```

### Vertical Scaling

**Increase Resources**:
```yaml
resources:
  requests:
    memory: "4Gi"
    cpu: "2"
  limits:
    memory: "8Gi"
    cpu: "4"
```

### Database Scaling

**Read Replicas**:
```bash
# PostgreSQL replication
# Setup primary-replica replication
# Route read queries to replicas
```

**Connection Pooling**:
```env
CONNECTION_POOL__MIN=10
CONNECTION_POOL__MAX=100
```

## 💾 Backup & Recovery

### Backup Strategy

| Component | Frequency | Retention | Method |
|-----------|-----------|-----------|--------|
| **PostgreSQL** | Daily | 30 days | pg_dump + S3 |
| **Qdrant** | Daily | 7 days | Snapshot + S3 |
| **Redis** | None | N/A | Ephemeral cache |
| **Application Files** | Weekly | 90 days | S3 sync |

### PostgreSQL Backup

See [Database Backups](#database-backups) section above.

### Qdrant Backup

```bash
# Create snapshot
curl -X POST http://localhost:6333/collections/rules/snapshots

# Download snapshot
curl -X GET http://localhost:6333/collections/rules/snapshots/snapshot-name \
  -o snapshot.tar

# Upload to S3
aws s3 cp snapshot.tar s3://meepleai-backups/qdrant/
```

### Disaster Recovery

**RTO (Recovery Time Objective)**: 1 hour
**RPO (Recovery Point Objective)**: 24 hours

**Recovery Procedure**:
1. Provision new infrastructure
2. Restore PostgreSQL from latest backup
3. Restore Qdrant snapshots
4. Deploy latest application version
5. Verify health checks
6. Update DNS (if needed)

## 🔐 Security

### TLS/SSL

**Certificates**: Use Let's Encrypt or managed certificates

**Auto-renewal with cert-manager (Kubernetes)**:
```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: admin@meepleai.dev
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
```

### Firewall Rules

**Allow**:
- 80/443 (HTTP/HTTPS) from internet
- 8080 (API) from web tier only
- 5432 (PostgreSQL) from API tier only
- 6333 (Qdrant) from API tier only
- 6379 (Redis) from API tier only

**Deny**: All other traffic

### Security Headers

See [Nginx Configuration](#nginx-configuration) for security headers.

### Vulnerability Scanning

```bash
# Docker image scanning
docker scan meepleai/api:latest

# Dependency scanning
dotnet list package --vulnerable
pnpm audit --audit-level=high
```

## 🐛 Troubleshooting

### Common Issues

**"Service won't start"**:
```bash
# Check logs
docker compose logs api
kubectl logs deployment/api -n meepleai

# Check health
curl http://localhost:5080/health
```

**"Database connection failed"**:
```bash
# Verify database is running
docker ps | grep postgres
kubectl get pods -n meepleai | grep postgres

# Test connection
psql -h localhost -U meepleai -d meepleai
```

**"High memory usage"**:
```bash
# Check resource usage
docker stats
kubectl top pods -n meepleai

# Restart services
docker compose restart api
kubectl rollout restart deployment/api -n meepleai
```

**"Slow responses"**:
- Check Grafana dashboards
- Review Seq logs for slow queries
- Check Jaeger traces
- Verify Redis cache hit rate

### Rollback Procedure

**Docker Compose**:
```bash
# Pull previous version
docker compose pull meepleai/api:v1.0.0

# Restart with previous version
VERSION=v1.0.0 docker compose up -d
```

**Kubernetes**:
```bash
# Rollback deployment
kubectl rollout undo deployment/api -n meepleai

# Rollback to specific revision
kubectl rollout undo deployment/api --to-revision=2 -n meepleai

# Check rollout status
kubectl rollout status deployment/api -n meepleai
```

## 📚 Additional Resources

- **[Administrator Guide](./05-administrator-guide.md)** - System maintenance
- **[Architecture Guide](./06-architecture-guide.md)** - Technical architecture
- **[Main Documentation](../docs/INDEX.md)** - Complete documentation
- **Docker Docs**: https://docs.docker.com/
- **Kubernetes Docs**: https://kubernetes.io/docs/
- **Helm Docs**: https://helm.sh/docs/

---

**Version**: 1.0-rc
**Last Updated**: 2025-11-15
**For**: DevOps & SRE
