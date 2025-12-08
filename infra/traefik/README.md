# Traefik v3.2 Reverse Proxy

Traefik provides automatic service discovery, routing, and load balancing for Docker containers.

## Quick Start (Development)

```bash
# Start Traefik with minimal services
cd infra
docker compose -f docker-compose.yml -f docker-compose.traefik.yml up -d

# Access dashboard
open http://traefik.localhost:8080
```

## Features

### Current (Development Mode)
- ✅ Automatic Docker service discovery
- ✅ HTTP routing with labels
- ✅ Dashboard at `http://traefik.localhost:8080`
- ✅ Rate limiting middlewares
- ✅ Security headers
- ✅ Prometheus metrics
- ✅ Access logs

### Production-Ready (Commented, see upgrade guide)
- ⏳ Docker socket proxy (security)
- ⏳ Let's Encrypt automatic SSL
- ⏳ HTTPS redirect
- ⏳ Dashboard authentication
- ⏳ IP whitelisting

## Configuration Files

```
traefik/
├── traefik.yml              # Static configuration (requires restart)
├── dynamic/
│   ├── middlewares.yml      # Rate limit, headers, auth, CORS
│   └── tls.yml              # TLS/SSL options
├── logs/                    # Access and application logs
└── README.md                # This file
```

## Adding Services to Traefik

### Example: Expose API service

```yaml
# In docker-compose.yml
api:
  # ... existing config ...
  labels:
    - "traefik.enable=true"
    - "traefik.http.routers.api.rule=Host(`api.localhost`)"
    - "traefik.http.routers.api.entrypoints=web"
    - "traefik.http.services.api.loadbalancer.server.port=8080"

    # Optional: Add middlewares
    - "traefik.http.routers.api.middlewares=rate-limit-api@file,security-headers@file"
```

### Common Label Patterns

**Basic HTTP routing:**
```yaml
- "traefik.enable=true"
- "traefik.http.routers.<service-name>.rule=Host(`<subdomain>.localhost`)"
- "traefik.http.routers.<service-name>.entrypoints=web"
```

**With middlewares:**
```yaml
- "traefik.http.routers.<service-name>.middlewares=rate-limit-standard@file,security-headers@file"
```

**Custom port (if not default 80):**
```yaml
- "traefik.http.services.<service-name>.loadbalancer.server.port=<port>"
```

**Multiple domains:**
```yaml
- "traefik.http.routers.<service-name>.rule=Host(`domain1.localhost`) || Host(`domain2.localhost`)"
```

**Path-based routing:**
```yaml
- "traefik.http.routers.<service-name>.rule=Host(`localhost`) && PathPrefix(`/api`)"
```

## Available Middlewares (from middlewares.yml)

Apply with: `traefik.http.routers.<name>.middlewares=<middleware>@file`

- `rate-limit-standard`: 100 req/s, burst 50
- `rate-limit-strict`: 10 req/s, burst 5
- `rate-limit-api`: 300 req/min, burst 100
- `security-headers`: OWASP recommended headers
- `cors-api`: CORS for API services
- `compress`: Gzip/Brotli compression
- `retry`: Retry failed requests (3 attempts)
- `circuit-breaker`: Prevent cascading failures

## Production Upgrade Path

### Step 1: Docker Socket Proxy (Security)

Uncomment in `docker-compose.traefik.yml`:

```yaml
docker-socket-proxy:
  # ... entire service ...
```

Update Traefik:
```yaml
# Remove direct socket mount
# volumes:
#   - /var/run/docker.sock:/var/run/docker.sock:ro

# Update static config
command:
  - "--providers.docker.endpoint=tcp://docker-socket-proxy:2375"
```

### Step 2: Let's Encrypt SSL

Choose challenge type:

**Option A: HTTP Challenge** (simpler, requires port 80 public)
```yaml
# In docker-compose.traefik.yml
command:
  - "--certificatesresolvers.letsencrypt.acme.httpchallenge=true"
  - "--certificatesresolvers.letsencrypt.acme.httpchallenge.entrypoint=web"
  - "--certificatesresolvers.letsencrypt.acme.email=your-email@example.com"
  - "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json"

  # Start with staging to avoid rate limits
  - "--certificatesresolvers.letsencrypt.acme.caserver=https://acme-staging-v02.api.letsencrypt.org/directory"

ports:
  - "443:443"

volumes:
  - ./traefik/letsencrypt:/letsencrypt
```

**Option B: DNS Challenge** (wildcard certs, requires Cloudflare)
```yaml
command:
  - "--certificatesresolvers.letsencrypt.acme.dnschallenge=true"
  - "--certificatesresolvers.letsencrypt.acme.dnschallenge.provider=cloudflare"
  - "--certificatesresolvers.letsencrypt.acme.email=your-email@example.com"
  - "--certificatesresolvers.letsencrypt.acme.storage=/letsencrypt/acme.json"

environment:
  CF_DNS_API_TOKEN_FILE: /run/secrets/cloudflare-api-token

secrets:
  - cloudflare-api-token
```

