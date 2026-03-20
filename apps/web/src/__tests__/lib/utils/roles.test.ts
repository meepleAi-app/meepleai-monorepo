import { describe, expect, it } from 'vitest';

import { isAdminRole } from '@/lib/utils/roles';

describe('isAdminRole', () => {
  it('returns true for "Admin"', () => {
    expect(isAdminRole('Admin')).toBe(true);
  });

  it('returns true for "admin" (lowercase)', () => {
    expect(isAdminRole('admin')).toBe(true);
  });

  it('returns true for "superadmin"', () => {
    expect(isAdminRole('superadmin')).toBe(true);
  });

  it('returns true for "SuperAdmin" (mixed case)', () => {
    expect(isAdminRole('SuperAdmin')).toBe(true);
  });

  it('returns false for "User"', () => {
    expect(isAdminRole('User')).toBe(false);
  });

  it('returns false for "Editor"', () => {
    expect(isAdminRole('Editor')).toBe(false);
  });

  it('returns false for undefined', () => {
    expect(isAdminRole(undefined)).toBe(false);
  });

  it('returns false for null', () => {
    expect(isAdminRole(null)).toBe(false);
  });

  it('returns false for empty string', () => {
    expect(isAdminRole('')).toBe(false);
  });
});
