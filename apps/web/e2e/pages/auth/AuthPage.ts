import { Locator } from '@playwright/test';
import { BasePage } from '../base/BasePage';
import {
  IAuthPage,
  LoginCredentials,
  RegistrationData,
  TwoFactorData,
} from '../../types/pom-interfaces';

/**
 * AuthPage - Authentication page interactions
 *
 * Handles all authentication flows:
 * - Login (email/password, remember me)
 * - Registration
 * - OAuth (Google, Discord, GitHub)
 * - Two-Factor Authentication (TOTP, backup codes)
 * - Password reset
 * - Logout
 *
 * Usage:
 *   const authPage = new AuthPage(page);
 *   await authPage.goto();
 *   await authPage.loginAndWait({ email: '...', password: '...' });
 */
export class AuthPage extends BasePage implements IAuthPage {
  // ========================================================================
  // Locators
  // ========================================================================

  /**
   * Login form container (contains "Accesso" or "Login" heading)
   */
  private get loginForm(): Locator {
    return this.page
      .locator('form')
      .filter({ has: this.page.getByRole('heading', { name: /accesso|login/i }) });
  }

  /**
   * Registration form container (contains "Registrazione" or "Register" heading)
   */
  private get registerForm(): Locator {
    return this.page
      .locator('form')
      .filter({ has: this.page.getByRole('heading', { name: /registrazione|register/i }) });
  }

  // OAuth buttons
  private get googleOAuthButton(): Locator {
    return this.page.getByRole('button', { name: /google/i });
  }

  private get discordOAuthButton(): Locator {
    return this.page.getByRole('button', { name: /discord/i });
  }

  private get githubOAuthButton(): Locator {
    return this.page.getByRole('button', { name: /github/i });
  }

  // 2FA elements
  private get totpInput(): Locator {
    return this.page.getByLabel(/totp|authenticator code|2fa code/i);
  }

  private get backupCodeInput(): Locator {
    return this.page.getByLabel(/backup code/i);
  }

  private get verify2FAButton(): Locator {
    return this.page.getByRole('button', { name: /verify|verifica/i });
  }

  // Success/Error messages
  private get loginSuccessMessage(): Locator {
    return this.page.getByText(/accesso eseguito|logged in successfully/i);
  }

  private get registerSuccessMessage(): Locator {
    return this.page.getByText(/registrazione completata|registration successful/i);
  }

  private get logoutSuccessMessage(): Locator {
    return this.page.getByText(/logged out|uscito con successo/i);
  }

  // ========================================================================
  // Navigation
  // ========================================================================

  /**
   * Navigate to authentication page (login page)
   */
  async goto(): Promise<void> {
    await this.page.goto('/login');
    await this.waitForLoad();
  }

  // ========================================================================
  // Login Actions
  // ========================================================================

  /**
   * Fill and submit login form
   * @param credentials - Email, password, and optional rememberMe flag
   */
  async login(credentials: LoginCredentials): Promise<void> {
    await this.fill(this.loginForm.getByLabel(/email/i), credentials.email);
    await this.fill(this.loginForm.getByLabel(/password/i), credentials.password);

    if (credentials.rememberMe) {
      await this.loginForm.getByLabel(/remember me/i).check();
    }

    await this.click(this.loginForm.getByRole('button', { name: /entra|login/i }));
  }

  /**
   * Wait for login success message to appear
   */
  async waitForLoginSuccess(): Promise<void> {
    await this.waitForElement(this.loginSuccessMessage);
  }

  /**
   * Login and wait for success message (composite method)
   * @param credentials - Email and password
   */
  async loginAndWait(credentials: LoginCredentials): Promise<void> {
    await this.login(credentials);
    await this.waitForLoginSuccess();
  }

  // ========================================================================
  // Registration Actions
  // ========================================================================

  /**
   * Fill and submit registration form
   * @param data - Email, password, and display name
   */
  async register(data: RegistrationData): Promise<void> {
    await this.fill(this.registerForm.getByLabel(/email/i), data.email);
    await this.fill(this.registerForm.getByLabel(/password/i), data.password);
    await this.fill(this.registerForm.getByLabel(/display name|nome/i), data.displayName);

    await this.click(this.registerForm.getByRole('button', { name: /registra|register/i }));
  }

