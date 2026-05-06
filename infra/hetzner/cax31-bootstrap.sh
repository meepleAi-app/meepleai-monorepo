#!/usr/bin/env bash
# =============================================================================
# CAX21/CAX31 ARM64 server bootstrap (Hetzner Cloud)
# =============================================================================
# Applies to: meepleai-app staging/prod servers on Hetzner CAX21 (8GB) or
# CAX31 (16GB) ARM64. Same hardening rules apply.
#
# This script is idempotent: safe to run multiple times.
#
# Spec: docs/superpowers/specs/2026-05-06-database-network-isolation-design.md
# Issue: #795
# =============================================================================
set -euo pipefail

export DEBIAN_FRONTEND=noninteractive

echo "==> System update"
apt-get update && apt-get upgrade -y

echo "==> Install essential tools"
# Note: ufw is intentionally NOT installed here. On Ubuntu 24.04 it is in
# conflict with iptables-persistent (apt removes ufw when installing
# iptables-persistent). We use iptables directly for full Docker bypass control.
apt-get install -y \
  curl wget git ca-certificates gnupg lsb-release \
  cifs-utils iptables-persistent fail2ban htop ncdu jq \
  postgresql-client-common age

echo "==> Install Docker (ARM64)"
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker

echo "==> Install Docker Compose plugin"
mkdir -p ~/.docker/cli-plugins
curl -SL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64 \
  -o ~/.docker/cli-plugins/docker-compose
chmod +x ~/.docker/cli-plugins/docker-compose

echo "==> Firewall: INPUT chain — public-facing rules"
# Default policy: drop incoming, allow loopback + established.
iptables -P INPUT DROP
iptables -P FORWARD DROP
iptables -P OUTPUT ACCEPT
iptables -A INPUT -i lo -j ACCEPT
iptables -A INPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT

# Allow SSH (only entrypoint we want public)
iptables -C INPUT -p tcp --dport 22 -j ACCEPT 2>/dev/null \
  || iptables -A INPUT -p tcp --dport 22 -j ACCEPT

# IPv6 mirror (drop everything except SSH and established/loopback)
ip6tables -P INPUT DROP
ip6tables -P FORWARD DROP
ip6tables -P OUTPUT ACCEPT
ip6tables -A INPUT -i lo -j ACCEPT
ip6tables -A INPUT -m conntrack --ctstate ESTABLISHED,RELATED -j ACCEPT
ip6tables -C INPUT -p tcp --dport 22 -j ACCEPT 2>/dev/null \
  || ip6tables -A INPUT -p tcp --dport 22 -j ACCEPT

echo "==> Firewall: DOCKER-USER chain — bypass-proof (Issue #795)"
# Docker bypasses the INPUT chain via its own DOCKER chain. The DOCKER-USER
# chain is the only one Docker does NOT overwrite. We DROP all inbound traffic
# to internal application ports here, so even an accidental `docker run -p X:X`
# (without 127.0.0.1: prefix) cannot expose the service to the public.
EXT_IF="$(ip route get 8.8.8.8 2>/dev/null | awk '{for(i=1;i<=NF;i++) if($i=="dev") print $(i+1)}' | head -1)"
EXT_IF="${EXT_IF:-eth0}"
echo "    External interface detected: $EXT_IF"

# Application ports that MUST NOT be reachable from public Internet.
# Split in two groups because iptables `multiport --dports` is limited to 15.
APP_PORTS_GROUP1="5432,6379,9000,9001,9090,9093,3000,3001,8000,8001,8002,8003,8004,8025,1025"
APP_PORTS_GROUP2="5678,3100,11434,8090"

for PORTS in "$APP_PORTS_GROUP1" "$APP_PORTS_GROUP2"; do
  iptables -C DOCKER-USER -i "$EXT_IF" -p tcp -m multiport --dports "$PORTS" -j DROP 2>/dev/null \
    || iptables -I DOCKER-USER -i "$EXT_IF" -p tcp -m multiport --dports "$PORTS" -j DROP
  ip6tables -C DOCKER-USER -i "$EXT_IF" -p tcp -m multiport --dports "$PORTS" -j DROP 2>/dev/null \
    || ip6tables -I DOCKER-USER -i "$EXT_IF" -p tcp -m multiport --dports "$PORTS" -j DROP || true
done

echo "==> Persist iptables rules"
netfilter-persistent save

echo "==> Fail2ban for SSH brute-force protection"
systemctl enable --now fail2ban

echo "==> Swap (8 GB safety net)"
if [ ! -f /swapfile ]; then
  fallocate -l 8G /swapfile
  chmod 600 /swapfile
  mkswap /swapfile
  swapon /swapfile
  echo '/swapfile none swap sw 0 0' >> /etc/fstab
fi

echo "==> Storage Box mount preparation"
mkdir -p /mnt/storagebox
# Credentials file MUST be created manually before first mount (security)
if [ ! -f /etc/cifs-creds-storagebox ]; then
  echo "ATTENTION: Create /etc/cifs-creds-storagebox manually with:"
  echo "  username=u<box-id>"
  echo "  password=<box-password>"
  echo "  Then chmod 600 /etc/cifs-creds-storagebox"
fi

echo "==> Bootstrap complete. Reboot recommended."
echo "==> Next steps:"
echo "  1. Create /etc/cifs-creds-storagebox"
echo "  2. Add Storage Box mount to /etc/fstab"
echo "  3. Deploy docker-compose stack"
echo "  4. Configure DNS"
