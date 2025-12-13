# Signed Commits Guide

> **Security Classification**: Internal
> **Last Updated**: 2025-12-13
> **Owner**: Engineering Lead

## Overview

Signed commits provide cryptographic proof that commits were made by verified authors. This guide explains how to set up GPG commit signing for the MeepleAI repository.

## Why Sign Commits?

| Benefit | Description |
|---------|-------------|
| **Identity Verification** | Proves commits came from the claimed author |
| **Tampering Detection** | Cryptographically detects any modifications |
| **Supply Chain Security** | Prevents impersonation in the git history |
| **Compliance** | Required by some security frameworks (SOC2, etc.) |

## Current Status

> **Recommendation**: Optional but encouraged for security-sensitive contributors

Signed commits are currently **optional** for MeepleAI. However, they are recommended for:
- Contributors with write access to main branches
- Security-related changes
- Release commits
- Infrastructure changes

## Setup Guide

### Prerequisites

- GPG installed on your system
- GitHub account with verified email
- Git 2.0+ installed

### Step 1: Install GPG

**macOS:**
```bash
brew install gnupg
```

**Windows:**
```bash
# Download from https://www.gnupg.org/download/
# Or use winget:
winget install GnuPG.GnuPG
```

**Linux (Debian/Ubuntu):**
```bash
sudo apt-get install gnupg
```

### Step 2: Generate a GPG Key

```bash
# Generate a new GPG key
gpg --full-generate-key

# When prompted:
# - Key type: RSA and RSA (default)
# - Key size: 4096 bits (recommended)
# - Expiration: 1-2 years (recommended)
# - Use your GitHub-verified email address
```

### Step 3: Get Your GPG Key ID

```bash
# List your GPG keys
gpg --list-secret-keys --keyid-format=long

# Output example:
# sec   rsa4096/3AA5C34371567BD2 2024-01-01 [SC]
#       Key fingerprint = A123 B456 C789 D012 E345 F678 G901 H234 I567 J890
# uid                 [ultimate] Your Name <your.email@example.com>
# ssb   rsa4096/42B317FD4BA89E7A 2024-01-01 [E]

# The key ID is after "rsa4096/" - in this example: 3AA5C34371567BD2
```

### Step 4: Configure Git

```bash
# Set your signing key
git config --global user.signingkey 3AA5C34371567BD2

# Enable automatic commit signing
git config --global commit.gpgsign true

# Enable automatic tag signing
git config --global tag.gpgsign true

# Tell Git to use GPG
git config --global gpg.program gpg
```

### Step 5: Add Key to GitHub

```bash
# Export your public key
gpg --armor --export 3AA5C34371567BD2

# Copy the entire output including:
# -----BEGIN PGP PUBLIC KEY BLOCK-----
# ...
# -----END PGP PUBLIC KEY BLOCK-----
```

1. Go to GitHub → Settings → SSH and GPG keys
2. Click "New GPG key"
3. Paste your public key
4. Click "Add GPG key"

### Step 6: Verify Setup

```bash
# Make a test commit
echo "test" >> test-signing.txt
git add test-signing.txt
git commit -m "test: verify GPG signing works"

# Check if commit is signed
git log --show-signature -1

# You should see:
# gpg: Signature made...
# gpg: Good signature from "Your Name <your.email@example.com>"
```

## Troubleshooting

### "gpg: signing failed: No secret key"

```bash
# Ensure GPG agent is running
gpgconf --launch gpg-agent

# Verify your key is available
gpg --list-secret-keys --keyid-format=long
```

### "gpg: signing failed: Inappropriate ioctl for device"

```bash
# Add to ~/.bashrc or ~/.zshrc:
export GPG_TTY=$(tty)

# Then reload:
source ~/.bashrc
```

### Key Password Prompts

Configure GPG agent to cache your passphrase:

```bash
# Create/edit ~/.gnupg/gpg-agent.conf
default-cache-ttl 3600
max-cache-ttl 86400
```

### Windows-Specific Issues

If using Git Bash on Windows:

```bash
# Add to ~/.bashrc:
export GPG_TTY=$(tty)

# Configure Git to use the right GPG:
git config --global gpg.program "C:/Program Files (x86)/GnuPG/bin/gpg.exe"
```

## VS Code Integration

Add to VS Code settings (`settings.json`):

```json
{
  "git.enableCommitSigning": true
}
```

## GitHub Verified Badge

Signed commits show a "Verified" badge on GitHub:

- ✅ **Verified**: Signature valid, email matches GitHub account
- ⚠️ **Partially verified**: Signature valid, email mismatch
- ❌ **Unverified**: Invalid or missing signature

## Team Adoption Plan

### Phase 1: Awareness (Current)
- Documentation available
- Optional for all contributors
- Training materials provided

### Phase 2: Encouraged
- Signing enabled by default in onboarding
- Signed commits highlighted in PRs
- Security team uses signing

### Phase 3: Required (Future)
- Branch protection requires signed commits
- All contributors must set up GPG
- Automated verification in CI

## Security Considerations

### Key Management

| Practice | Recommendation |
|----------|----------------|
| Key expiration | Set 1-2 year expiration |
| Key backup | Store encrypted backup offline |
| Key revocation | Have revocation certificate ready |
| Passphrase | Use strong, unique passphrase |

### Key Rotation

When rotating GPG keys:

1. Generate new key pair
2. Add new key to GitHub
3. Update Git config
4. Sign transition statement
5. Revoke old key (after grace period)

## CI/CD Integration

To verify signed commits in CI:

```yaml
# .github/workflows/verify-signatures.yml
name: Verify Commit Signatures

on: [pull_request]

jobs:
  verify:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with:
          fetch-depth: 0

      - name: Verify commit signatures
        run: |
          # Get commits in PR
          COMMITS=$(git log --format='%H' origin/${{ github.base_ref }}..HEAD)

          for commit in $COMMITS; do
            if ! git verify-commit $commit 2>/dev/null; then
              echo "Warning: Commit $commit is not signed"
              # Use 'exit 1' to fail if required
            fi
          done
```

## Related Documentation

- [Branch Protection Rules](./branch-protection-rules.md)
- [CONTRIBUTING.md](../../CONTRIBUTING.md)
- [GitHub GPG Documentation](https://docs.github.com/en/authentication/managing-commit-signature-verification)
- [GnuPG Manual](https://www.gnupg.org/documentation/manuals/gnupg/)

## FAQ

**Q: Do I need to sign every commit?**
A: If you enable `commit.gpgsign = true`, all commits will be signed automatically.

**Q: What if I forget my GPG passphrase?**
A: You'll need to generate a new key. This is why backup is important.

**Q: Can I use SSH keys instead of GPG?**
A: GitHub now supports SSH commit signing. See [GitHub SSH Signing Docs](https://docs.github.com/en/authentication/managing-commit-signature-verification/about-commit-signature-verification#ssh-commit-signature-verification).

**Q: Will unsigned commits be rejected?**
A: Currently no, but this may change. See the adoption plan above.
