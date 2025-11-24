/**
 * Vitest + MSW Validation Test
 *
 * This test validates that Vitest is configured correctly and MSW integration works.
 * Once validated, this file can be deleted.
 *
 * Tests:
 * - Vitest basic functionality
 * - MSW request interception
 * - React Testing Library integration
 * - TypeScript support
 */

import { describe, it, expect } from 'vitest';
import { server } from './mocks/server';

describe('Vitest + MSW Integration Validation', () => {
  it('should run basic Vitest test', () => {
    expect(1 + 1).toBe(2);
    expect(true).toBe(true);
  });

  it('should have MSW server available', () => {
    expect(server).toBeDefined();
    expect(server.listen).toBeInstanceOf(Function);
    expect(server.resetHandlers).toBeInstanceOf(Function);
    expect(server.close).toBeInstanceOf(Function);
  });

  it('should intercept fetch requests with MSW', async () => {
    // This should be intercepted by auth.handlers.ts
    const response = await fetch('http://localhost:8080/api/v1/auth/me');
    const data = await response.json();

    expect(response.ok).toBe(true);
    expect(data).toHaveProperty('user');
    expect(data.user).toHaveProperty('email');
  });

  it('should handle POST requests with MSW', async () => {
    // This should be intercepted by auth.handlers.ts
    const response = await fetch('http://localhost:8080/api/v1/auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        email: 'test@meepleai.dev',
        password: 'validpassword',
      }),
    });

    const data = await response.json();

    expect(response.ok).toBe(true);
    expect(data).toHaveProperty('user');
    expect(data.user.email).toBe('user@meepleai.dev'); // Default mock response
  });

  it('should handle error responses from MSW', async () => {
    // Invalid credentials should return 401
    const response = await fetch('http://localhost:8080/api/v1/auth/login', {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        email: 'test@meepleai.dev',
        password: 'wrongpassword',
      }),
    });

    const data = await response.json();

    expect(response.ok).toBe(false);
    expect(response.status).toBe(401);
    expect(data).toHaveProperty('error');
  });

  it('should support TypeScript', () => {
    const testObject: { name: string; value: number } = {
      name: 'test',
      value: 42,
    };

    expect(testObject.name).toBe('test');
    expect(testObject.value).toBe(42);
  });
});