Create acme.json:
```bash
touch letsencrypt/acme.json
chmod 600 letsencrypt/acme.json
```

### Step 3: Update Service Labels for HTTPS

```yaml
labels:
  # HTTPS router
  - "traefik.http.routers.<service>-secure.rule=Host(`<domain>.com`)"
  - "traefik.http.routers.<service>-secure.entrypoints=websecure"
  - "traefik.http.routers.<service>-secure.tls.certresolver=letsencrypt"

  # HTTP to HTTPS redirect (optional, can be global)
  - "traefik.http.routers.<service>.rule=Host(`<domain>.com`)"
  - "traefik.http.routers.<service>.entrypoints=web"
  - "traefik.http.routers.<service>.middlewares=redirect-to-https@file"
```

### Step 4: Dashboard Authentication

Generate password:
```bash
# Install htpasswd (Debian/Ubuntu)
sudo apt-get install apache2-utils

# Generate hash (replace 'admin' and 'your-password')
echo $(htpasswd -nbB admin "your-password") | sed -e s/\\$/\\$\\$/g
```

Add label:
```yaml
- "traefik.http.routers.traefik-dashboard.middlewares=dashboard-auth"
- "traefik.http.middlewares.dashboard-auth.basicauth.users=admin:$$2y$$05$$..."
```

Remove insecure flag:
```yaml
command:
  # - "--api.insecure=true"  # REMOVE THIS
```

### Step 5: IP Whitelisting (Optional)

For sensitive endpoints (admin, metrics):
```yaml
# In middlewares.yml
ip-whitelist-admin:
  ipAllowList:
    sourceRange:
      - "YOUR_IP/32"
      - "YOUR_OFFICE_IP/32"

# In service labels
- "traefik.http.routers.<service>.middlewares=ip-whitelist-admin@file"
```

## Troubleshooting

### Dashboard not accessible
```bash
# Check Traefik is running
docker compose ps traefik

# Check logs
docker compose logs traefik

# Verify localhost routing
curl -H "Host: traefik.localhost" http://localhost:8080
```

### Service not routing
```bash
# Check service has labels
docker compose config | grep -A 10 "labels:"

# Check Traefik discovered it
curl http://traefik.localhost:8080/api/http/routers

# Check service logs
docker compose logs <service-name>
```

### Let's Encrypt errors
```bash
# Common issues:
# 1. Port 80/443 not publicly accessible
# 2. DNS not pointing to server
# 3. Rate limit hit (use staging first)

# Check certificate status
curl http://traefik.localhost:8080/api/http/routers/<router-name>

# Verify DNS
dig +short yourdomain.com

# Test with staging first
# Remove staging flag only after successful test
```

### High CPU/Memory usage
```bash
# Check logs volume
du -sh traefik/logs/

# Disable access logs if not needed
# accesslog=false in traefik.yml

# Reduce log level
# log.level=WARN

# Check for routing loops
docker compose logs traefik | grep -i "loop"
```

## Security Best Practices

1. ✅ Use Docker socket proxy (production)
2. ✅ Enable HTTPS with Let's Encrypt
3. ✅ Set `api.insecure=false`
4. ✅ Use basic auth or OAuth for dashboard
5. ✅ Apply rate limiting to all public services
6. ✅ Use security headers middleware
7. ✅ Whitelist admin endpoints by IP
8. ✅ Keep Traefik updated
9. ✅ Monitor access logs for suspicious activity
10. ✅ Use secrets for sensitive data

## Monitoring

### Prometheus Metrics
Available at: `http://traefik.localhost:8080/metrics`

Add to Prometheus:
```yaml
# prometheus.yml
scrape_configs:
  - job_name: 'traefik'
    static_configs:
      - targets: ['traefik:8080']
```

### Key Metrics
- `traefik_entrypoint_requests_total`: Total requests per entrypoint
- `traefik_entrypoint_request_duration_seconds`: Request duration
- `traefik_backend_requests_total`: Backend requests
- `traefik_backend_request_duration_seconds`: Backend latency

### Grafana Dashboard
Import dashboard: https://grafana.com/grafana/dashboards/17346

## References

- [Official Traefik v3 Docs](https://doc.traefik.io/traefik/)
- [Docker Provider](https://doc.traefik.io/traefik/providers/docker/)
- [Let's Encrypt](https://doc.traefik.io/traefik/https/acme/)
- [Middlewares](https://doc.traefik.io/traefik/middlewares/overview/)
- [MeepleAI Infrastructure Docs](../docs/05-operations/)

## Support

For issues or questions:
1. Check logs: `docker compose logs traefik`
2. Verify configuration: `docker compose config`
3. Consult docs: https://doc.traefik.io/traefik/
4. Open issue: https://github.com/your-org/meepleai-monorepo/issues
