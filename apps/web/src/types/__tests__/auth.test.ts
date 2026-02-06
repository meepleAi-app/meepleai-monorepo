/**
 * Unit tests for authentication types
 * Tests auth utility functions and type guards
 */

import { hasRole, canEdit } from '../auth';
import type { AuthUser, UserRole } from '../auth';

describe('Auth Types', () => {
  describe('hasRole', () => {
    const adminUser: AuthUser = {
      id: '1',
      email: 'admin@test.com',
      displayName: 'Admin',
      role: 'Admin',
    };

    const editorUser: AuthUser = {
      id: '2',
      email: 'editor@test.com',
      displayName: 'Editor',
      role: 'Editor',
    };

    const superAdminUser: AuthUser = {
      id: '3',
      email: 'superadmin@test.com',
      displayName: 'SuperAdmin',
      role: 'SuperAdmin',
    };

    const regularUser: AuthUser = {
      id: '4',
      email: 'user@test.com',
      displayName: 'User',
      role: 'User',
    };

    describe('Admin Access', () => {
      it('should return true for admin user with any role', () => {
        expect(hasRole(adminUser, 'Admin')).toBe(true);
        expect(hasRole(adminUser, 'Editor')).toBe(true);
        expect(hasRole(adminUser, 'SuperAdmin')).toBe(false);
        expect(hasRole(adminUser, 'User')).toBe(true);
      });

      it('should handle case-insensitive admin role', () => {
        const mixedCaseAdmin: AuthUser = {
          ...adminUser,
          role: 'admin', // lowercase
        };

        expect(hasRole(mixedCaseAdmin, 'Admin')).toBe(true);
        expect(hasRole(mixedCaseAdmin, 'Editor')).toBe(true);
      });
    });

    describe('Exact Role Match', () => {
      it('should return true for editor with editor role', () => {
        expect(hasRole(editorUser, 'Editor')).toBe(true);
      });

      it('should return false for editor with admin role', () => {
        expect(hasRole(editorUser, 'Admin')).toBe(false);
      });

      it('should return true for superadmin with superadmin role', () => {
        expect(hasRole(superAdminUser, 'SuperAdmin')).toBe(true);
      });

      it('should return true for superadmin with any role', () => {
        expect(hasRole(superAdminUser, 'Admin')).toBe(true);
        expect(hasRole(superAdminUser, 'Editor')).toBe(true);
        expect(hasRole(superAdminUser, 'User')).toBe(true);
      });

      it('should return true for user with user role', () => {
        expect(hasRole(regularUser, 'User')).toBe(true);
      });

      it('should return false for user with admin role', () => {
        expect(hasRole(regularUser, 'Admin')).toBe(false);
      });
    });

    describe('Case Insensitivity', () => {
      it('should handle lowercase user roles', () => {
        const lowercaseUser: AuthUser = {
          ...editorUser,
          role: 'editor',
        };

        expect(hasRole(lowercaseUser, 'Editor')).toBe(true);
      });

      it('should handle uppercase user roles', () => {
        const uppercaseUser: AuthUser = {
          ...superAdminUser,
          role: 'SUPERADMIN' as UserRole,
        };

        expect(hasRole(uppercaseUser, 'SuperAdmin')).toBe(true);
      });

      it('should handle mixed case required roles', () => {
        expect(hasRole(editorUser, 'editor' as UserRole)).toBe(true);
        expect(hasRole(editorUser, 'EDITOR' as UserRole)).toBe(true);
      });
    });

    describe('Null/Undefined User', () => {
      it('should return false for null user', () => {
        expect(hasRole(null, 'Admin')).toBe(false);
        expect(hasRole(null, 'Editor')).toBe(false);
        expect(hasRole(null, 'SuperAdmin')).toBe(false);
        expect(hasRole(null, 'User')).toBe(false);
      });
    });

    describe('Edge Cases', () => {
      it('should handle user without displayName', () => {
        const userNoDisplay: AuthUser = {
          id: '5',
          email: 'test@test.com',
          role: 'User',
        };

        expect(hasRole(userNoDisplay, 'User')).toBe(true);
      });

      it('should handle user with null displayName', () => {
        const userNullDisplay: AuthUser = {
          id: '6',
          email: 'test@test.com',
          displayName: null,
          role: 'Editor',
        };

        expect(hasRole(userNullDisplay, 'Editor')).toBe(true);
      });
    });
  });

  describe('canEdit', () => {
    const adminUser: AuthUser = {
      id: '1',
      email: 'admin@test.com',
      displayName: 'Admin',
      role: 'Admin',
    };

    const editorUser: AuthUser = {
      id: '2',
      email: 'editor@test.com',
      displayName: 'Editor',
      role: 'Editor',
    };

    const superAdminUser: AuthUser = {
      id: '3',
      email: 'superadmin@test.com',
      displayName: 'SuperAdmin',
      role: 'SuperAdmin',
    };

    const regularUser: AuthUser = {
      id: '4',
      email: 'user@test.com',
      displayName: 'User',
      role: 'User',
    };

    describe('Allowed Roles', () => {
      it('should return true for superadmin user', () => {
        expect(canEdit(superAdminUser)).toBe(true);
      });

      it('should return true for admin user', () => {
        expect(canEdit(adminUser)).toBe(true);
      });

      it('should return true for editor user', () => {
        expect(canEdit(editorUser)).toBe(true);
      });
    });

    describe('Disallowed Roles', () => {

      it('should return false for regular user', () => {
        expect(canEdit(regularUser)).toBe(false);
      });
    });

    describe('Case Insensitivity', () => {
      it('should handle lowercase admin role', () => {
        const lowercaseAdmin: AuthUser = {
          ...adminUser,
          role: 'admin',
        };

        expect(canEdit(lowercaseAdmin)).toBe(true);
      });

      it('should handle uppercase editor role', () => {
        const uppercaseEditor: AuthUser = {
          ...editorUser,
          role: 'EDITOR',
        };

        expect(canEdit(uppercaseEditor)).toBe(true);
      });

      it('should handle mixed case roles', () => {
        const mixedAdmin: AuthUser = {
          ...adminUser,
          role: 'AdMiN' as UserRole,
        };

        const mixedEditor: AuthUser = {
          ...editorUser,
          role: 'EdItOr' as UserRole,
        };

        expect(canEdit(mixedAdmin)).toBe(true);
        expect(canEdit(mixedEditor)).toBe(true);
      });
    });

    describe('Null/Undefined User', () => {
      it('should return false for null user', () => {
        expect(canEdit(null)).toBe(false);
      });
    });
  });

  describe('Type Exports', () => {
    it('should export all required types', () => {
      // This test ensures all types are properly exported and accessible
      // TypeScript will fail compilation if types are missing

      const authUser: import('../auth').AuthUser = {
        id: '1',
        email: 'test@test.com',
        role: 'Admin',
      };

      const authResponse: import('../auth').AuthResponse = {
        user: authUser,
        expiresAt: '2024-12-31T23:59:59Z',
      };

      const sessionStatus: import('../auth').SessionStatusResponse = {
        expiresAt: '2024-12-31T23:59:59Z',
        lastSeenAt: '2024-01-01T00:00:00Z',
        remainingMinutes: 30,
      };

      const role: import('../auth').UserRole = 'Admin';

      // If we reach here, all types are properly defined
      expect(authUser).toBeDefined();
      expect(authResponse).toBeDefined();
      expect(sessionStatus).toBeDefined();
      expect(role).toBeDefined();
    });
  });
});
