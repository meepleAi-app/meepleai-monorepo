# Frontend Deployment Guide

**Status**: 🚨 **CRITICAL BLOCKER** - Not Documented
**Priority**: P0 (Required for production)
**Owner**: DevOps + Frontend Team
**Last Updated**: 2025-01-15

---

## Deployment Options

### Option 1: Vercel (Recommended)

**Pros**:
- Zero-config Next.js deployment
- Automatic CDN distribution (global edge network)
- Built-in Analytics & Web Vitals monitoring
- Automatic HTTPS & SSL certificates
- Preview deployments for PRs
- Serverless functions for API routes

**Cons**:
- Vendor lock-in
- Pricing scales with usage
- Limited backend integration control

**Setup**:
```bash
# Install Vercel CLI
pnpm add -D vercel

# Deploy
cd apps/web
vercel

# Production deployment
vercel --prod
```

**Environment Variables** (Vercel Dashboard):
```
NEXT_PUBLIC_API_BASE=https://api.meepleai.dev
NEXT_PUBLIC_ENVIRONMENT=production
```

---

### Option 2: Docker + Kubernetes (Self-Hosted)

**Pros**:
- Full control over infrastructure
- Multi-cloud portability
- Custom scaling rules
- No vendor lock-in

**Cons**:
- Higher operational overhead
- Requires DevOps expertise
- Manual CDN setup

**Dockerfile**:
```dockerfile
# apps/web/Dockerfile
FROM node:24-alpine AS builder

WORKDIR /app
COPY package.json pnpm-lock.yaml ./
RUN npm install -g pnpm && pnpm install --frozen-lockfile

COPY . .
RUN pnpm build

FROM node:24-alpine AS runner
WORKDIR /app

ENV NODE_ENV=production

COPY --from=builder /app/public ./public
COPY --from=builder /app/.next/standalone ./
COPY --from=builder /app/.next/static ./.next/static

EXPOSE 3000
CMD ["node", "server.js"]
```

**Kubernetes Deployment**:
```yaml
# k8s/frontend-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: meepleai-frontend
spec:
  replicas: 3
  selector:
    matchLabels:
      app: meepleai-frontend
  template:
    metadata:
      labels:
        app: meepleai-frontend
    spec:
      containers:
      - name: frontend
        image: meepleai/frontend:latest
        ports:
        - containerPort: 3000
        env:
        - name: NEXT_PUBLIC_API_BASE
          value: "https://api.meepleai.dev"
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```

---

## CDN Configuration

### Cloudflare (Recommended for Italian Users)

**Benefits**:
- Strong European presence (low latency for Italian users)
- DDoS protection
- Web Application Firewall (WAF)
- Analytics

**Setup**:
1. Point DNS to Cloudflare nameservers
2. Enable "Proxied" mode for `meepleai.dev`
3. Configure caching rules:
   - Static assets (`/_next/static/*`): 1 year cache
   - API routes (`/api/*`): No cache
   - Pages: 1 hour cache with stale-while-revalidate

---

## Environment Variables

### Required Variables

| Variable | Development | Production | Description |
|----------|-------------|------------|-------------|
| `NEXT_PUBLIC_API_BASE` | `http://localhost:8080` | `https://api.meepleai.dev` | Backend API URL |
| `NEXT_PUBLIC_ENVIRONMENT` | `development` | `production` | Environment name |
| `NEXT_PUBLIC_SENTRY_DSN` | (optional) | (required) | Error tracking |
| `NEXT_PUBLIC_ANALYTICS_ID` | (optional) | (required) | Vercel Analytics |

### Secret Management

**Vercel**: Environment variables in dashboard
**Kubernetes**: Kubernetes Secrets
**Docker**: `.env` file (never commit!)

---

## Security Headers

**next.config.js**:
```javascript
module.exports = {
  async headers() {
    return [
      {
        source: '/:path*',
        headers: [
          {
            key: 'X-DNS-Prefetch-Control',
            value: 'on'
          },
          {
            key: 'Strict-Transport-Security',
            value: 'max-age=63072000; includeSubDomains; preload'
          },
          {
            key: 'X-Frame-Options',
            value: 'DENY'
          },
          {
            key: 'X-Content-Type-Options',
            value: 'nosniff'
          },
          {
            key: 'X-XSS-Protection',
            value: '1; mode=block'
          },
          {
            key: 'Referrer-Policy',
            value: 'strict-origin-when-cross-origin'
          },
          {
            key: 'Content-Security-Policy',
            value: "default-src 'self'; script-src 'self' 'unsafe-inline' 'unsafe-eval'; style-src 'self' 'unsafe-inline'; img-src 'self' data: https:; font-src 'self' data:; connect-src 'self' https://api.meepleai.dev;"
          }
        ],
      },
    ];
  },
};
```

