# Traefik Testing Guide

Manual validation checklist for Traefik v3.2 setup.

## Prerequisites

✅ Docker Desktop running
✅ Ports 80, 8080 available
✅ Network `meepleai` exists (created by main docker-compose.yml)

## Test 1: Start Traefik

```bash
cd infra
docker compose -f docker-compose.yml -f docker-compose.traefik.yml up -d traefik
```

**Expected output:**
```
[+] Running 1/1
 ✔ Container meepleai-traefik  Started
```

**Verify:**
```bash
docker compose ps traefik
```

Should show:
- NAME: `meepleai-traefik`
- STATUS: `Up` (healthy)
- PORTS: `0.0.0.0:80->80/tcp`, `0.0.0.0:8080->8080/tcp`

## Test 2: Dashboard Access

**Browser test:**
```
http://traefik.localhost:8080
```

**Curl test:**
```bash
curl -H "Host: traefik.localhost" http://localhost:8080/dashboard/
```

**Expected:**
- ✅ Dashboard loads without errors
- ✅ Shows "HTTP Routers" section
- ✅ Shows "HTTP Services" section
- ✅ Shows "HTTP Middlewares" section with file provider entries

**Screenshots to capture:**
1. Dashboard overview
2. HTTP Routers tab
3. HTTP Services tab

## Test 3: API Endpoints

```bash
# List all routers
curl http://traefik.localhost:8080/api/http/routers | jq

# List all services
curl http://traefik.localhost:8080/api/http/services | jq

# List all middlewares
curl http://traefik.localhost:8080/api/http/middlewares | jq

# Health check
curl http://traefik.localhost:8080/ping
```

**Expected:**
- ✅ Routers: Should show `traefik-dashboard@docker`
- ✅ Services: Should show `api@internal`
- ✅ Middlewares: Should show all from `dynamic/middlewares.yml` with `@file` provider
- ✅ Ping: Returns `OK`

## Test 4: Dynamic Configuration Reload

```bash
# Edit a middleware file
echo "# Test change" >> traefik/dynamic/middlewares.yml

# Wait 5 seconds for Traefik to detect change
sleep 5

# Check logs
docker compose logs traefik | tail -20
```

**Expected in logs:**
```
Configuration loaded from file: /etc/traefik/dynamic/middlewares.yml
```

## Test 5: Service Routing (with examples)

**Start a test service:**
```bash
docker compose -f docker-compose.yml -f docker-compose.traefik.yml -f docker-compose.traefik-examples.yml up -d web
```

**Test routing:**
```bash
# Via hostname
curl -H "Host: app.localhost" http://localhost/

# Direct localhost (should also work based on routing rule)
curl http://app.localhost/
```

**Expected:**
- ✅ Returns Next.js app HTML
- ✅ No 404 errors
- ✅ Response time < 100ms

**Verify in dashboard:**
1. Open http://traefik.localhost:8080
2. Go to HTTP Routers tab
3. Look for `web@docker` router
4. Should show:
   - Rule: `Host(`app.localhost`) || Host(`localhost`)`
   - Service: `web`
   - Entrypoints: `web`
   - Middlewares: rate-limit, compress

## Test 6: Middlewares Application

**Test rate limiting:**
```bash
# Send 150 requests (exceeds 100/s limit)
for i in {1..150}; do
  curl -s -o /dev/null -w "%{http_code}\n" http://app.localhost/
done | sort | uniq -c
```

**Expected:**
- ✅ First ~100 requests: `200` status
- ✅ Remaining requests: `429` (Too Many Requests)

**Test compression:**
```bash
curl -H "Accept-Encoding: gzip" -I http://app.localhost/
```

**Expected headers:**
```
Content-Encoding: gzip
Vary: Accept-Encoding
```

**Test security headers:**
```bash
curl -I http://api.localhost:8080/health
```

**Expected headers (if security-headers middleware applied):**
```
X-Frame-Options: SAMEORIGIN
X-Content-Type-Options: nosniff
Referrer-Policy: strict-origin-when-cross-origin
```

## Test 7: Metrics (Prometheus)

```bash
curl http://traefik.localhost:8080/metrics
```

**Expected output (sample):**
```
# HELP traefik_entrypoint_requests_total Total requests
# TYPE traefik_entrypoint_requests_total counter
traefik_entrypoint_requests_total{entrypoint="web"} 42

# HELP traefik_entrypoint_request_duration_seconds Request duration
# TYPE traefik_entrypoint_request_duration_seconds histogram
traefik_entrypoint_request_duration_seconds_bucket{entrypoint="web",le="0.1"} 35
```

## Test 8: Log Files

```bash
# Access logs
tail -f traefik/logs/access.log

# Application logs
docker compose logs -f traefik
```

