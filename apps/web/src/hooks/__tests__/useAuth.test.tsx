/**
 * useAuth Hook Tests
 */
import { renderHook, act, waitFor } from '@testing-library/react';
import { useAuth } from '../useAuth';
import { api } from '@/lib/api';

// Mock next/navigation
const mockPush = vi.fn();
vi.mock('next/navigation', () => ({
  useRouter: () => ({
    push: mockPush,
  }),
}));

// Mock api
vi.mock('@/lib/api', () => ({
  api: {
    get: vi.fn(),
    post: vi.fn(),
  },
}));

describe('useAuth', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('initial state', () => {
    it('should have null user initially', async () => {
      vi.mocked(api.get).mockResolvedValueOnce({ user: null });

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.user).toBeNull();
      expect(result.current.error).toBe('');
    });

    it('should load user on mount', async () => {
      const mockUser = { id: '1', email: 'test@test.com', role: 'User' };
      vi.mocked(api.get).mockResolvedValueOnce({ user: mockUser });

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.user).toEqual(mockUser);
    });
  });

  describe('login', () => {
    it('should login successfully', async () => {
      const mockUser = { id: '1', email: 'test@test.com', role: 'User' };
      vi.mocked(api.get).mockResolvedValueOnce({ user: null });
      vi.mocked(api.post).mockResolvedValueOnce({ user: mockUser });

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await act(async () => {
        const user = await result.current.login({
          email: 'test@test.com',
          password: 'password',
        });
        expect(user).toEqual(mockUser);
      });

      expect(result.current.user).toEqual(mockUser);
    });

    it('should handle login error', async () => {
      vi.mocked(api.get).mockResolvedValueOnce({ user: null });
      vi.mocked(api.post).mockRejectedValueOnce(new Error('Invalid credentials'));

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await act(async () => {
        await expect(
          result.current.login({ email: 'test@test.com', password: 'wrong' })
        ).rejects.toThrow();
      });

      expect(result.current.error).toBe('Invalid credentials');
    });
  });

  describe('register', () => {
    it('should register successfully', async () => {
      const mockUser = { id: '1', email: 'new@test.com', role: 'User' };
      vi.mocked(api.get).mockResolvedValueOnce({ user: null });
      vi.mocked(api.post).mockResolvedValueOnce({ user: mockUser });

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await act(async () => {
        const user = await result.current.register({
          email: 'new@test.com',
          password: 'password',
        });
        expect(user).toEqual(mockUser);
      });

      expect(result.current.user).toEqual(mockUser);
    });
  });

  describe('logout', () => {
    it('should logout and redirect', async () => {
      const mockUser = { id: '1', email: 'test@test.com', role: 'User' };
      vi.mocked(api.get).mockResolvedValueOnce({ user: mockUser });
      vi.mocked(api.post).mockResolvedValueOnce(undefined);

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.user).toEqual(mockUser);
      });

      await act(async () => {
        await result.current.logout();
      });

      expect(result.current.user).toBeNull();
      expect(mockPush).toHaveBeenCalledWith('/');
    });
  });

  describe('clearError', () => {
    it('should clear error', async () => {
      vi.mocked(api.get).mockResolvedValueOnce({ user: null });
      vi.mocked(api.post).mockRejectedValueOnce(new Error('Error'));

      const { result } = renderHook(() => useAuth());

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      await act(async () => {
        await expect(
          result.current.login({ email: 'test@test.com', password: 'wrong' })
        ).rejects.toThrow();
      });

      expect(result.current.error).not.toBe('');

      act(() => {
        result.current.clearError();
      });

      expect(result.current.error).toBe('');
    });
  });
});
