# Codecov Setup Instructions

This document provides step-by-step instructions to complete the Codecov integration for the MeepleAI monorepo.

## Overview

The GitHub Actions workflow has been configured to collect and upload coverage reports to Codecov for both API and Web projects. To complete the setup, you need to:

1. Create a Codecov account
2. Add the repository to Codecov
3. Configure the Codecov token in GitHub secrets
4. Merge the PR to trigger the first coverage upload

## Step 1: Create Codecov Account

1. Visit https://codecov.io
2. Sign up using your GitHub account
3. Authorize Codecov to access your GitHub repositories

**Free Plan Features**:
- Unlimited public repositories
- Unlimited private repositories (limited users)
- Coverage reports and trends
- PR comments with coverage changes
- Branch comparison

## Step 2: Add Repository to Codecov

1. After signing in, click **"Add new repository"**
2. Search for `meepleai-monorepo`
3. Select the repository
4. Codecov will provide a **repository upload token**

**Important**: Copy the upload token - you'll need it in the next step.

## Step 3: Add Codecov Token to GitHub Secrets

1. Go to your GitHub repository: https://github.com/DegrassiAaron/meepleai-monorepo
2. Navigate to **Settings** → **Secrets and variables** → **Actions**
3. Click **"New repository secret"**
4. Configure the secret:
   - **Name**: `CODECOV_TOKEN`
   - **Value**: Paste the upload token from Codecov
5. Click **"Add secret"**

## Step 4: Verify Workflow Configuration

The workflow has been configured in `.github/workflows/ci.yml`:

### API Coverage (Job: ci-api)

```yaml
- name: Test with Coverage
  run: |
    dotnet test \
      --filter "FullyQualifiedName~..." \
      -p:CollectCoverage=true \
      -p:CoverletOutputFormat=lcov \
      -p:CoverletOutput=./tests/Api.Tests/coverage/

- name: Upload API Coverage to Codecov
  uses: codecov/codecov-action@v4
  with:
    files: apps/api/tests/Api.Tests/coverage/coverage.info
    flags: api
    name: api-coverage
    fail_ci_if_error: false
  env:
    CODECOV_TOKEN: ${{ secrets.CODECOV_TOKEN }}
```

### Web Coverage (Job: ci-web)

```yaml
- name: Test with Coverage
  run: pnpm test:coverage -- --ci

- name: Upload Web Coverage to Codecov
  uses: codecov/codecov-action@v4
  with:
    files: apps/web/coverage/lcov.info
    flags: web
    name: web-coverage
    fail_ci_if_error: false
  env:
    CODECOV_TOKEN: ${{ secrets.CODECOV_TOKEN }}
```

**Key Configuration**:
- `fail_ci_if_error: false` - Won't fail the build if Codecov upload fails
- `flags: api` / `flags: web` - Separates coverage by project
- `lcov` format - Standard format supported by Codecov

## Step 5: Trigger First Coverage Upload

Once the token is configured:

1. Merge a PR or push to `main` branch
2. GitHub Actions will run the CI workflow
3. Coverage reports will be uploaded to Codecov automatically
4. Visit Codecov dashboard to see the results

## Step 6: Add Coverage Badge to README

After the first successful upload:

1. Go to Codecov dashboard for your repository
2. Navigate to **Settings** → **Badge**
3. Copy the Markdown badge code
4. Add it to your repository's `README.md`

**Example Badge**:
```markdown
[![codecov](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo/branch/main/graph/badge.svg)](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo)
```

## Codecov Features

### Coverage Reports

Codecov provides detailed reports:
- **Overall coverage**: Combined API + Web coverage
- **Per-project coverage**: Separate reports for `api` and `web` flags
- **File-level coverage**: Drill down to specific files
- **Line-by-line coverage**: See which lines are covered/uncovered

### Pull Request Comments

Codecov automatically comments on PRs with:
- Coverage change (increase/decrease)
- New uncovered lines
- Files with coverage changes
- Link to detailed report

**Example PR Comment**:
```
📊 Coverage: 82.5% (+1.2%) compared to base
✅ All files have coverage above threshold
🟢 +45 lines covered
🔴 -12 lines uncovered
```

### Coverage Trends

Track coverage over time:
- Historical coverage graph
- Per-commit coverage changes
- Branch comparison
- Coverage by component/flag

