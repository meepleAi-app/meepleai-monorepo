import {
  hasMinimumRole,
  isAdmin,
  isSuperAdmin,
  isCreator,
  isEditor,
  ROLE_HIERARCHY,
} from '@/types/permissions';

describe('ROLE_HIERARCHY', () => {
  it('should order roles correctly', () => {
    expect(ROLE_HIERARCHY.user).toBeLessThan(ROLE_HIERARCHY.creator);
    expect(ROLE_HIERARCHY.creator).toBeLessThan(ROLE_HIERARCHY.editor);
    expect(ROLE_HIERARCHY.editor).toBeLessThan(ROLE_HIERARCHY.admin);
    expect(ROLE_HIERARCHY.admin).toBeLessThan(ROLE_HIERARCHY.superadmin);
  });
});

describe('hasMinimumRole', () => {
  it('superadmin has all roles', () => {
    expect(hasMinimumRole('superadmin', 'user')).toBe(true);
    expect(hasMinimumRole('superadmin', 'creator')).toBe(true);
    expect(hasMinimumRole('superadmin', 'editor')).toBe(true);
    expect(hasMinimumRole('superadmin', 'admin')).toBe(true);
    expect(hasMinimumRole('superadmin', 'superadmin')).toBe(true);
  });

  it('user cannot access creator features', () => {
    expect(hasMinimumRole('user', 'creator')).toBe(false);
  });

  it('creator has user access but not editor', () => {
    expect(hasMinimumRole('creator', 'user')).toBe(true);
    expect(hasMinimumRole('creator', 'editor')).toBe(false);
  });

  it('editor has creator access', () => {
    expect(hasMinimumRole('editor', 'creator')).toBe(true);
  });
});

describe('role helpers', () => {
  it('isAdmin checks admin and superadmin', () => {
    expect(isAdmin('admin')).toBe(true);
    expect(isAdmin('superadmin')).toBe(true);
    expect(isAdmin('editor')).toBe(false);
    expect(isAdmin('creator')).toBe(false);
    expect(isAdmin('user')).toBe(false);
  });

  it('isSuperAdmin only matches superadmin', () => {
    expect(isSuperAdmin('superadmin')).toBe(true);
    expect(isSuperAdmin('admin')).toBe(false);
  });

  it('isCreator checks creator and above', () => {
    expect(isCreator('creator')).toBe(true);
    expect(isCreator('editor')).toBe(true);
    expect(isCreator('admin')).toBe(true);
    expect(isCreator('superadmin')).toBe(true);
    expect(isCreator('user')).toBe(false);
  });

  it('isEditor checks editor and above', () => {
    expect(isEditor('editor')).toBe(true);
    expect(isEditor('admin')).toBe(true);
    expect(isEditor('superadmin')).toBe(true);
    expect(isEditor('creator')).toBe(false);
    expect(isEditor('user')).toBe(false);
  });
});
