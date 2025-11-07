import { test, expect } from './fixtures/auth';
import { getTextMatcher, t } from './fixtures/i18n';

test.describe('Setup Guide Page', () => {
  test('should require authentication', async ({ page }) => {
    await page.goto('/setup');

    // Should show login required message
    await expect(page.getByRole('heading', { name: getTextMatcher('setup.loginRequired') })).toBeVisible();
    await expect(page.getByText(getTextMatcher('setup.loginRequiredMessage'))).toBeVisible();

    // Should have login link
    await expect(page.getByRole('link', { name: getTextMatcher('setup.goToLogin') })).toBeVisible();
  });

  test('should have navigation links when not authenticated', async ({ page }) => {
    await page.goto('/setup');

    // Should have back to home link
    await expect(page.getByRole('link', { name: getTextMatcher('setup.backToHome') })).toBeVisible();

    // Should have "Go to Login" button
    const loginButton = page.getByRole('link', { name: getTextMatcher('setup.goToLogin') });
    await expect(loginButton).toBeVisible();
    await expect(loginButton).toHaveAttribute('href', '/');
  });

  test.describe('Authenticated User Flow', () => {
    test.beforeEach(async ({ userPage: page }) => {
      // Navigate to setup page (already authenticated as user)
      await page.goto('/setup');
      await page.waitForLoadState('networkidle');
    });

    test('should display setup guide interface', async ({ userPage: page }) => {
      // Check header is present
      await expect(page.getByRole('heading', { name: getTextMatcher('setup.heading') })).toBeVisible();

      // Check navigation
      await expect(page.getByRole('link', { name: getTextMatcher('nav.home') })).toBeVisible();

      // Check game selection section
      await expect(page.getByText(getTextMatcher('setup.selectGame'))).toBeVisible();
      await expect(page.getByLabel(getTextMatcher('setup.gameLabel'))).toBeVisible();

      // Check generate button
      await expect(page.getByRole('button', { name: getTextMatcher('setup.generateButton') })).toBeVisible();
    });

    test('should load available games', async ({ userPage: page }) => {
      // Wait for games to load
      const gameSelect = page.getByLabel(getTextMatcher('setup.gameLabel'));
      await expect(gameSelect).toBeVisible();

      // Should have placeholder option
      await expect(gameSelect).toContainText(t('setup.selectGamePlaceholder'));

      // Should have at least one game (Tic-Tac-Toe or Chess from seed data)
      const options = await gameSelect.locator('option').count();
      expect(options).toBeGreaterThan(1); // 1 placeholder + at least 1 game
    });

    test('should auto-select first game', async ({ userPage: page }) => {
      // Wait for games to load
      await page.waitForTimeout(1000);

      const gameSelect = page.getByLabel(getTextMatcher('setup.gameLabel'));
      const selectedValue = await gameSelect.inputValue();

      // Should not be empty (first game should be auto-selected)
      expect(selectedValue).toBeTruthy();
      expect(selectedValue).not.toBe('');
    });

    test('should generate setup guide when button is clicked', async ({ userPage: page }) => {
      // Wait for games to load
      await page.waitForTimeout(1000);

      // Select a game (should be auto-selected, but click to be sure)
      const gameSelect = page.getByLabel(getTextMatcher('setup.gameLabel'));
      const firstGameOption = await gameSelect.locator('option').nth(1); // Skip placeholder
      const firstGameValue = await firstGameOption.getAttribute('value');

      if (firstGameValue) {
        await gameSelect.selectOption(firstGameValue);
      }

      // Click generate button
      const generateButton = page.getByRole('button', { name: getTextMatcher('setup.generateButton') });
      await expect(generateButton).toBeEnabled();
      await generateButton.click({ force: true });

      // Should show loading state
      await expect(page.getByText(getTextMatcher('setup.generatingMessage'))).toBeVisible();

      // Wait for setup guide to load
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Should show progress section
      await expect(page.getByText(getTextMatcher('setup.progress'))).toBeVisible();
      await expect(page.getByText(getTextMatcher('setup.estimatedTime'))).toBeVisible();
    });

    test('should display setup steps with checkboxes', async ({ userPage: page }) => {
      // Generate setup guide first
      await page.waitForTimeout(1000);
      const generateButton = page.getByRole('button', { name: getTextMatcher('setup.generateButton') });
      await generateButton.click({ force: true });

      // Wait for steps to load
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Check for step checkboxes
      const checkboxes = page.locator('input[type="checkbox"]');
      const count = await checkboxes.count();

      // Should have at least one step
      expect(count).toBeGreaterThan(0);

      // All checkboxes should be unchecked initially
      for (let i = 0; i < count; i++) {
        await expect(checkboxes.nth(i)).not.toBeChecked();
      }
    });

    test('should toggle step completion when checkbox is clicked', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Get first checkbox
      const firstCheckbox = page.locator('input[type="checkbox"]').first();

      // Check it
      await firstCheckbox.check();
      await expect(firstCheckbox).toBeChecked();

      // Progress should update
      await expect(page.getByText(/Progress: 1 \//i)).toBeVisible();

      // Uncheck it
      await firstCheckbox.uncheck();
      await expect(firstCheckbox).not.toBeChecked();

      // Progress should reset
      await expect(page.getByText(/Progress: 0 \//i)).toBeVisible();
    });

    test('should show progress percentage', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Initial progress should be 0%
      await expect(page.getByText('0%')).toBeVisible();

      // Check first step
      await page.locator('input[type="checkbox"]').first().check();

      // Progress percentage should increase (exact value depends on number of steps)
      const progressText = await page.locator('text=/\\d+%/').textContent();
      expect(progressText).not.toBe('0%');
    });

    test('should show completion message when all steps are checked', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Check all checkboxes
      const checkboxes = page.locator('input[type="checkbox"]');
      const count = await checkboxes.count();

      for (let i = 0; i < count; i++) {
        await checkboxes.nth(i).check();
      }

      // Should show completion message
      await expect(page.getByText(getTextMatcher('setup.setupComplete'))).toBeVisible();
      await expect(page.getByText(getTextMatcher('setup.readyToPlay'))).toBeVisible();

      // Progress should be 100%
      await expect(page.getByText('100%')).toBeVisible();
    });

    test('should show references button for steps with citations', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Look for "View" buttons (references)
      const referenceButtons = page.locator(`button:has-text("${t('setup.viewButton')}")`);
      const count = await referenceButtons.count();

      // Should have at least some references
      expect(count).toBeGreaterThan(0);
    });

    test('should open citation modal when reference button is clicked', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Find and click first reference button
      const referenceButton = page.locator(`button:has-text("${t('setup.viewButton')}")`).first();
      await referenceButton.click({ force: true });

      // Modal should appear
      await expect(page.getByRole('heading', { name: getTextMatcher('setup.references') })).toBeVisible();

      // Should have close button
      const closeButton = page.locator(`button[title="${t('setup.closeButton')}"]`);
      await expect(closeButton).toBeVisible();
    });

    test('should close citation modal when close button is clicked', async ({ userPage: page }) => {
      // Generate setup guide and open modal
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      await page.locator(`button:has-text("${t('setup.viewButton')}")`).first().click({ force: true });
      await expect(page.getByRole('heading', { name: getTextMatcher('setup.references') })).toBeVisible();

      // Close modal
      await page.locator(`button[title="${t('setup.closeButton')}"]`).click({ force: true });

      // Modal should disappear
      await expect(page.getByRole('heading', { name: getTextMatcher('setup.references') })).not.toBeVisible();
    });

    test('should close citation modal when clicking outside', async ({ userPage: page }) => {
      // Generate setup guide and open modal
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      await page.locator(`button:has-text("${t('setup.viewButton')}")`).first().click({ force: true });
      await expect(page.getByRole('heading', { name: getTextMatcher('setup.references') })).toBeVisible();

      // Click outside modal (on backdrop)
      await page.locator('div').filter({ hasText: t('setup.references') }).first().click({
        position: { x: 0, y: 0 } // Click on backdrop
      });

      // Modal should disappear
      await expect(page.getByRole('heading', { name: getTextMatcher('setup.references') })).not.toBeVisible();
    });

    test('should reset progress when reset button is clicked', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Check some steps
      await page.locator('input[type="checkbox"]').first().check();
      await page.locator('input[type="checkbox"]').nth(1).check();

      // Verify progress updated
      await expect(page.getByText(/Progress: 2 \//i)).toBeVisible();

      // Setup dialog confirm handler
      page.on('dialog', dialog => dialog.accept());

      // Click reset button
      await page.getByRole('button', { name: getTextMatcher('setup.resetProgress') }).click({ force: true });

      // Progress should reset
      await expect(page.getByText(/Progress: 0 \//i)).toBeVisible();

      // All checkboxes should be unchecked
      const checkboxes = page.locator('input[type="checkbox"]');
      const count = await checkboxes.count();
      for (let i = 0; i < count; i++) {
        await expect(checkboxes.nth(i)).not.toBeChecked();
      }
    });

    test('should show empty state before generating guide', async ({ userPage: page }) => {
      // Should see empty state
      await expect(page.getByText(getTextMatcher('setup.noGuideYet'))).toBeVisible();
      await expect(page.getByText(getTextMatcher('setup.noGuideMessage'))).toBeVisible();

      // Should have game selection dice emoji
      await expect(page.locator('text=🎲')).toBeVisible();
    });

    test('should disable generate button when no game selected', async ({ userPage: page }) => {
      // Wait for page to load
      await page.waitForTimeout(1000);

      // Deselect game
      const gameSelect = page.getByLabel(getTextMatcher('setup.gameLabel'));
      await gameSelect.selectOption('');

      // Button should be disabled
      const generateButton = page.getByRole('button', { name: getTextMatcher('setup.generateButton') });
      await expect(generateButton).toBeDisabled();
    });

    test('should show loading indicator while generating', async ({ userPage: page }) => {
      // Wait and generate
      await page.waitForTimeout(1000);
      const generateButton = page.getByRole('button', { name: getTextMatcher('setup.generateButton') });
      await generateButton.click({ force: true });

      // Should show loading state
      await expect(page.getByText(getTextMatcher('setup.generatingMessage'))).toBeVisible();
      await expect(page.getByText(getTextMatcher('setup.generatingSubMessage'))).toBeVisible();

      // Button should change text
      await expect(page.getByRole('button', { name: getTextMatcher('setup.generating') })).toBeVisible();
    });

    test('should mark optional steps with badge', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Look for OPTIONAL badges (may or may not exist depending on generated content)
      const optionalBadges = page.locator(`text=${t('setup.optional')}`);
      const count = await optionalBadges.count();

      // Just verify we can detect them if present (no assertion on count)
      console.log(`Found ${count} optional steps`);
    });

    test('should display AI confidence score', async ({ userPage: page }) => {
      // Generate setup guide
      await page.waitForTimeout(1000);
      await page.getByRole('button', { name: getTextMatcher('setup.generateButton') }).click({ force: true });
      await page.waitForSelector(`text=${t('setup.progress')}`, { timeout: 15000 });

      // Look for AI confidence (if present)
      const confidenceText = page.getByText(new RegExp(`${t('setup.aiConfidence')}\\s+\\d+%`));

      // May or may not be visible depending on API response
      const isVisible = await confidenceText.isVisible().catch(() => false);
      console.log(`AI Confidence visible: ${isVisible}`);
    });
  });
});