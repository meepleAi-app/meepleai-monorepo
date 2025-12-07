/**
 * Authentication Actions
 * Client-side action functions for auth operations using React 19 useActionState pattern
 *
 * Issue #1078: FE-IMP-002 — Server Actions per Auth & Export
 *
 * These actions wrap API calls and provide:
 * - Typed error handling
 * - Localized error messages (Italian)
 * - Progress states for UI
 * - CSRF protection via cookies
 */

'use client';

import { api, ApiError } from '@/lib/api';
import { getLocalizedError, type LocalizedError, successMessages } from '@/lib/i18n/errors';
import type { AuthUser } from '@/types';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

// ============================================================================
// Types
// ============================================================================

/**
 * Standard action state returned by all auth actions
 */
export interface AuthActionState {
  success: boolean;
  error?: LocalizedError;
  user?: AuthUser;
  message?: string;
}

export interface SessionActionState {
  success: boolean;
  error?: LocalizedError;
  expiresAt?: string;
  remainingMinutes?: number;
  message?: string;
}

// ============================================================================
// Login Action
// ============================================================================

/**
 * Login action for use with useActionState
 *
 * @example
 * const [state, loginAction, isPending] = useActionState(loginAction, { success: false });
 *
 * <form action={loginAction}>
 *   <input name="email" type="email" required />
 *   <input name="password" type="password" required />
 *   <button disabled={isPending}>Login</button>
 * </form>
 */
export async function loginAction(
  prevState: AuthActionState,
  formData: FormData
): Promise<AuthActionState> {
  try {
    const email = formData.get('email') as string;
    const password = formData.get('password') as string;

    if (!email || !password) {
      return {
        success: false,
        error: {
          type: 'validation',
          message: 'Email e password sono obbligatori.',
        },
      };
    }

    const authUser = await api.auth.login({
      email,
      password,
    });

    return {
      success: true,
      user: authUser,
      message: successMessages.loginSuccess,
    };
  } catch (error) {
    logger.error(
      'Login action failed',
      error instanceof Error ? error : new Error(String(error)),
      createErrorContext('AuthActions', 'loginAction', { operation: 'login' })
    );

    if (error instanceof ApiError) {
      return {
        success: false,
        error: getLocalizedError(error.statusCode, error.message),
      };
    }

    return {
      success: false,
      error: {
        type: 'network',
        message: 'Impossibile connettersi al server.',
      },
    };
  }
}

// ============================================================================
// Register Action
// ============================================================================

/**
 * Registration action for use with useActionState
 *
 * @example
 * const [state, registerAction, isPending] = useActionState(registerAction, { success: false });
 *
 * <form action={registerAction}>
 *   <input name="email" type="email" required />
 *   <input name="password" type="password" required />
 *   <input name="displayName" />
 *   <button disabled={isPending}>Registrati</button>
 * </form>
 */
export async function registerAction(
  prevState: AuthActionState,
  formData: FormData
): Promise<AuthActionState> {
  try {
    const email = formData.get('email') as string;
    const password = formData.get('password') as string;
    const displayName = formData.get('displayName') as string | undefined;

    if (!email || !password) {
      return {
        success: false,
        error: {
          type: 'validation',
          message: 'Email e password sono obbligatori.',
        },
      };
    }

    const authUser = await api.auth.register({
      email,
      password,
      displayName: displayName || undefined,
      role: 'User', // Default role
    });

    return {
      success: true,
      user: authUser,
      message: successMessages.registrationSuccess,
    };
  } catch (error) {
    logger.error(
      'Registration action failed',
      error instanceof Error ? error : new Error(String(error)),
      createErrorContext('AuthActions', 'registerAction', { operation: 'register' })
    );

    if (error instanceof ApiError) {
      // Special handling for email conflict (409)
      if (error.statusCode === 409) {
        return {
          success: false,
          error: getLocalizedError(409, error.message, 'email'),
        };
      }

      return {
        success: false,
        error: getLocalizedError(error.statusCode, error.message),
      };
    }

    return {
      success: false,
      error: {
        type: 'network',
        message: 'Impossibile connettersi al server.',
      },
    };
  }
}

// ============================================================================
// Logout Action
// ============================================================================

/**
 * Logout action for use with useTransition or direct call
 *
 * @example
 * const [isPending, startTransition] = useTransition();
 *
 * const handleLogout = () => {
 *   startTransition(async () => {
 *     const result = await logoutAction();
 *     if (result.success) {
 *       // Handle successful logout
 *     }
 *   });
 * };
 */
export async function logoutAction(): Promise<AuthActionState> {
  try {
    await api.auth.logout();

    return {
      success: true,
      message: successMessages.logoutSuccess,
    };
  } catch (error) {
    logger.error(
      'Logout action failed',
      error instanceof Error ? error : new Error(String(error)),
      createErrorContext('AuthActions', 'logoutAction', { operation: 'logout' })
    );

    // Even if API call fails, consider it success to clear client state
    // User can't remain logged in if server rejected logout
    return {
      success: true,
      message: successMessages.logoutSuccess,
    };
  }
}

// ============================================================================
// Extend Session Action
// ============================================================================

/**
 * Extend session action for session management
 *
 * @example
 * const [isPending, startTransition] = useTransition();
 *
 * const handleExtendSession = () => {
 *   startTransition(async () => {
 *     const result = await extendSessionAction();
 *     if (result.success) {
 *       // Show remaining time
 *     }
 *   });
 * };
 */
export async function extendSessionAction(): Promise<SessionActionState> {
  try {
    const response = await api.auth.extendSession();

    return {
      success: true,
      expiresAt: response.ExpiresAt,
      remainingMinutes: response.RemainingMinutes,
      message: successMessages.sessionExtended,
    };
  } catch (error) {
    logger.error(
      'Extend session action failed',
      error instanceof Error ? error : new Error(String(error)),
      createErrorContext('AuthActions', 'extendSessionAction', { operation: 'extend_session' })
    );

    if (error instanceof ApiError) {
      return {
        success: false,
        error: getLocalizedError(error.statusCode, error.message),
      };
    }

    return {
      success: false,
      error: {
        type: 'network',
        message: 'Impossibile estendere la sessione.',
      },
    };
  }
}

// ============================================================================
// Get Current User (for AuthProvider compatibility)
// ============================================================================

/**
 * Get current authenticated user
 * Not a form action, but included for AuthProvider integration
 */
export async function getCurrentUser(): Promise<AuthActionState> {
  try {
    const authUser = await api.auth.getMe();

    if (!authUser) {
      return {
        success: false,
        user: undefined,
      };
    }

    return {
      success: true,
      user: authUser,
    };
  } catch (error) {
    logger.error(
      'Get current user failed',
      error instanceof Error ? error : new Error(String(error)),
      createErrorContext('AuthActions', 'getCurrentUser', { operation: 'get_current_user' })
    );

    return {
      success: false,
      error:
        error instanceof ApiError
          ? getLocalizedError(error.statusCode, error.message)
          : {
              type: 'network',
              message: 'Impossibile caricare i dati utente.',
            },
    };
  }
}
