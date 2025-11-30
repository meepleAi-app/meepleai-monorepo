/**
 * MSW handlers for authentication endpoints
 *
 * Covers: /api/v1/auth/* routes
 * - Login, Register, Logout
 * - Session status, extension
 * - 2FA operations
 * - OAuth endpoints
 */

import { http, HttpResponse } from 'msw';
import {
  createMockAuthResponse,
  createMockSessionStatus,
  mockAdminAuth,
  mockEditorAuth,
  mockUserAuth,
} from '../../fixtures/common-fixtures';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

export const authHandlers = [
  // POST /api/v1/auth/register - User registration
  http.post(`${API_BASE}/api/v1/auth/register`, async ({ request }) => {
    const body = await request.json() as { email: string; password: string; displayName?: string };

    // Simulate email conflict
    if (body.email === 'existing@meepleai.dev') {
      return HttpResponse.json(
        { error: 'Email already registered' },
        { status: 409 }
      );
    }

    // Success response
    const authResponse = createMockAuthResponse({
      email: body.email,
      displayName: body.displayName || 'New User',
      role: 'User',
    });

    return HttpResponse.json(authResponse, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/auth/login - User login
  http.post(`${API_BASE}/api/v1/auth/login`, async ({ request }) => {
    const body = await request.json() as { email: string; password: string };

    // Simulate invalid credentials
    if (body.password === 'wrongpassword') {
      return HttpResponse.json(
        { error: 'Invalid credentials' },
        { status: 401 }
      );
    }

    // Return auth response based on email
    let authResponse;
    if (body.email.includes('admin')) {
      authResponse = mockAdminAuth();
    } else if (body.email.includes('editor')) {
      authResponse = mockEditorAuth();
    } else {
      authResponse = mockUserAuth();
    }

    return HttpResponse.json(authResponse, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/auth/logout - User logout
  http.post(`${API_BASE}/api/v1/auth/logout`, () => {
    return HttpResponse.json(
      { success: true },
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // GET /api/v1/auth/me - Get current user
  http.get(`${API_BASE}/api/v1/auth/me`, () => {
    return HttpResponse.json(mockUserAuth(), {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/auth/session/status - Session status
  http.get(`${API_BASE}/api/v1/auth/session/status`, () => {
    return HttpResponse.json(createMockSessionStatus(), {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/auth/session/extend - Extend session
  http.post(`${API_BASE}/api/v1/auth/session/extend`, () => {
    return HttpResponse.json(
      createMockSessionStatus({ remainingMinutes: 30 }),
      {
        headers: {
          'X-Correlation-Id': `test-correlation-${Date.now()}`,
        },
      }
    );
  }),

  // POST /api/v1/auth/2fa/enable - Enable 2FA
  http.post(`${API_BASE}/api/v1/auth/2fa/enable`, () => {
    return HttpResponse.json({
      qrCodeUrl: 'data:image/png;base64,test',
      secret: 'JBSWY3DPEHPK3PXP',
      backupCodes: ['code1', 'code2', 'code3'],
    });
  }),

  // POST /api/v1/auth/2fa/verify - Verify 2FA code
  http.post(`${API_BASE}/api/v1/auth/2fa/verify`, async ({ request }) => {
    const body = await request.json() as { code: string };

    if (body.code === '123456') {
      return HttpResponse.json({ success: true });
    }

    return HttpResponse.json(
      { error: 'Invalid 2FA code' },
      { status: 401 }
    );
  }),

  // POST /api/v1/auth/2fa/disable - Disable 2FA
  http.post(`${API_BASE}/api/v1/auth/2fa/disable`, () => {
    return HttpResponse.json({ success: true });
  }),

  // OAuth endpoints
  http.get(`${API_BASE}/api/v1/auth/oauth/:provider`, ({ params }) => {
    const { provider } = params;
    return HttpResponse.json({
      authUrl: `https://oauth.${provider}.com/authorize?client_id=test`,
    });
  }),

  http.get(`${API_BASE}/api/v1/auth/oauth/:provider/callback`, () => {
    return HttpResponse.json(mockUserAuth());
  }),

  // Error simulation handlers (for testing error scenarios)
  http.post(`${API_BASE}/api/v1/auth/login-error-500`, () => {
    return HttpResponse.json(
      { error: 'Internal server error' },
      { status: 500 }
    );
  }),

  http.get(`${API_BASE}/api/v1/auth/me-unauthorized`, () => {
    return HttpResponse.json(
      { error: 'Unauthorized' },
      { status: 401 }
    );
  }),
];