### Coverage Sunburst

Visual representation of coverage:
- Interactive tree map
- Coverage by directory
- Identify areas needing improvement

## Configuration Files

### Optional: codecov.yml

You can add a `codecov.yml` file to the repository root for advanced configuration:

```yaml
# codecov.yml
coverage:
  status:
    project:
      default:
        target: 75%                # Minimum coverage target
        threshold: 1%              # Allow 1% decrease
    patch:
      default:
        target: 80%                # New code should have 80% coverage

comment:
  layout: "reach,diff,flags,tree"
  behavior: default

flags:
  api:
    paths:
      - apps/api/src/
  web:
    paths:
      - apps/web/src/
```

**Common Settings**:
- `target` - Minimum coverage percentage
- `threshold` - Allowed coverage decrease
- `patch` - Coverage requirements for new code
- `comment.layout` - What to include in PR comments

### Excluding Files from Coverage

Already configured in `.gitignore`:
```gitignore
coverage*.json
coverage*.xml
coverage*.info
coverage/
```

To exclude specific files from coverage calculation, add to `codecov.yml`:
```yaml
ignore:
  - "**/Migrations/**"
  - "**/*.g.cs"
  - "**/obj/**"
  - "**/bin/**"
```

## Troubleshooting

### Issue: "Codecov token not found"

**Solution**: Verify the secret is named exactly `CODECOV_TOKEN` (case-sensitive).

### Issue: Coverage upload fails

**Possible causes**:
1. Coverage file not generated (check test output)
2. Wrong file path in workflow
3. Invalid token

**Debug steps**:
```yaml
# Add before Codecov upload step
- name: Debug - List coverage files
  run: find . -name "*.info" -o -name "coverage.xml"
```

### Issue: Coverage shows 0%

**Possible causes**:
1. Test filter excluding all tests
2. Coverage not collected properly
3. Wrong source paths

**Verify**:
- Check that tests actually run in CI
- Verify coverage file exists and has content
- Check Codecov dashboard for error messages

### Issue: Codecov action version error

**Solution**: The workflow uses `codecov/codecov-action@v4`. If there are issues, check:
- https://github.com/codecov/codecov-action/releases
- Update to latest stable version if needed

## Monitoring Coverage

### Set Coverage Goals

1. **Initial Baseline** (Week 1):
   - Establish current coverage percentage
   - No enforcement, just measurement

2. **Short Term** (Month 1-2):
   - Set minimum at baseline - 5%
   - Gradually increase to 75%

3. **Medium Term** (Month 3-6):
   - Target 80% overall coverage
   - 85% for new code (patch coverage)

4. **Long Term** (6+ months):
   - Maintain 85%+ coverage
   - 90% for critical paths
   - Monitor and prevent regressions

### Review Coverage Regularly

- **Weekly**: Check coverage trend
- **Per PR**: Review coverage change
- **Monthly**: Identify low-coverage areas
- **Quarterly**: Adjust targets and goals

## Next Steps

1. ✅ **Workflow configured** - Coverage collection added to CI
2. 🔲 **Create Codecov account** - Sign up at https://codecov.io
3. 🔲 **Add repository** - Connect meepleai-monorepo
4. 🔲 **Configure token** - Add `CODECOV_TOKEN` secret
5. 🔲 **Merge PR** - Trigger first coverage upload
6. 🔲 **Add badge** - Display coverage in README
7. 🔲 **Configure codecov.yml** - Set targets and thresholds (optional)
8. 🔲 **Monitor trends** - Review coverage regularly

## Resources

- **Codecov Documentation**: https://docs.codecov.com/docs
- **GitHub Action**: https://github.com/codecov/codecov-action
- **Codecov Best Practices**: https://docs.codecov.com/docs/common-recipe-list
- **Sunburst Graph**: https://docs.codecov.com/docs/graphs#sunburst

## Support

If you encounter issues:

1. Check Codecov status: https://status.codecov.io
2. Review documentation: https://docs.codecov.com
3. GitHub Action logs in Actions tab
4. Codecov support: https://codecov.io/support

## Estimated Time

- **Setup**: 15-30 minutes
- **First report**: After next PR merge (~5 minutes)
- **Full configuration**: 1-2 hours (with custom thresholds)

---

**Last Updated**: 2025-10-09
**Status**: Workflow configured, awaiting token setup
