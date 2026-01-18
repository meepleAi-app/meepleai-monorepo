#!/usr/bin/env python3
"""
OAuth Secrets Validation Script
Validates that OAuth provider credentials are properly configured
"""

import os
import sys
from pathlib import Path

# Colors
GREEN = '\033[0;32m'
RED = '\033[0;31m'
YELLOW = '\033[1;33m'
NC = '\033[0m'

SECRETS_DIR = Path(__file__).parent.parent / "infra" / "secrets"

def validate_secret(provider: str, secret_type: str, filename: str, min_length: int) -> bool:
    """Validate a single secret file"""
    file_path = SECRETS_DIR / filename

    if not file_path.exists():
        print(f"{RED}[X] MISSING{NC} {provider} {secret_type}: File not found ({file_path})")
        return False

    value = file_path.read_text().strip()

    if not value:
        print(f"{RED}[X] EMPTY{NC} {provider} {secret_type}: File is empty")
        return False

    # Check for placeholder values
    placeholders = ['placeholder', 'your-', '${']
    if any(p in value.lower() for p in placeholders):
        print(f"{YELLOW}[!]  PLACEHOLDER{NC} {provider} {secret_type}: Contains placeholder text")
        return False

    # Check minimum length
    if len(value) < min_length:
        print(f"{YELLOW}[!]  TOO SHORT{NC} {provider} {secret_type}: Length {len(value)} < {min_length}")
        return False

    # Check for whitespace
    if ' ' in value:
        print(f"{YELLOW}[!]  WHITESPACE{NC} {provider} {secret_type}: Contains spaces")
        return False

    # Mask value for display
    masked = f"{value[:4]}...{value[-4:]}" if len(value) > 8 else "***"
    print(f"{GREEN}[OK]{NC} {provider} {secret_type}: {masked} (length: {len(value)})")
    return True

def main():
    print("=" * 50)
    print("   OAuth Configuration Validation")
    print("=" * 50)
    print()

    providers_valid = 0
    providers_invalid = 0

    # Validate Google OAuth
    print("=== Google OAuth ===")
    google_valid = (
        validate_secret("Google", "Client ID", "google-oauth-client-id.txt", 20) and
        validate_secret("Google", "Client Secret", "google-oauth-client-secret.txt", 20)
    )
    if google_valid:
        providers_valid += 1
    else:
        providers_invalid += 1
    print()

    # Validate Discord OAuth
    print("=== Discord OAuth ===")
    discord_valid = (
        validate_secret("Discord", "Client ID", "discord-oauth-client-id.txt", 10) and
        validate_secret("Discord", "Client Secret", "discord-oauth-client-secret.txt", 20)
    )
    if discord_valid:
        providers_valid += 1
    else:
        providers_invalid += 1
    print()

    # Validate GitHub OAuth
    print("=== GitHub OAuth ===")
    github_valid = (
        validate_secret("GitHub", "Client ID", "github-oauth-client-id.txt", 10) and
        validate_secret("GitHub", "Client Secret", "github-oauth-client-secret.txt", 20)
    )
    if github_valid:
        providers_valid += 1
    else:
        providers_invalid += 1
    print()

    # Validate other critical secrets
    print("=== Other Critical Secrets ===")
    openrouter_valid = validate_secret("OpenRouter", "API Key", "openrouter-api-key.txt", 30)
    postgres_valid = validate_secret("PostgreSQL", "Password", "postgres-password.txt", 8)
    redis_valid = validate_secret("Redis", "Password", "redis-password.txt", 8)
    print()

    # Summary
    print("=" * 50)
    print("   Summary")
    print("=" * 50)
    print()
    print(f"Valid OAuth Providers:   {providers_valid} / 3")
    print(f"Invalid/Missing:         {providers_invalid} / 3")
    print()

    if providers_valid == 3:
        print(f"{GREEN}[OK] All OAuth providers are properly configured{NC}")
        print(f"{GREEN}[OK] All critical secrets validated{NC}")
        return 0
    elif providers_valid == 0:
        print(f"{RED}[X] No OAuth providers configured - social login will not work{NC}")
        return 1
    else:
        print(f"{YELLOW}[!]  Partial OAuth configuration - some social login methods will not work{NC}")
        return 0

if __name__ == "__main__":
    try:
        sys.exit(main())
    except Exception as e:
        print(f"{RED}[X] ERROR: {e}{NC}")
        sys.exit(1)