  /**
   * Wait for registration success message
   */
  async waitForRegistrationSuccess(): Promise<void> {
    await this.waitForElement(this.registerSuccessMessage);
  }

  // ========================================================================
  // OAuth Actions
  // ========================================================================

  /**
   * Click OAuth provider button
   * @param provider - OAuth provider (google, discord, github)
   *
   * Note: This only clicks the button. OAuth callback handling must be
   * mocked separately in tests.
   */
  async clickOAuthButton(provider: 'google' | 'discord' | 'github'): Promise<void> {
    const buttonMap = {
      google: this.googleOAuthButton,
      discord: this.discordOAuthButton,
      github: this.githubOAuthButton,
    };

    await this.click(buttonMap[provider]);
  }

  // ========================================================================
  // Two-Factor Authentication Actions
  // ========================================================================

  /**
   * Enter and submit 2FA code (TOTP or backup code)
   * @param data - Either TOTP code or backup code
   */
  async verify2FA(data: TwoFactorData): Promise<void> {
    if (data.code) {
      // TOTP code (6 digits)
      await this.fill(this.totpInput, data.code);
    } else if (data.backupCode) {
      // Backup code (XXXX-XXXX format)
      await this.fill(this.backupCodeInput, data.backupCode);
    }

    await this.click(this.verify2FAButton);
  }

  /**
   * Wait for 2FA prompt to appear (after successful password login)
   */
  async waitFor2FAPrompt(): Promise<void> {
    await this.waitForElement(this.page.getByRole('heading', { name: /two.factor|2fa/i }));
  }

  // ========================================================================
  // Logout Actions
  // ========================================================================

