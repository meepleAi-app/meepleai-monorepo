#!/usr/bin/env bash
# Oracle ARM64 VM Setup Script
# Epic #2967: Zero-Cost CI/CD Infrastructure
#
# Run this AFTER cloud-init completes (or instead of cloud-init for manual setup).
# Usage: ssh ubuntu@<VM_IP> 'bash -s' < setup-vm.sh

set -euo pipefail

echo "=== MeepleAI Runner VM Setup ==="
echo "Target: Oracle ARM64 (4 OCPU, 24GB RAM)"

# System updates
echo "[1/7] Updating system packages..."
sudo apt-get update -qq && sudo apt-get upgrade -y -qq

# Install dependencies
echo "[2/7] Installing dependencies..."
sudo apt-get install -y -qq \
  apt-transport-https ca-certificates curl gnupg lsb-release \
  jq unzip build-essential libicu-dev libgdiplus

# Docker
echo "[3/7] Installing Docker..."
if ! command -v docker &>/dev/null; then
  curl -fsSL https://get.docker.com | sudo sh
  sudo usermod -aG docker ubuntu
  sudo systemctl enable docker
  sudo systemctl start docker
  echo "Docker installed. You may need to re-login for group changes."
else
  echo "Docker already installed: $(docker --version)"
fi

# Docker Compose v2
echo "[4/7] Installing Docker Compose v2..."
sudo mkdir -p /usr/local/lib/docker/cli-plugins
sudo curl -SL "https://github.com/docker/compose/releases/latest/download/docker-compose-linux-aarch64" \
  -o /usr/local/lib/docker/cli-plugins/docker-compose
sudo chmod +x /usr/local/lib/docker/cli-plugins/docker-compose

# .NET SDK 9.0
echo "[5/7] Installing .NET SDK 9.0..."
if ! command -v dotnet &>/dev/null; then
  curl -sSL https://dot.net/v1/dotnet-install.sh | bash /dev/stdin --channel 9.0 --install-dir /usr/share/dotnet
  sudo ln -sf /usr/share/dotnet/dotnet /usr/local/bin/dotnet
  echo 'export DOTNET_ROOT=/usr/share/dotnet' | sudo tee -a /etc/environment >/dev/null
else
  echo ".NET already installed: $(dotnet --version)"
fi

# Node.js 20 + pnpm
echo "[6/7] Installing Node.js 20 + pnpm..."
if ! command -v node &>/dev/null; then
  curl -fsSL https://deb.nodesource.com/setup_20.x | sudo bash -
  sudo apt-get install -y -qq nodejs
  sudo npm install -g pnpm@10
else
  echo "Node.js already installed: $(node --version)"
fi

# Swap (4GB)
echo "[7/7] Configuring swap..."
if [ ! -f /swapfile ]; then
  sudo fallocate -l 4G /swapfile
  sudo chmod 600 /swapfile
  sudo mkswap /swapfile
  sudo swapon /swapfile
  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab >/dev/null
else
  echo "Swap already configured."
fi

echo ""
echo "=== Setup Complete ==="
echo "Docker:  $(docker --version 2>/dev/null || echo 'requires re-login')"
echo "Compose: $(docker compose version 2>/dev/null || echo 'requires re-login')"
echo ".NET:    $(dotnet --version 2>/dev/null || echo 'not found')"
echo "Node:    $(node --version 2>/dev/null || echo 'not found')"
echo "pnpm:    $(pnpm --version 2>/dev/null || echo 'not found')"
echo ""
echo "Next: Run setup-runner.sh to install the GitHub Actions runner."
