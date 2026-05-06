#!/usr/bin/env bash
# =============================================================================
# security-audit.sh — External port scan against staging/prod with whitelist
# =============================================================================
# Usage:
#   bash infra/scripts/security-audit.sh [TARGET_IP]
#
# Default target: staging (204.168.135.69)
# Override:       SECURITY_AUDIT_TARGET=1.2.3.4 bash infra/scripts/security-audit.sh
#                 bash infra/scripts/security-audit.sh 1.2.3.4
#
# Whitelist: only TCP/22 (SSH) is expected to be OPEN from public Internet.
# All other ports MUST be filtered/closed. Exit 1 on drift.
#
# Required: nmap (apt install nmap) OR fallback to /dev/tcp (limited)
#
# Spec: docs/superpowers/specs/2026-05-06-database-network-isolation-design.md
# Issue: #795
# =============================================================================

set -euo pipefail

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------
TARGET="${SECURITY_AUDIT_TARGET:-${1:-204.168.135.69}}"

# Ports we explicitly check. Each must be in either WHITELIST or BLACKLIST.
ALL_PORTS=(22 80 443 5432 6379 8080 8090 3000 3001 8000 8001 8002 8003 8004 8025 1025 5678 3100 9000 9001 9090 9093 11434)

# Ports allowed to be open from public Internet
WHITELIST=(22)

# Pretty colors (only when stdout is a TTY)
if [[ -t 1 ]]; then
  RED='\033[0;31m'
  GREEN='\033[0;32m'
  YELLOW='\033[1;33m'
  BLUE='\033[0;34m'
  NC='\033[0m'
else
  RED=''; GREEN=''; YELLOW=''; BLUE=''; NC=''
fi

# ---------------------------------------------------------------------------
# Detect scanner
# ---------------------------------------------------------------------------
USE_NMAP=false
if command -v nmap >/dev/null 2>&1; then
  USE_NMAP=true
fi

is_whitelisted() {
  local p="$1"
  for w in "${WHITELIST[@]}"; do
    [[ "$w" == "$p" ]] && return 0
  done
  return 1
}

# ---------------------------------------------------------------------------
# Scan with nmap (preferred)
# ---------------------------------------------------------------------------
scan_nmap() {
  local ports
  ports=$(IFS=,; echo "${ALL_PORTS[*]}")
  # -Pn: skip host discovery (Hetzner blocks ICMP, would falsely mark host down)
  # -T4: aggressive timing
  # --open: omit closed/filtered from raw output (we parse separately)
  # -oG: grepable output, easy to parse
  nmap -Pn -T4 -p "$ports" -oG - "$TARGET" 2>/dev/null | grep -E '^Host:'
}

parse_nmap_open_ports() {
  # Extract open ports from nmap grepable output
  local nmap_output="$1"
  echo "$nmap_output" | sed -n 's/.*Ports: //p' | tr ',' '\n' | \
    awk -F'/' '$2 == "open" { gsub(/^[ \t]+/, "", $1); print $1 }'
}

# ---------------------------------------------------------------------------
# Fallback scan with /dev/tcp (no nmap installed)
# ---------------------------------------------------------------------------
scan_fallback() {
  local opens=()
  for p in "${ALL_PORTS[@]}"; do
    if timeout 3 bash -c ">/dev/tcp/$TARGET/$p" 2>/dev/null; then
      opens+=("$p")
    fi
  done
  printf '%s\n' "${opens[@]}"
}

# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------
echo -e "${BLUE}=== Security Audit: external port scan ===${NC}"
echo "Target:    $TARGET"
echo "Scanner:   $([[ $USE_NMAP == true ]] && echo "nmap" || echo "/dev/tcp fallback")"
echo "Whitelist: ${WHITELIST[*]}"
echo "Total checked: ${#ALL_PORTS[@]} ports"
echo ""

OPEN_PORTS=()
if [[ "$USE_NMAP" == "true" ]]; then
  raw=$(scan_nmap)
  if [[ -z "$raw" ]]; then
    echo -e "${RED}ERROR: nmap returned no output (host unreachable?)${NC}" >&2
    exit 2
  fi
  while IFS= read -r p; do
    [[ -n "$p" ]] && OPEN_PORTS+=("$p")
  done < <(parse_nmap_open_ports "$raw")
else
  while IFS= read -r p; do
    [[ -n "$p" ]] && OPEN_PORTS+=("$p")
  done < <(scan_fallback)
fi

# ---------------------------------------------------------------------------
# Report
# ---------------------------------------------------------------------------
DRIFT=()
WHITELIST_VERIFIED=()

for p in "${ALL_PORTS[@]}"; do
  is_open=false
  for op in "${OPEN_PORTS[@]:-}"; do
    [[ "$op" == "$p" ]] && is_open=true && break
  done

  if is_whitelisted "$p"; then
    if [[ "$is_open" == "true" ]]; then
      WHITELIST_VERIFIED+=("$p")
      echo -e "  ${GREEN}✓${NC} $p OPEN (whitelisted)"
    else
      echo -e "  ${YELLOW}!${NC} $p whitelisted but NOT open — expected open"
    fi
  else
    if [[ "$is_open" == "true" ]]; then
      DRIFT+=("$p")
      echo -e "  ${RED}✗${NC} $p OPEN — DRIFT (should be filtered/closed)"
    else
      echo -e "  ${GREEN}✓${NC} $p closed/filtered"
    fi
  fi
done

echo ""
echo -e "${BLUE}=== Summary ===${NC}"
echo "Whitelist verified: ${#WHITELIST_VERIFIED[@]} / ${#WHITELIST[@]}"
echo "Drift detected:     ${#DRIFT[@]}"
if [[ ${#DRIFT[@]} -gt 0 ]]; then
  echo -e "${RED}❌ FAIL — unauthorized open ports: ${DRIFT[*]}${NC}"
  echo ""
  echo "Run on staging:"
  echo "  ssh deploy@$TARGET 'sudo ss -tlnp 2>/dev/null | grep -E \":(${DRIFT[0]})\"'"
  echo "  ssh deploy@$TARGET 'docker ps --format \"{{.Names}} {{.Ports}}\" | grep -E \"0.0.0.0:|::\"'"
  exit 1
fi

echo -e "${GREEN}✅ PASS — only whitelisted ports are reachable${NC}"
exit 0
