#!/usr/bin/env bash
set -euo pipefail

echo "==> System update"
apt-get update && apt-get upgrade -y

echo "==> Install essential tools"
apt-get install -y \
  curl wget git ca-certificates gnupg lsb-release \
  cifs-utils ufw fail2ban htop ncdu jq \
  postgresql-client-common

echo "==> Install Docker (ARM64)"
curl -fsSL https://get.docker.com | sh
systemctl enable --now docker

echo "==> Install Docker Compose plugin"
mkdir -p ~/.docker/cli-plugins
curl -SL https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64 \
  -o ~/.docker/cli-plugins/docker-compose
chmod +x ~/.docker/cli-plugins/docker-compose

echo "==> Firewall (only 22, 80, 443 from internet)"
ufw default deny incoming
ufw default allow outgoing
ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
ufw --force enable

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
