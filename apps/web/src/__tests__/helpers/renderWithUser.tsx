/**
 * Test Helper: Render with User Context
 *
 * Issue #1611: SSR Auth Migration
 * Provides default user prop for pages migrated to SSR auth
 */

import { render, RenderOptions } from '@testing-library/react';
import { ReactElement } from 'react';
import type { AuthUser } from '@/types/auth';
import { mockAdmin, mockEditor } from '../fixtures/mockUsers';

/**
 * Default props for pages requiring SSR auth user prop
 */
export interface WithUserProps {
  user: AuthUser;
}

/**
 * Render a component with default admin user
 */
export function renderWithAdmin(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'>
) {
  return render(ui, options);
}

/**
 * Render a component with default editor user
 */
export function renderWithEditor(
  ui: ReactElement,
  options?: Omit<RenderOptions, 'wrapper'>
) {
  return render(ui, options);
}

/**
 * Render a component with custom user
 */
export function renderWithUser(
  ui: ReactElement,
  user: AuthUser,
  options?: Omit<RenderOptions, 'wrapper'>
) {
  return render(ui, options);
}

/**
 * Get default user props for testing
 */
export function getDefaultUserProps(role: 'admin' | 'editor' = 'editor'): WithUserProps {
  return {
    user: role === 'admin' ? mockAdmin : mockEditor,
  };
}
