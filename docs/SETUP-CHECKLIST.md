# Codecov Setup Checklist

This checklist guides you through completing the Codecov integration for automated coverage tracking.

## ✅ Completed Steps

The following has already been configured:

- [x] Coverage documentation created (`docs/code-coverage.md`)
- [x] Baseline estimate established (`docs/coverage-baseline-estimate.md`)
- [x] PowerShell automation script (`tools/measure-coverage.ps1`)
- [x] CI/CD workflow updated with coverage collection
- [x] Codecov upload steps added for API and Web
- [x] Setup guide created (`docs/codecov-setup.md`)
- [x] README created with Codecov badge
- [x] Documentation index updated

## 🔲 Remaining Steps (5-10 minutes)

Follow these steps to activate Codecov:

### Step 1: Create Codecov Account (2 minutes)

1. Visit https://codecov.io
2. Click "Sign up with GitHub"
3. Authorize Codecov to access your repositories
4. Complete account setup

### Step 2: Add Repository (2 minutes)

1. In Codecov dashboard, click **"Add new repository"**
2. Search for `meepleai-monorepo`
3. Click to add the repository
4. **Important**: Copy the **upload token** shown (you'll need it next)

### Step 3: Configure GitHub Secret (2 minutes)

1. Go to: https://github.com/DegrassiAaron/meepleai-monorepo/settings/secrets/actions
2. Click **"New repository secret"**
3. Fill in:
   - **Name**: `CODECOV_TOKEN`
   - **Secret**: Paste the upload token from Step 2
4. Click **"Add secret"**

### Step 4: Trigger Coverage Upload (Automatic)

Option A: **Merge this PR** (recommended)
```bash
# Once PR is approved and CI passes, merge it
# Coverage will be uploaded automatically
```

Option B: **Push to main branch**
```bash
git checkout main
git pull
git merge feature/ISSUE-323-test-naming-docs
git push
```

### Step 5: Verify Coverage Report (1 minute)

1. Wait for GitHub Actions workflow to complete (~3-5 minutes)
2. Check Actions tab: https://github.com/DegrassiAaron/meepleai-monorepo/actions
3. Look for green checkmark on latest workflow run
4. Visit Codecov dashboard to see coverage report

### Step 6: Confirm Badge (1 minute)

1. Go to repository main page: https://github.com/DegrassiAaron/meepleai-monorepo
2. Check that the Codecov badge appears in README
3. Badge should show coverage percentage (e.g., "82%")
4. Click badge to view detailed report

## 📊 Expected Results

After completing the setup:

### Codecov Dashboard

You'll see:
- **Overall coverage percentage** (estimated 75-85%)
- **API coverage** (with "api" flag)
- **Web coverage** (with "web" flag)
- **Coverage trend graph**
- **File-by-file breakdown**
- **Sunburst visualization**

### Pull Request Comments

On future PRs, Codecov will automatically comment with:
- Coverage change vs base branch
- New uncovered lines
- Files with coverage changes
- Link to detailed report

Example:
```
📊 Coverage: 82.5% (+1.2%) compared to base
✅ All files have coverage above threshold
🟢 +45 lines covered
🔴 -12 lines uncovered
```

### README Badge

The badge will update automatically:
```markdown
[![codecov](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo/branch/main/graph/badge.svg)](https://codecov.io/gh/DegrassiAaron/meepleai-monorepo)
```

Shows: `codecov 82.5%` (or current coverage)

## 🎯 Next Steps After Setup

Once Codecov is working:

### Immediate (Week 1)

1. **Review baseline coverage**
   - Check which components have good coverage
   - Identify areas with low coverage
   - Document any intentional exclusions

2. **Set initial targets** (see `docs/code-coverage.md`)
   - Establish minimum threshold (e.g., 75%)
   - Set goals for increasing coverage
   - Add to project documentation

### Short Term (Month 1-2)

1. **Configure codecov.yml** (optional)
   ```yaml
   coverage:
     status:
       project:
         default:
           target: 75%
           threshold: 1%
   ```

2. **Add coverage to PR checklist**
   - Require coverage not to decrease
   - Review coverage changes in PRs
   - Focus on testing new code

3. **Identify coverage gaps**
   - Review Codecov sunburst chart
   - Prioritize critical paths
   - Add tests for low-coverage areas

### Medium Term (Month 3-6)

1. **Increase coverage gradually**
   - Target 80% overall
   - 85% for new code (patch coverage)
   - Focus on high-value areas first

2. **Automate coverage gates**
   - Fail PR if coverage drops > 1%
   - Require 80% coverage for new files
   - Set per-component targets

3. **Regular reviews**
   - Weekly: Check coverage trend
   - Monthly: Identify improvement areas
   - Quarterly: Adjust targets

## 🔧 Troubleshooting

### Issue: Codecov upload fails

**Check**:
1. Verify `CODECOV_TOKEN` secret is set correctly
2. Check GitHub Actions logs for error messages
3. Ensure coverage files are generated (look for `coverage.info`)

**Debug**:
```yaml
# Add to workflow before Codecov step
- name: Debug - List coverage files
  run: find . -name "*.info" -o -name "coverage.xml"
```

### Issue: Badge shows "unknown"

**Causes**:
- First upload not completed yet
- Invalid badge URL
- Codecov processing report

**Solution**: Wait 5-10 minutes after first upload, then refresh

### Issue: Coverage shows 0%

**Check**:
1. Coverage files have content: `cat apps/api/tests/Api.Tests/coverage/coverage.info`
2. Test filter not excluding all tests
3. Codecov dashboard for error messages

## 📝 Reference Links

- **Codecov Setup Guide**: `docs/codecov-setup.md`
- **Coverage Documentation**: `docs/code-coverage.md`
- **Baseline Estimate**: `docs/coverage-baseline-estimate.md`
- **CI Workflow**: `.github/workflows/ci.yml`

## 🆘 Need Help?

1. **Codecov Documentation**: https://docs.codecov.com/docs
2. **Codecov Support**: https://codecov.io/support
3. **GitHub Actions Logs**: Check workflow runs for errors
4. **Team Discussion**: Open an issue or discuss in team chat

## ✨ Success Criteria

You'll know the setup is complete when:

- ✅ Codecov badge shows coverage percentage
- ✅ Coverage reports visible in Codecov dashboard
- ✅ Separate reports for API and Web (flags)
- ✅ Historical trend graph shows data points
- ✅ Test PR gets automatic Codecov comment

---

**Estimated Total Time**: 5-10 minutes
**Last Updated**: 2025-10-09
**Status**: Ready for activation - token configuration required