---

## CI/CD Pipeline

**GitHub Actions** (`.github/workflows/deploy-frontend.yml`):
```yaml
name: Deploy Frontend

on:
  push:
    branches: [main]
  pull_request:
    branches: [main]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-node@v3
        with:
          node-version: '24'

      - name: Install dependencies
        run: |
          cd apps/web
          pnpm install

      - name: Run tests
        run: |
          cd apps/web
          pnpm test
          pnpm lint
          pnpm typecheck

      - name: Build
        run: |
          cd apps/web
          pnpm build
        env:
          NEXT_PUBLIC_API_BASE: ${{ secrets.API_BASE_URL }}

      - name: Deploy to Vercel
        if: github.ref == 'refs/heads/main'
        run: |
          cd apps/web
          vercel --prod --token=${{ secrets.VERCEL_TOKEN }}
```

---

## Monitoring & Observability

### Error Tracking (Sentry)

```typescript
// src/app/providers.tsx (client entrypoint for App Router)
import * as Sentry from '@sentry/nextjs';

Sentry.init({
  dsn: process.env.NEXT_PUBLIC_SENTRY_DSN,
  environment: process.env.NEXT_PUBLIC_ENVIRONMENT,
  tracesSampleRate: 0.1,  // 10% performance monitoring
  beforeSend(event) {
    // Filter sensitive data
    if (event.request?.headers) {
      delete event.request.headers['Authorization'];
    }
    return event;
  },
});
```

### Performance Monitoring

**Vercel Analytics** (automatic):
- Core Web Vitals tracking
- Real User Monitoring
- Geographic distribution
- Device breakdown

**Custom RUM**:
```typescript
// lib/vitals.ts
export function reportWebVitals(metric: NextWebVitalsMetric) {
  // Send to backend analytics
  fetch('/api/analytics/vitals', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      name: metric.name,
      value: metric.value,
      id: metric.id,
      label: metric.label,
    }),
    keepalive: true,
  });
}
```

---

## Health Checks

**pages/api/health.ts**:
```typescript
import type { NextApiRequest, NextApiResponse } from 'next';

export default async function handler(req: NextApiRequest, res: NextApiResponse) {
  // Check API connectivity
  try {
    const apiCheck = await fetch(process.env.NEXT_PUBLIC_API_BASE + '/health');
    if (!apiCheck.ok) throw new Error('API unhealthy');

    res.status(200).json({
      status: 'healthy',
      timestamp: new Date().toISOString(),
      api: 'connected',
    });
  } catch (error) {
    res.status(503).json({
      status: 'unhealthy',
      timestamp: new Date().toISOString(),
      api: 'disconnected',
      error: error.message,
    });
  }
}
```

**Kubernetes Health Probe**:
```yaml
livenessProbe:
  httpGet:
    path: /api/health
    port: 3000
  initialDelaySeconds: 30
  periodSeconds: 10

readinessProbe:
  httpGet:
    path: /api/health
    port: 3000
  initialDelaySeconds: 5
  periodSeconds: 5
```

---

## Rollback Procedure

### Vercel

```bash
# List deployments
vercel ls

# Rollback to specific deployment
vercel rollback <deployment-url>
```

### Kubernetes

```bash
# Rollback to previous version
kubectl rollout undo deployment/meepleai-frontend

# Rollback to specific revision
kubectl rollout undo deployment/meepleai-frontend --to-revision=2
```

---

## Scaling Strategy

### Vercel
- Auto-scales based on traffic (serverless)
- No manual configuration required

### Kubernetes
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: meepleai-frontend-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: meepleai-frontend
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

---

## Acceptance Criteria

- [ ] Deployment pipeline configured
- [ ] Environment variables secured
- [ ] Security headers implemented
- [ ] CDN configured for Italian users
- [ ] Health checks operational
- [ ] Monitoring & alerting active
- [ ] Rollback procedure tested
- [ ] Documentation complete

---

**See Also**:
- [Disaster Recovery](./disaster-recovery.md)
- [Performance Requirements](../frontend/performance-requirements.md)

---

**Maintained by**: DevOps + Frontend Team
**Review Frequency**: Quarterly
