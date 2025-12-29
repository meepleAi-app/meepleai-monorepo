/**
 * Authentication utilities for k6 tests
 *
 * Handles user registration, login, and session management.
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { getHeaders } from './common.js';

/**
 * Register a new user
 */
export function register(baseUrl, email, password, displayName = null) {
  const payload = JSON.stringify({
    email: email,
    password: password,
    displayName: displayName || email.split('@')[0],
    role: 'User',
  });

  const response = http.post(
    `${baseUrl}/api/v1/auth/register`,
    payload,
    { headers: getHeaders() }
  );

  const success = check(response, {
    'registration successful': (r) => r.status === 200,
    'has session cookie': (r) => r.headers['Set-Cookie'] !== undefined,
  });

  if (!success) {
    console.error(`Registration failed: ${response.status} - ${response.body}`);
    return null;
  }

  // Extract session token from Set-Cookie header
  const setCookie = response.headers['Set-Cookie'];
  const sessionToken = extractSessionToken(setCookie);

  return {
    user: JSON.parse(response.body).user,
    sessionToken: sessionToken,
  };
}

/**
 * Login user and get session token
 */
export function login(baseUrl, email, password) {
  const payload = JSON.stringify({
    email: email,
    password: password,
  });

  const response = http.post(
    `${baseUrl}/api/v1/auth/login`,
    payload,
    { headers: getHeaders() }
  );

  const success = check(response, {
    'login successful': (r) => r.status === 200,
    'has session cookie': (r) => r.headers['Set-Cookie'] !== undefined,
  });

  if (!success) {
    console.error(`Login failed: ${response.status} - ${response.body}`);
    return null;
  }

  // Extract session token from Set-Cookie header
  const setCookie = response.headers['Set-Cookie'];
  const sessionToken = extractSessionToken(setCookie);

  return {
    user: JSON.parse(response.body).user,
    sessionToken: sessionToken,
  };
}

/**
 * Logout user
 */
export function logout(baseUrl, sessionToken) {
  const response = http.post(
    `${baseUrl}/api/v1/auth/logout`,
    null,
    { headers: getHeaders(sessionToken) }
  );

  return check(response, {
    'logout successful': (r) => r.status === 200,
  });
}

/**
 * Get user sessions
 */
export function getSessions(baseUrl, sessionToken) {
  const response = http.get(
    `${baseUrl}/api/v1/users/me/sessions`,
    { headers: getHeaders(sessionToken) }
  );

  const success = check(response, {
    'sessions retrieved': (r) => r.status === 200,
  });

  return success ? JSON.parse(response.body) : null;
}

/**
 * Extract session token from Set-Cookie header
 */
function extractSessionToken(setCookieHeader) {
  if (!setCookieHeader) return null;

  // Handle array of cookies or single cookie string
  const cookies = Array.isArray(setCookieHeader) ? setCookieHeader : [setCookieHeader];

  for (const cookie of cookies) {
    const match = cookie.match(/meepleai_session=([^;]+)/);
    if (match) {
      return match[1];
    }
  }

  return null;
}

/**
 * Setup function: create test user and login
 */
export function setupTestUser(baseUrl, email, password, displayName = null) {
  console.log(`Setting up test user: ${email}`);

  // Try to login first (user might already exist)
  let auth = login(baseUrl, email, password);

  // If login fails, register new user
  if (!auth) {
    console.log('User not found, registering...');
    auth = register(baseUrl, email, password, displayName);
  }

  if (!auth) {
    throw new Error(`Failed to setup test user: ${email}`);
  }

  console.log(`Test user ready: ${email}`);
  return auth;
}

/**
 * Teardown function: logout test user
 */
export function teardownTestUser(baseUrl, sessionToken) {
  if (sessionToken) {
    logout(baseUrl, sessionToken);
  }
}
