# Docker Resource Limits & Reservations - Complete Documentation Index

**Overview**: Comprehensive guide to configuring CPU and memory limits/reservations for all services in MeepleAI's docker-compose stack.

**Navigation**: Choose based on your need:

---

## 📖 Documentation

### For Everyone: Start Here
1. **[Quick Reference Card](./docker-resource-limits-quick-reference.md)** ← Start here if in a hurry
   - Copy-paste configurations for each service
   - Total resource requirements table
   - Emergency troubleshooting checklist
   - Print-friendly format

### For Deep Understanding
2. **[Complete Resource Limits Guide](./docker-compose-resource-limits.md)** ← The full reference
   - Core concepts (limits vs reservations)
   - Development vs production configurations
   - Service-specific recommendations with calculations
   - Monitoring & adjustment procedures
   - Common issues & solutions

### For Problem Solving
3. **[FAQ & Troubleshooting](./docker-resource-limits-faq.md)** ← When things break
   - Diagnosis checklist
   - Service-specific issues (PostgreSQL OOM, Redis memory, etc.)
   - Root cause analysis
   - Step-by-step solutions
   - Prevention strategies

### For Implementation
4. **[Docker Compose Template](../../infra/docker-compose.resource-limits-template.yml)**
   - Ready-to-use configuration with comments
   - Development and production sections
   - All 15 services configured
   - Environment-specific settings
   - Copy and customize for your host

---

## 🛠️ Tools & Scripts

### Monitor Resource Usage
```bash
# Real-time monitoring
./scripts/docker-resource-monitor.sh --watch

# Capture baseline for comparison
./scripts/docker-resource-monitor.sh --baseline

# Analyze saved baseline
./scripts/docker-resource-monitor.sh --analyze resource-baseline.txt

# Export to CSV for analysis
./scripts/docker-resource-monitor.sh --export stats.csv
```

See: [docker-resource-monitor.sh](../../scripts/docker-resource-monitor.sh)

---

## 🚀 Quick Start Paths

### Path 1: Just Get Started (5 minutes)
1. Read: [Quick Reference Card](./docker-resource-limits-quick-reference.md)
2. Copy: [Docker Compose Template](../../infra/docker-compose.resource-limits-template.yml) to your compose file
3. Run: `docker-compose up -d`
4. Monitor: `./scripts/docker-resource-monitor.sh --watch`

