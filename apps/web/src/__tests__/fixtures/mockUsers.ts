/**
 * Mock User Fixtures for Testing
 *
 * Provides standardized test users for unit and E2E tests
 * Updated for Issue #1611: SSR Auth Migration
 */

import type { AuthUser } from '@/types/auth';

export const mockAdmin: AuthUser = {
  id: 'test-admin-id',
  email: 'admin@meepleai.dev',
  displayName: 'Test Admin',
  role: 'Admin',
};

export const mockEditor: AuthUser = {
  id: 'test-editor-id',
  email: 'editor@meepleai.dev',
  displayName: 'Test Editor',
  role: 'Editor',
};

export const mockUser: AuthUser = {
  id: 'test-user-id',
  email: 'user@meepleai.dev',
  displayName: 'Test User',
  role: 'User',
};

export const mockViewer: AuthUser = {
  id: 'test-viewer-id',
  email: 'viewer@meepleai.dev',
  displayName: 'Test Viewer',
  role: 'Viewer',
};

/**
 * Get mock user by role
 */
export function getMockUserByRole(role: 'Admin' | 'Editor' | 'User' | 'Viewer'): AuthUser {
  switch (role) {
    case 'Admin':
      return mockAdmin;
    case 'Editor':
      return mockEditor;
    case 'User':
      return mockUser;
    case 'Viewer':
      return mockViewer;
  }
}
