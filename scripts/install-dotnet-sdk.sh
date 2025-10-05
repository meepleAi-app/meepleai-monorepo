#!/usr/bin/env bash
set -euo pipefail

# Simple helper to install the .NET SDK via the official dotnet-install script.
# Usage:
#   DOTNET_CHANNEL=8.0 INSTALL_DIR=/usr/share/dotnet ./scripts/install-dotnet-sdk.sh
# The script defaults to channel 8.0 and /usr/share/dotnet when variables are not provided.

DOTNET_CHANNEL="${DOTNET_CHANNEL:-8.0}"
INSTALL_DIR="${INSTALL_DIR:-/usr/share/dotnet}"
SCRIPT_PATH="${DOTNET_INSTALL_SCRIPT:-$(mktemp)}"

cleanup() {
  if [[ -n "${KEEP_INSTALLER:-}" ]]; then
    return
  fi

  if [[ -f "$SCRIPT_PATH" && -w "$SCRIPT_PATH" ]]; then
    rm -f "$SCRIPT_PATH"
  fi
}
trap cleanup EXIT

if [[ ! -f "$SCRIPT_PATH" ]]; then
  curl -sSL https://dot.net/v1/dotnet-install.sh -o "$SCRIPT_PATH"
fi

bash "$SCRIPT_PATH" --channel "$DOTNET_CHANNEL" --install-dir "$INSTALL_DIR"

echo "dotnet CLI installed to $INSTALL_DIR"
