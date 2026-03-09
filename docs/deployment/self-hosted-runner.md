# Self-Hosted GitHub Actions Runner

**Epic**: #2967 — Zero-Cost CI/CD Infrastructure
**Platform**: Oracle Cloud Always Free (ARM64)

## Architecture

```
GitHub Actions (workflow trigger)
    │
    ▼
Oracle ARM64 VM (4 OCPU, 24GB RAM, 200GB disk)
    ├── GitHub Actions Runner (systemd service)
    ├── Docker + Docker Compose
    ├── .NET SDK 9.0
    ├── Node.js 20 + pnpm 10
    └── Monitoring (Node Exporter + Prometheus + Grafana)
```

## Quick Start

### 1. Provision VM

Use Oracle Cloud Console → Compute → Create Instance:
- **Image**: Ubuntu 22.04 ARM64
- **Shape**: VM.Standard.A1.Flex (4 OCPU, 24GB RAM)
- **Boot Volume**: 200GB
- **Cloud-init**: Paste contents of `infra/runner/cloud-init.yml`

Or manually after SSH:
```bash
ssh ubuntu@<VM_IP> 'bash -s' < infra/runner/setup-vm.sh
```

### 2. Install Runner

Get a registration token from GitHub → Settings → Actions → Runners → New self-hosted runner.

```bash
ssh ubuntu@<VM_IP>
cd /home/ubuntu
bash setup-runner.sh --token <GITHUB_TOKEN>
```

### 3. Enable Self-Hosted Runner in Workflows

Set the GitHub repository variable to switch all workflows:

```bash
gh variable set RUNNER --body "self-hosted"
```

To switch back to GitHub-hosted runners:
```bash
gh variable delete RUNNER
# All workflows fall back to ubuntu-latest
```

### 4. Install Maintenance Cron

```bash
ssh ubuntu@<VM_IP>
bash maintenance.sh --install-cron
```

### 5. Optional: Monitoring Stack

```bash
ssh ubuntu@<VM_IP>
cd /path/to/infra/runner
docker compose -f docker-compose.monitoring.yml up -d
# Set a secure Grafana password first:
export GF_ADMIN_PASSWORD=<secure_password>
docker compose -f docker-compose.monitoring.yml up -d
# Grafana: http://<VM_IP>:3001
# Prometheus: http://<VM_IP>:9090
```

## Workflow Migration

All 15 workflow files use the expression:
```yaml
runs-on: ${{ vars.RUNNER || 'ubuntu-latest' }}
```

| `vars.RUNNER` value | Behavior |
|---------------------|----------|
| *(not set)* | Uses `ubuntu-latest` (GitHub-hosted) |
| `self-hosted` | Uses self-hosted runner |
| `ubuntu-latest` | Explicitly uses GitHub-hosted |

This is a **zero-downtime toggle** — no workflow file changes needed to switch.

## Important Constraints

- The runner **must run as a bare systemd service** on the host (not inside a Docker container). GitHub Actions `services:` blocks require Docker socket access and host networking.
- All workflow steps use `shell: bash`. PowerShell (`pwsh`) is not installed on the self-hosted runner.

## Monitoring

### Health Check
```bash
./monitor.sh          # Human-readable output
./monitor.sh --json   # JSON output (for alerting)
./monitor.sh --check  # Exit 1 if unhealthy
```

### Maintenance Schedule (cron)
| Frequency | Time (UTC) | Tasks |
|-----------|-----------|-------|
| Every 5 min | * | Health check |
| Daily | 3:00 AM | Docker cleanup, disk check, log rotation |
| Weekly (Sun) | 4:00 AM | + System package updates |
| Monthly (1st) | 5:00 AM | + Runner update check |

## Troubleshooting

### Runner Not Starting

```bash
# Check service status
systemctl status actions.runner.*

# View runner logs
journalctl -u actions.runner.* -n 50

# Restart runner
sudo systemctl restart actions.runner.*
```

### Runner Shows "Offline" in GitHub

1. Check VM is running: `ssh ubuntu@<VM_IP> uptime`
2. Check runner service: `systemctl is-active actions.runner.*`
3. Check network: `curl -s https://api.github.com` (should return JSON)
4. Re-register if needed:
   ```bash
   cd ~/actions-runner
   ./config.sh remove --token <TOKEN>
   ./config.sh --url <REPO_URL> --token <NEW_TOKEN> --name oracle-arm-runner --labels self-hosted,linux,ARM64 --unattended --replace
   sudo ./svc.sh install && sudo ./svc.sh start
   ```

### Docker Disk Space Full

```bash
# Check usage
docker system df

# Aggressive cleanup
docker system prune -af --volumes

# Check system disk
df -h /
```

### ARM64 Compatibility Issues

Some Docker images don't have ARM64 variants. Symptoms:
- `exec format error` in CI logs
- Container exits immediately with code 1

Solutions:
1. Use multi-arch images (most official images support ARM64)
2. Build from source with `--platform linux/arm64`
3. For specific tools, check ARM64 support in their docs

### .NET Build Slow or OOM

```bash
# Check memory during build
watch -n1 free -m

# Increase swap if needed
sudo fallocate -l 8G /swapfile2
sudo mkswap /swapfile2
sudo swapon /swapfile2

# Limit .NET build parallelism
export DOTNET_CLI_TELEMETRY_OPTOUT=1
dotnet build -maxcpucount:2
```

### pnpm/Node Memory Issues

```bash
# Increase Node memory limit
export NODE_OPTIONS="--max-old-space-size=4096"
```

## Rollback

To revert to GitHub-hosted runners:

```bash
# Remove the RUNNER variable
gh variable delete RUNNER

# All workflows automatically fall back to ubuntu-latest
```

To decommission the VM:
1. Remove runner: `cd ~/actions-runner && ./config.sh remove --token <TOKEN>`
2. Terminate VM in Oracle Cloud Console
3. Delete VCN and security lists

## Cost

| Resource | Cost |
|----------|------|
| Oracle VM (Always Free) | $0/month |
| GitHub Actions minutes | $0/month (self-hosted = unlimited) |
| **Total** | **$0/month** |
