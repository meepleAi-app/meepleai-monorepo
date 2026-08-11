import { describe, expect, it } from 'vitest';

import { canUseEditor, isAdminRole } from '@/lib/utils/roles';

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

describe('canUseEditor', () => {
  // Issue #2845 / finding #GG: the RuleSpec editor + agent-proposals guards
  // used a flat, case-sensitive compare (role !== 'Admin' && role !== 'Editor').
  // The backend normalizes roles to lowercase, so 'superadmin' AND 'admin' were
  // both denied. canUseEditor must be case-insensitive and grant superadmin.
  it('returns true for "superadmin" (was the reported bug)', () => {
    expect(canUseEditor('superadmin')).toBe(true);
  });

  it('returns true for "SuperAdmin" (mixed case)', () => {
    expect(canUseEditor('SuperAdmin')).toBe(true);
  });

  it('returns true for "admin" (lowercase from backend)', () => {
    expect(canUseEditor('admin')).toBe(true);
  });

  it('returns true for "Admin" (PascalCase)', () => {
    expect(canUseEditor('Admin')).toBe(true);
  });

  it('returns true for "editor"', () => {
    expect(canUseEditor('editor')).toBe(true);
  });

  it('returns true for "Editor" (PascalCase)', () => {
    expect(canUseEditor('Editor')).toBe(true);
  });

  it('returns false for "user"', () => {
    expect(canUseEditor('user')).toBe(false);
  });

  it('returns false for "creator" (not an editor role)', () => {
    expect(canUseEditor('creator')).toBe(false);
  });

  it('returns false for null', () => {
    expect(canUseEditor(null)).toBe(false);
  });

  it('returns false for undefined', () => {
    expect(canUseEditor(undefined)).toBe(false);
  });

  it('returns false for empty string', () => {
    expect(canUseEditor('')).toBe(false);
  });
});