  /**
   * Click logout button
   */
  async logout(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /logout|esci/i }));
  }

  /**
   * Wait for logout success message
   */
  async waitForLogoutSuccess(): Promise<void> {
    await this.waitForElement(this.logoutSuccessMessage);
  }

  // ========================================================================
  // Password Reset
  // ========================================================================

  /**
   * Click "Forgot Password" link (navigates to password reset page)
   */
  async clickForgotPassword(): Promise<void> {
    await this.click(this.page.getByRole('link', { name: /forgot password/i }));
  }

  // ========================================================================
  // Assertions
  // ========================================================================

  /**
   * Assert login form is visible
   */
  async assertLoginFormVisible(): Promise<void> {
    await this.waitForElement(this.loginForm);
  }

  /**
   * Assert registration form is visible
   */
  async assertRegisterFormVisible(): Promise<void> {
    await this.waitForElement(this.registerForm);
  }

  /**
   * Assert validation error message is visible
   * @param message - Error message text or pattern
   */
  async assertValidationError(message: string | RegExp): Promise<void> {
    await this.waitForElement(this.page.getByText(message));
  }

  /**
   * Assert user is logged in (check for user info display)
   * @param email - Expected user email
   */
  async assertLoggedIn(email: string): Promise<void> {
    await this.waitForElement(this.page.getByText(new RegExp(`Email:\\s*${email}`)));
  }

  /**
   * Assert OAuth button is visible
   * @param provider - OAuth provider
   */
  async assertOAuthButtonVisible(provider: 'google' | 'discord' | 'github'): Promise<void> {
    const buttonMap = {
      google: this.googleOAuthButton,
      discord: this.discordOAuthButton,
      github: this.githubOAuthButton,
    };
    await this.waitForElement(buttonMap[provider]);
  }

  // ========================================================================
  // 2FA Settings Page Actions (AUTH-07)
  // ========================================================================

  /**
   * Navigate to settings page for 2FA management
   */
  async gotoSettings(): Promise<void> {
    await this.page.goto('/settings');
    await this.waitForLoad();
  }

  /**
   * Get 2FA status section
   */
  private get twoFactorSection(): Locator {
    return this.page.getByRole('heading', { name: /two.factor authentication/i }).locator('..');
  }

  /**
   * Click "Enable Two-Factor Authentication" button
   */
  async clickEnableTwoFactor(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /enable two.factor authentication/i }));
  }

  /**
   * Get QR code element
   */
  private get qrCode(): Locator {
    return this.page
      .locator('svg')
      .filter({ has: this.page.locator('path') })
      .first();
  }

  /**
   * Check if QR code is visible
   */
  async isQRCodeVisible(): Promise<boolean> {
    return await this.isVisible(this.qrCode);
  }

  /**
   * Get manual entry secret from settings page
   */
  async getManualEntrySecret(): Promise<string> {
    // Click "Can't scan QR code? Enter manually" details element
    await this.click(this.page.locator('details > summary'));
    const secretCode = this.page.locator('code').filter({ hasText: /^[A-Z0-9]{32}$/ });
    return (await secretCode.textContent()) || '';
  }

  /**
   * Get backup codes from settings page
   */
  async getBackupCodes(): Promise<string[]> {
    const codeElements = this.page.locator('.bg-white.px-3.py-2.rounded.font-mono');
    const count = await codeElements.count();
    const codes: string[] = [];
    for (let i = 0; i < count; i++) {
      const text = await codeElements.nth(i).textContent();
      if (text) codes.push(text.trim());
    }
    return codes;
  }

  /**
   * Click "Download Codes" button
   */
  async clickDownloadBackupCodes(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /download codes/i }));
  }

  /**
   * Click "I've Saved My Codes" button
   */
  async clickSavedBackupCodes(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /i've saved my codes/i }));
  }

  /**
   * Enter TOTP verification code in settings page
   */
  async enterVerificationCode(code: string): Promise<void> {
    await this.fill(this.page.getByPlaceholder('000000'), code);
  }

  /**
   * Click "Verify & Enable" button
   */
  async clickVerifyAndEnable(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /verify.*enable/i }));
  }

  /**
   * Complete 2FA setup flow (setup → save codes → enter code → enable)
   */
  async completeTwoFactorSetup(verificationCode: string): Promise<void> {
    await this.clickEnableTwoFactor();
    await this.waitForElement(this.qrCode);
    await this.clickSavedBackupCodes();
    await this.enterVerificationCode(verificationCode);
    await this.clickVerifyAndEnable();
  }

  /**
   * Check if 2FA is enabled (green status message visible)
   */
  async isTwoFactorEnabled(): Promise<boolean> {
    const enabledMessage = this.page.getByText(/two.factor authentication is enabled/i);
    return await this.isVisible(enabledMessage);
  }

  /**
   * Get backup codes remaining count from settings page
   */
  async getBackupCodesCount(): Promise<number> {
    const countText = await this.page.getByText(/backup codes remaining:\s*\d+/i).textContent();
    const match = countText?.match(/(\d+)/);
    return match ? parseInt(match[1], 10) : 0;
  }

  /**
   * Fill disable 2FA form
   */
  async fillDisableTwoFactorForm(password: string, code: string): Promise<void> {
    // Find password input in disable section
    await this.fill(this.page.getByLabel(/current password/i), password);
    // Find TOTP/backup code input in disable section
    await this.fill(this.page.getByLabel(/totp code or backup code/i), code);
  }

  /**
   * Click "Disable 2FA" button
   */
  async clickDisableTwoFactor(): Promise<void> {
    // Handle browser confirmation dialog
    this.page.once('dialog', dialog => dialog.accept());
    await this.click(this.page.getByRole('button', { name: /disable 2fa/i }));
  }

  /**
   * Complete 2FA disable flow
   */
  async disableTwoFactor(password: string, code: string): Promise<void> {
    await this.fillDisableTwoFactorForm(password, code);
    await this.clickDisableTwoFactor();
  }

  /**
   * Assert 2FA status message
   */
  async assert2FAEnabled(): Promise<void> {
    await this.waitForElement(this.page.getByText(/two.factor authentication is enabled/i));
  }

  /**
   * Assert 2FA disabled (enable button visible)
   */
  async assert2FADisabled(): Promise<void> {
    await this.waitForElement(
      this.page.getByRole('button', { name: /enable two.factor authentication/i })
    );
  }

  /**
   * Assert error message is visible
   */
  async assertErrorMessage(message: string | RegExp): Promise<void> {
    await this.waitForElement(this.page.getByText(message));
  }

  /**
   * Click "Cancel Setup" button
   */
  async clickCancelSetup(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /cancel setup/i }));
  }

  // ========================================================================
  // Password Reset Actions (AUTH-04)
  // ========================================================================

  /**
   * Navigate to password reset page (request mode)
   */
  async gotoPasswordReset(): Promise<void> {
    await this.page.goto('/reset-password');
    await this.waitForLoad();
  }

  /**
   * Navigate to password reset page with token (reset mode)
   */
  async gotoPasswordResetWithToken(token: string): Promise<void> {
    await this.page.goto(`/reset-password?token=${encodeURIComponent(token)}`);
    await this.waitForLoad();
  }

  /**
   * Fill and submit password reset request form
   * @param email - Email address to send reset link
   */
  async requestPasswordReset(email: string): Promise<void> {
    await this.fill(this.page.getByLabel(/email address/i), email);
    await this.click(this.page.getByRole('button', { name: /send reset instructions/i }));
  }

  /**
   * Fill and submit new password form
   * @param newPassword - New password
   * @param confirmPassword - Password confirmation
   */
  async submitNewPassword(newPassword: string, confirmPassword: string): Promise<void> {
    await this.fill(this.page.getByLabel(/^new password$/i), newPassword);
    await this.fill(this.page.getByLabel(/confirm password/i), confirmPassword);
    await this.click(this.page.getByRole('button', { name: /reset password/i }));
  }

  /**
   * Wait for password reset request success message
   */
  async waitForResetRequestSuccess(): Promise<void> {
    await this.waitForElement(this.page.getByText(/check your email/i));
  }

  /**
   * Wait for password reset success message
   */
  async waitForResetSuccess(): Promise<void> {
    await this.waitForElement(this.page.getByText(/password reset successful/i));
  }

  /**
   * Wait for invalid token message
   */
  async waitForInvalidTokenMessage(): Promise<void> {
    await this.waitForElement(this.page.getByRole('heading', { name: /invalid or expired link/i }));
  }

  /**
   * Assert password validation error
   * @param message - Expected error message
   */
  async assertPasswordValidationError(message: string | RegExp): Promise<void> {
    await this.waitForElement(this.page.getByText(message));
  }

  /**
   * Assert password requirement not met
   * @param requirement - Requirement text (e.g., "At least 8 characters")
   */
  async assertPasswordRequirementNotMet(requirement: string): Promise<boolean> {
    const requirementElement = this.page.getByText(requirement);
    const classes = await requirementElement.getAttribute('class');
    return classes?.includes('text-slate-500') || false;
  }

  /**
   * Assert password requirement met
   * @param requirement - Requirement text (e.g., "At least 8 characters")
   */
  async assertPasswordRequirementMet(requirement: string): Promise<boolean> {
    const requirementElement = this.page.getByText(requirement);
    const classes = await requirementElement.getAttribute('class');
    return classes?.includes('text-green-400') || false;
  }

  /**
   * Get password strength indicator text
   */
  async getPasswordStrength(): Promise<string> {
    const strengthElement = this.page.getByText(/password strength:/i).locator('..');
    const strengthText = await strengthElement.getByText(/weak|medium|strong/i).textContent();
    return strengthText?.toLowerCase().trim() || '';
  }

  /**
   * Assert reset button is disabled
   */
  async assertResetButtonDisabled(): Promise<void> {
    const button = this.page.getByRole('button', { name: /reset password/i });
    await this.waitForElement(button);
    const isDisabled = await button.isDisabled();
    if (!isDisabled) {
      throw new Error('Reset button should be disabled');
    }
  }

  /**
   * Assert reset button is enabled
   */
  async assertResetButtonEnabled(): Promise<void> {
    const button = this.page.getByRole('button', { name: /reset password/i });
    await this.waitForElement(button);
    const isDisabled = await button.isDisabled();
    if (isDisabled) {
      throw new Error('Reset button should be enabled');
    }
  }

  // ========================================================================
  // OAuth Profile Management (AUTH-06 Advanced)
  // ========================================================================

  /**
   * Navigate to settings page for OAuth account management
   * (Issue #1672: /profile deprecated, now redirects to /settings)
   */
  async gotoProfile(): Promise<void> {
    await this.page.goto('/settings');
    await this.waitForLoad();
  }

  /**
   * Get OAuth provider button (link/unlink)
   * @param provider - OAuth provider (google, discord, github)
   */
  private getProviderButton(provider: 'google' | 'discord' | 'github'): Locator {
    return this.page.getByRole('button', { name: new RegExp(provider, 'i') });
  }

  /**
   * Click Link OAuth provider button
   * @param provider - OAuth provider to link
   */
  async clickLinkProvider(provider: 'google' | 'discord' | 'github'): Promise<void> {
    const button = this.getProviderButton(provider).filter({ hasText: /link/i });
    await this.click(button);
  }

  /**
   * Click Unlink OAuth provider button (handles confirmation dialog)
   * @param provider - OAuth provider to unlink
   */
  async clickUnlinkProvider(provider: 'google' | 'discord' | 'github'): Promise<void> {
    // Handle confirmation dialog
    this.page.once('dialog', dialog => dialog.accept());
    const button = this.getProviderButton(provider).filter({ hasText: /unlink/i });
    await this.click(button);
  }

  /**
   * Get all linked provider cards
   */
  private get linkedProviderCards(): Locator {
    return this.page.locator('.bg-white, .dark\\:bg-slate-800').filter({ hasText: /linked/i });
  }

  /**
   * Check if provider is linked
   * @param provider - OAuth provider
   */
  async isProviderLinked(provider: 'google' | 'discord' | 'github'): Promise<boolean> {
    const card = this.page
      .locator('.bg-white, .dark\\:bg-slate-800')
      .filter({
        hasText: new RegExp(provider, 'i'),
      })
      .filter({ hasText: /linked/i });
    return await this.isVisible(card);
  }

  /**
   * Get count of linked providers
   */
  async getLinkedProvidersCount(): Promise<number> {
    const cards = await this.linkedProviderCards.count();
    return cards;
  }

  /**
   * Assert provider is linked
   * @param provider - OAuth provider
   */
  async assertProviderLinked(provider: 'google' | 'discord' | 'github'): Promise<void> {
    const isLinked = await this.isProviderLinked(provider);
    if (!isLinked) {
      throw new Error(`Expected ${provider} to be linked`);
    }
  }

  /**
   * Assert provider is not linked
   * @param provider - OAuth provider
   */
  async assertProviderNotLinked(provider: 'google' | 'discord' | 'github'): Promise<void> {
    const isLinked = await this.isProviderLinked(provider);
    if (isLinked) {
      throw new Error(`Expected ${provider} to NOT be linked`);
    }
  }

  /**
   * Assert unlink button is disabled (last auth method protection)
   * @param provider - OAuth provider
   */
  async assertUnlinkButtonDisabled(provider: 'google' | 'discord' | 'github'): Promise<void> {
    const button = this.getProviderButton(provider).filter({ hasText: /unlink/i });
    await this.waitForElement(button);
    const isDisabled = await button.isDisabled();
    if (!isDisabled) {
      throw new Error(`Expected unlink button for ${provider} to be disabled`);
    }
  }

  /**
   * Wait for OAuth callback success message
   */
  async waitForOAuthCallbackSuccess(): Promise<void> {
    await this.waitForElement(this.page.getByText(/successfully linked|account linked/i));
  }

  /**
   * Wait for OAuth callback error message
   */
  async waitForOAuthCallbackError(): Promise<void> {
    await this.waitForElement(this.page.getByText(/failed to link|error linking|already linked/i));
  }

  /**
   * Assert session is persisted after OAuth login (user info visible)
   */
  async assertOAuthSessionPersisted(email: string): Promise<void> {
    await this.assertLoggedIn(email);
  }
}
