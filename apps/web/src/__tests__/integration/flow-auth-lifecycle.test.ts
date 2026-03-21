/**
 * Flow Test: Auth Lifecycle
 *
 * Tests the complete authentication lifecycle:
 * 1. Register new account
 * 2. Login with credentials
 * 3. Check session status
 * 4. Enable 2FA → verify code
 * 5. Disable 2FA
 * 6. OAuth flow (Google, Discord, GitHub)
 * 7. Session extension
 * 8. Logout
 * 9. Reject invalid credentials
 * 10. Reject duplicate registration
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { server } from '../mocks/server';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

describe('Flow: Auth Lifecycle', () => {
  beforeEach(() => {
    server.resetHandlers();
  });

  it('should complete full auth lifecycle: register → login → 2FA → logout', async () => {
    // Step 1: Register
    const registerRes = await fetch(`${API_BASE}/api/v1/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email: 'newuser@meepleai.dev',
        password: 'SecurePass123!',
        displayName: 'New Player',
      }),
    });
    expect(registerRes.ok).toBe(true);
    const registerData = await registerRes.json();
    expect(registerData.user.email).toBe('newuser@meepleai.dev');
    expect(registerData.user.displayName).toBe('New Player');
    expect(registerData.user.role).toBe('User');
    expect(registerData.expiresAt).toBeDefined();

    // Step 2: Login with same credentials
    const loginRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email: 'newuser@meepleai.dev',
        password: 'SecurePass123!',
      }),
    });
    expect(loginRes.ok).toBe(true);
    const loginData = await loginRes.json();
    expect(loginData.user).toBeDefined();

    // Step 3: Check session status
    const sessionRes = await fetch(`${API_BASE}/api/v1/auth/session/status`);
    expect(sessionRes.ok).toBe(true);
    const session = await sessionRes.json();
    expect(session.remainingMinutes).toBeGreaterThan(0);
    expect(session.expiresAt).toBeDefined();

    // Step 4: Get current user
    const meRes = await fetch(`${API_BASE}/api/v1/auth/me`);
    expect(meRes.ok).toBe(true);
    const me = await meRes.json();
    expect(me.user).toBeDefined();

    // Step 5: Enable 2FA
    const enable2faRes = await fetch(`${API_BASE}/api/v1/auth/2fa/enable`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({}),
    });
    expect(enable2faRes.ok).toBe(true);
    const twoFa = await enable2faRes.json();
    expect(twoFa.qrCodeUrl).toBeDefined();
    expect(twoFa.secret).toBeDefined();
    expect(twoFa.backupCodes).toBeDefined();
    expect(twoFa.backupCodes.length).toBeGreaterThan(0);

    // Step 6: Verify 2FA code (valid)
    const verify2faRes = await fetch(`${API_BASE}/api/v1/auth/2fa/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: '123456' }),
    });
    expect(verify2faRes.ok).toBe(true);
    const verifyData = await verify2faRes.json();
    expect(verifyData.success).toBe(true);

    // Step 7: Verify 2FA code (invalid)
    const invalidCodeRes = await fetch(`${API_BASE}/api/v1/auth/2fa/verify`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ code: '000000' }),
    });
    expect(invalidCodeRes.status).toBe(401);

    // Step 8: Disable 2FA
    const disable2faRes = await fetch(`${API_BASE}/api/v1/auth/2fa/disable`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({}),
    });
    expect(disable2faRes.ok).toBe(true);

    // Step 9: Extend session
    const extendRes = await fetch(`${API_BASE}/api/v1/auth/session/extend`, {
      method: 'POST',
    });
    expect(extendRes.ok).toBe(true);
    const extended = await extendRes.json();
    expect(extended.remainingMinutes).toBe(30);

    // Step 10: Logout
    const logoutRes = await fetch(`${API_BASE}/api/v1/auth/logout`, {
      method: 'POST',
    });
    expect(logoutRes.ok).toBe(true);
    const logoutData = await logoutRes.json();
    expect(logoutData.success).toBe(true);
  });

  it('should reject login with wrong password', async () => {
    const loginRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email: 'user@meepleai.dev',
        password: 'wrongpassword',
      }),
    });
    expect(loginRes.status).toBe(401);
    const error = await loginRes.json();
    expect(error.error).toContain('Invalid credentials');
  });

  it('should reject duplicate email registration', async () => {
    const registerRes = await fetch(`${API_BASE}/api/v1/auth/register`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        email: 'existing@meepleai.dev',
        password: 'Password123!',
        displayName: 'Existing',
      }),
    });
    expect(registerRes.status).toBe(409);
    const error = await registerRes.json();
    expect(error.error).toContain('already registered');
  });

  it('should return different roles based on login email', async () => {
    // Admin login
    const adminRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'admin@meepleai.dev', password: 'pass' }),
    });
    const admin = await adminRes.json();
    expect(admin.user.role).toBe('Admin');

    // Editor login
    const editorRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'editor@meepleai.dev', password: 'pass' }),
    });
    const editor = await editorRes.json();
    expect(editor.user.role).toBe('Editor');

    // Regular user login
    const userRes = await fetch(`${API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email: 'user@meepleai.dev', password: 'pass' }),
    });
    const user = await userRes.json();
    expect(user.user.role).toBe('User');
  });

  it('should handle OAuth flow initiation', async () => {
    const providers = ['google', 'discord', 'github'];

    for (const provider of providers) {
      const res = await fetch(`${API_BASE}/api/v1/auth/oauth/${provider}`);
      expect(res.ok).toBe(true);
      const data = await res.json();
      expect(data.authUrl).toContain(provider);
    }
  });

  it('should handle OAuth callback', async () => {
    const callbackRes = await fetch(`${API_BASE}/api/v1/auth/oauth/google/callback?code=test-code`);
    expect(callbackRes.ok).toBe(true);
    const data = await callbackRes.json();
    expect(data.user).toBeDefined();
    expect(data.user.role).toBe('User');
  });
});