**Expected in access logs (JSON format):**
```json
{
  "ClientHost": "172.18.0.1",
  "RequestMethod": "GET",
  "RequestPath": "/",
  "RequestProtocol": "HTTP/1.1",
  "RequestHost": "app.localhost",
  "DownstreamStatus": 200,
  "Duration": 45000000
}
```

## Test 9: Health Check

```bash
# Traefik health endpoint
curl http://traefik.localhost:8080/ping

# Docker health status
docker compose ps traefik
```

**Expected:**
- ✅ `/ping` returns `OK`
- ✅ Container health status: `healthy`

## Test 10: Error Handling

**Test 404 (no route):**
```bash
curl -H "Host: nonexistent.localhost" http://localhost/
```

**Expected:**
- ✅ Status: `404 Not Found`
- ✅ Traefik error page

**Test 502 (backend down):**
```bash
# Stop web service
docker compose stop web

# Try to access
curl -H "Host: app.localhost" http://localhost/
```

**Expected:**
- ✅ Status: `503 Service Unavailable` or `502 Bad Gateway`
- ✅ Traefik error page

## Test 11: Configuration Validation

```bash
# Validate compose files merge correctly
docker compose -f docker-compose.yml -f docker-compose.traefik.yml config > /dev/null
echo $?

# Should output: 0 (success)
```

## Production Readiness Checklist

Before deploying to production, verify:

- [ ] Socket proxy enabled (uncommented in docker-compose.traefik.yml)
- [ ] `api.insecure=false` (dashboard protected)
- [ ] Dashboard basic auth configured
- [ ] Let's Encrypt configured (test with staging first)
- [ ] HTTPS entrypoint enabled (port 443)
- [ ] All service labels updated for HTTPS
- [ ] Rate limiting tested and tuned
- [ ] IP whitelisting for admin endpoints
- [ ] Metrics integrated with Prometheus
- [ ] Logs forwarded to HyperDX
- [ ] Alerting rules configured
- [ ] Backup of `acme.json` (certificates)
- [ ] Documentation updated with production domains

## Troubleshooting Common Issues

### Issue: Dashboard not accessible

**Symptoms:**
```bash
curl http://traefik.localhost:8080
# Connection refused
```

**Checks:**
1. Traefik running? `docker compose ps traefik`
2. Port 8080 free? `netstat -an | grep 8080`
3. Correct host header? `curl -H "Host: traefik.localhost" http://localhost:8080`

### Issue: Service not routing

**Symptoms:**
```bash
curl http://app.localhost
# 404 Not Found
```

**Checks:**
1. Service has `traefik.enable=true`?
2. Service in `meepleai` network?
3. Router visible in dashboard?
4. Check Traefik logs: `docker compose logs traefik | grep ERROR`

### Issue: Rate limiting not working

**Symptoms:** No 429 errors even after many requests

**Checks:**
1. Middleware applied to router?
2. Middleware file loaded? Check dashboard
3. Source IP detection: `curl -H "X-Forwarded-For: 1.2.3.4" http://app.localhost`

### Issue: Docker permission denied

**Symptoms:**
```
Error: permission denied while trying to connect to Docker socket
```

**Fix:**
- Windows: Restart Docker Desktop
- Linux: `sudo usermod -aG docker $USER` (logout/login required)

## Cleanup

```bash
# Stop all services
docker compose -f docker-compose.yml -f docker-compose.traefik.yml down

# Remove volumes (careful - removes logs!)
docker compose -f docker-compose.yml -f docker-compose.traefik.yml down -v

# Clean logs manually
rm -rf traefik/logs/*
```

## Performance Benchmarks

**Expected baseline (local development):**
- Dashboard load: < 500ms
- Request latency: < 50ms
- Throughput: > 1000 req/s (single backend)
- Memory usage: < 256MB
- CPU usage: < 5% idle, < 50% under load

**Benchmark command:**
```bash
# Install Apache Bench
sudo apt-get install apache2-utils

# Run benchmark (1000 requests, 10 concurrent)
ab -n 1000 -c 10 http://app.localhost/

# Check Traefik metrics during test
watch -n 1 'curl -s http://traefik.localhost:8080/metrics | grep traefik_entrypoint_requests_total'
```

## Next Steps

After successful testing:

1. ✅ Commit configuration to git
2. ✅ Create PR with test results
3. ✅ Document production deployment plan
4. ✅ Plan Let's Encrypt integration
5. ✅ Plan socket proxy migration
6. ✅ Schedule load testing session
7. ✅ Train team on Traefik operations

## Resources

- [Traefik Documentation](https://doc.traefik.io/traefik/)
- [Docker Provider Reference](https://doc.traefik.io/traefik/providers/docker/)
- [Middleware Reference](https://doc.traefik.io/traefik/middlewares/overview/)
- [Metrics Reference](https://doc.traefik.io/traefik/observability/metrics/prometheus/)
