#!/usr/bin/env bash
# GitHub Actions Self-Hosted Runner Installation
# Epic #2967: Zero-Cost CI/CD Infrastructure
#
# Usage: ./setup-runner.sh --token <GITHUB_RUNNER_TOKEN> [--name <RUNNER_NAME>]
#
# Get token from: GitHub → Settings → Actions → Runners → New self-hosted runner

set -euo pipefail

# Defaults
RUNNER_VERSION="2.321.0"
RUNNER_NAME="oracle-arm-runner"
RUNNER_LABELS="self-hosted,linux,ARM64"
REPO_URL="${REPO_URL:-https://github.com/meepleAi-app/meepleai-monorepo}"
RUNNER_DIR="/home/ubuntu/actions-runner"
RUNNER_TOKEN=""

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --token) RUNNER_TOKEN="$2"; shift 2 ;;
    --name) RUNNER_NAME="$2"; shift 2 ;;
    --version) RUNNER_VERSION="$2"; shift 2 ;;
    --labels) RUNNER_LABELS="$2"; shift 2 ;;
    --repo) REPO_URL="$2"; shift 2 ;;
    *) echo "Unknown option: $1"; exit 1 ;;
  esac
done

if [ -z "$RUNNER_TOKEN" ]; then
  echo "Error: --token is required."
  echo "Get it from: GitHub → Settings → Actions → Runners → New self-hosted runner"
  exit 1
fi

echo "=== GitHub Actions Runner Setup ==="
echo "Version: ${RUNNER_VERSION}"
echo "Name:    ${RUNNER_NAME}"
echo "Labels:  ${RUNNER_LABELS}"
echo "Repo:    ${REPO_URL}"
echo ""

# Download runner
echo "[1/4] Downloading runner v${RUNNER_VERSION} (ARM64)..."
mkdir -p "${RUNNER_DIR}" && cd "${RUNNER_DIR}"

TARBALL="actions-runner-linux-arm64-${RUNNER_VERSION}.tar.gz"
if [ ! -f "${TARBALL}" ]; then
  curl -sL "https://github.com/actions/runner/releases/download/v${RUNNER_VERSION}/${TARBALL}" -o "${TARBALL}"
fi
tar xzf "${TARBALL}"

# Configure runner
echo "[2/4] Configuring runner..."
./config.sh \
  --url "${REPO_URL}" \
  --token "${RUNNER_TOKEN}" \
  --name "${RUNNER_NAME}" \
  --labels "${RUNNER_LABELS}" \
  --unattended \
  --replace

# Install as systemd service
echo "[3/4] Installing systemd service..."
sudo ./svc.sh install
sudo ./svc.sh start

# Enable auto-start on boot
echo "[4/4] Enabling auto-start..."
SERVICE_NAME=$(systemctl list-units --type=service | grep actions.runner | awk '{print $1}')
if [ -n "${SERVICE_NAME}" ]; then
  sudo systemctl enable "${SERVICE_NAME}"
  echo "Service enabled: ${SERVICE_NAME}"
fi

echo ""
echo "=== Runner Installation Complete ==="
echo "Status: $(sudo ./svc.sh status 2>/dev/null | head -3)"
echo ""
echo "Verify at: ${REPO_URL}/settings/actions/runners"
echo "Runner should show as 'Idle' within 30 seconds."