### Path 2: Understand & Optimize (30 minutes)
1. Read: [Quick Reference Card](./docker-resource-limits-quick-reference.md)
2. Deep dive: [Complete Guide - Core Concepts section](./docker-compose-resource-limits.md#overview-limits-vs-reservations)
3. Find your services: [Service-Specific Recommendations](./docker-compose-resource-limits.md#service-specific-recommendations)
4. Adjust template values and deploy
5. Monitor trends: Run baseline every week, compare

### Path 3: Troubleshoot an Issue (10-20 minutes)
1. Identify symptom in: [FAQ & Troubleshooting](./docker-resource-limits-faq.md#common-issues--solutions)
2. Follow diagnosis steps
3. Apply recommended solution
4. Verify with: `./scripts/docker-resource-monitor.sh --watch`

### Path 4: Production Deployment (1 hour)
1. Review: [Development vs Production Checklist](./docker-compose-resource-limits.md#development-vs-production-checklist)
2. Calculate: [Production Requirements Table](./docker-compose-resource-limits-quick-reference.md#production-setup-recommended-host-32gb-ram-8-cores)
3. Configure: Adjust template for your host specs
4. Deploy: Use docker-compose.prod.yml
5. Monitor: Set up Prometheus alerts (see guide)
6. Document: Record actual usage patterns

---

## 📊 Key Concepts at a Glance

### Limits vs Reservations

| Aspect | Limits | Reservations |
|--------|--------|--------------|
| **Function** | Hard ceiling on resource usage | Guaranteed minimum resources |
| **Enforcement** | OOM kill if exceeded | Docker ensures resource available |
| **Use Case** | Prevent runaway processes | Protect critical services |
| **When Set** | Always | When host might be under pressure |

**Best Practice**: Set BOTH for all services
```yaml
deploy:
  resources:
    limits:         # Peak usage ceiling
      cpus: '1.0'
      memory: 1G
    reservations:   # Guaranteed minimum
      cpus: '0.5'
      memory: 512M
```

### Development vs Production

**Development** (8-16GB host):
- More lenient limits
- Flexible to allow debugging
- Lighter reservations
- Single instance services
- Optional observability

**Production** (32GB+ host):
- Conservative limits (catch leaks early)
- Guaranteed reservations for all critical services
- HA replicas (3x API, 2x Web)
- Full observability stack
- Regular monitoring

---

## 🔍 Service Categories

### Database & Caching (I/O-Bound)
- **PostgreSQL**: 1-2 CPU, 1-4G memory
  - Critical: Always set limits and reservations
  - Special: Needs `shm_size` parameter
- **Redis**: 0.5-1 CPU, 512M-2G memory
  - Critical: Must set `maxmemory` configuration
  - Special: Lightweight, good candidate for reservations
- **Qdrant**: 1.5-4 CPU, 2-8G memory
  - Special: Vector search CPU-intensive
  - Scaling: Can use on-disk storage for large collections

### ML Services (CPU/Memory Heavy)
- **Ollama** (LLM): 4-8 CPU, 8-16G memory
  - Special: VRAM control via environment variables
  - Note: CPU inference slow, GPU highly recommended
- **Embedding**: 2-4 CPU, 2-4G memory
  - Note: Lightweight models, good performance
- **Reranker**: 2-4 CPU, 2-4G memory
  - Note: Lightweight model server
- **SmolDocling** (VLM): 2-4 CPU, 3-6G memory
  - Special: Image processing needs extra memory
- **Unstructured** (PDF): 2-4 CPU, 2-4G memory
  - Variable: Depends on document complexity

### Observability (Lower Priority)
- **Prometheus**: 0.5-1 CPU, 512M-1G memory
- **Grafana**: 0.5-1 CPU, 512M-1G memory
- **HyperDX**: 2-4 CPU, 2-8G memory
  - Note: ClickHouse-based, can disable in dev
- **AlertManager**: 0.25 CPU, 256M memory
  - Lightweight notification engine

### Application Services (Balanced)
- **ASP.NET API**: 2-4 CPU, 1.5-3G memory
  - Efficient: Auto-respects Docker limits
  - HA: Run 3 replicas in production
- **Next.js Web**: 1-2 CPU, 1-1.5G memory
  - Lightweight: Node.js frontend
  - HA: Run 2 replicas in production
- **n8n**: 2-4 CPU, 2-4G memory
  - Special: Configure `NODE_OPTIONS` heap size
  - Note: Workflow state in-memory

---

## 📈 Resource Totals

### Minimum (Dev: Core Services Only)
- CPU Limit: ~10 cores
- Memory Limit: ~15G
- **Host Required**: 16GB RAM, 4 cores

### Recommended Development
- CPU Limit: ~24 cores
- Memory Limit: ~31.5G
- **Host Required**: 16GB RAM, 8 cores

### Production (HA)
- CPU Limit: ~46 cores
- Memory Limit: ~72.5G
- **Host Required**: 32GB+ RAM, 8+ cores

*See [Full Tables](./docker-resource-limits-quick-reference.md#total-resource-requirements) for breakdown*

---

## ⚠️ Common Issues Quick Links

| Issue | Solution | Doc |
|-------|----------|-----|
| PostgreSQL OOM kill | Adjust shared_buffers, increase shm_size | [FAQ](./docker-resource-limits-faq.md#postgresql-oom-kill) |
| Redis "OOM command not allowed" | Set maxmemory configuration | [FAQ](./docker-resource-limits-faq.md#redis-memory-errors) |
| Qdrant out of memory | Increase limit or use on-disk storage | [FAQ](./docker-resource-limits-faq.md#qdrant-out-of-memory) |
| ML service crashes | Reduce model count, increase VRAM | [FAQ](./docker-resource-limits-faq.md#ml-services-memory-issues) |
| n8n workflow timeout | Increase NODE_OPTIONS, boost memory | [FAQ](./docker-resource-limits-faq.md#n8n-workflow-timeouts) |
| API high memory | Optimize code, enable GC tuning | [FAQ](./docker-resource-limits-faq.md#aspnet-core-api-high-memory-usage) |
| Multiple services competing | Use reservations to prevent oversubscription | [FAQ](./docker-resource-limits-faq.md#multiple-services-competing-for-resources) |
| Docker Desktop slow | Increase resource allocation | [FAQ](./docker-resource-limits-faq.md#docker-desktop-limited-resources) |

---

## 📚 Reference Materials

### Official Documentation
- [Docker Compose Deploy Specification](https://docs.docker.com/reference/compose-file/deploy/)
- [Docker Resource Constraints](https://docs.docker.com/engine/containers/resource_constraints/)
- [PostgreSQL Resource Consumption](https://www.postgresql.org/docs/current/runtime-config-resource.html)

### Related MeepleAI Docs
- [System Architecture](../01-architecture/overview/system-architecture.md)
- [Testing Guide](./testing/test-writing-guide.md)
- [Development Environment Setup](./development-environment-setup.md)

### External Resources
- [Qdrant Memory Consumption](https://qdrant.tech/articles/memory-consumption/)
- [Ollama GPU Configuration](https://markaicode.com/ollama-gpu-memory-allocation-vram-errors/)
- [n8n Memory Management](https://docs.n8n.io/hosting/scaling/memory-errors/)
- [PostgreSQL Tuning Wiki](https://wiki.postgresql.org/wiki/Tuning_Your_PostgreSQL_Server)

---

## 🎯 Typical Workflows

### Workflow 1: Setup Development Environment
```bash
1. Clone repo
2. Copy docker-compose.yml
3. Copy docker-compose.resource-limits-template.yml values
4. cd infra && docker-compose up -d
5. ./scripts/docker-resource-monitor.sh --watch
6. Check all services healthy: docker-compose ps
7. Baseline: ./scripts/docker-resource-monitor.sh --baseline
```

### Workflow 2: Troubleshoot High Memory
```bash
1. Run: ./scripts/docker-resource-monitor.sh --watch
2. Identify service with high memory %
3. Check: docker logs <service>
4. Find issue in: docker-resource-limits-faq.md
5. Apply fix
6. Verify: ./scripts/docker-resource-monitor.sh --watch
```

### Workflow 3: Adjust for Smaller Host
```bash
1. Read: Quick Reference Card
2. Find current total requirements
3. Disable non-critical services (ollama, hyperdx, prometheus)
4. Reduce limits for less critical services
5. Calculate new total
6. Verify total < host capacity
7. Deploy and test
8. Save as docker-compose.small.yml
```

### Workflow 4: Scale to Production
```bash
1. Provision: 32GB+ host with 8+ cores
2. Review: Development vs Production Checklist
3. Copy: docker-compose.prod.yml with HA replicas
4. Deploy: docker-compose -f docker-compose.prod.yml up -d
5. Monitor: Set up Prometheus + Grafana alerts
6. Run baseline: ./scripts/docker-resource-monitor.sh --baseline
7. Monitor weekly and adjust reservations based on actual usage
```

---

## 🔄 Monitoring & Maintenance

### Weekly
```bash
# Capture baseline
./scripts/docker-resource-monitor.sh --baseline

# Review for anomalies
docker stats --all --no-stream
```

### Monthly
```bash
# Compare to baseline
docker stats --all --no-stream > stats-current.txt

# Check for memory leaks
docker logs api | grep -i "memory\|gc"
```

### Quarterly
```bash
# Review resource usage trends
# Adjust limits/reservations based on actual peaks

# Test failover (restart containers)
docker restart postgres redis qdrant

# Verify recovery time and data integrity
```

---

## 📝 Change Log

### Version 1.0 (2025-12-08)
- Initial release with complete documentation
- 4 main guides + template + monitoring script
- Covers all 15 services in MeepleAI stack
- Development and production configurations
- FAQ with 8 service-specific issues
- Quick reference card with copy-paste configs

---

## 🤝 Contributing

Found an issue or have a suggestion?

1. **Report Issue**: Document what you found + steps to reproduce
2. **Check FAQ**: Might already be documented
3. **Test Solution**: Verify it works on your system
4. **Document**: Update relevant section with findings
5. **Submit**: Create PR with update

---

## 📞 Support

### Getting Help
1. **Quick issue**: Check [FAQ](./docker-resource-limits-faq.md#quick-checks)
2. **Configuration question**: See [Quick Reference](./docker-resource-limits-quick-reference.md)
3. **Deep dive**: Read [Complete Guide](./docker-compose-resource-limits.md)
4. **Still stuck**: Gather diagnostics (see [Troubleshooting](./docker-resource-limits-faq.md#gather-diagnostics))

### Diagnostic Commands
```bash
# System overview
docker system df
docker stats --all --no-stream

# Service config
docker inspect postgres | grep -A 10 "Resources"

# Service health
docker logs postgres
docker ps --filter "status=exited"

# Historical data
./scripts/docker-resource-monitor.sh --analyze resource-baseline.txt
```

---

**Last Updated**: 2025-12-08
**Status**: Complete & Ready for Use
**Version**: 1.0

---

## Quick Navigation

- 🚀 **Just start**: [Quick Reference](./docker-resource-limits-quick-reference.md)
- 📖 **Full details**: [Complete Guide](./docker-compose-resource-limits.md)
- ❓ **Something broken**: [FAQ](./docker-resource-limits-faq.md)
- 📋 **Template**: [docker-compose.resource-limits-template.yml](../../infra/docker-compose.resource-limits-template.yml)
- 🛠️ **Monitor**: [docker-resource-monitor.sh](../../scripts/docker-resource-monitor.sh)
