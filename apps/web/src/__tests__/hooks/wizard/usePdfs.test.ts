import { renderHook, waitFor } from '@testing-library/react';
import { usePdfs } from '@/hooks/wizard/usePdfs';

describe('usePdfs', () => {
  const mockPdfs = [
    {
      id: '1',
      fileName: 'rules.pdf',
      fileSizeBytes: 1024 * 1024,
      uploadedAt: '2024-01-15T10:30:00Z',
      uploadedByUserId: '990e8400-e29b-41d4-a716-000000000001',
      language: 'en',
      status: 'completed',
      logUrl: 'https://example.com/log1'
    },
    {
      id: '2',
      fileName: 'guide.pdf',
      fileSizeBytes: 1024 * 512,
      uploadedAt: '2024-01-16T14:20:00Z',
      uploadedByUserId: '990e8400-e29b-41d4-a716-000000000001',
      language: 'it',
      status: 'pending',
      logUrl: null
    }
  ];

  beforeEach(() => {
    jest.clearAllMocks();
    global.fetch = jest.fn();
  });

  afterEach(() => {
    jest.restoreAllMocks();
  });

  describe('Initial State', () => {
    it('initializes with empty state when gameId is null', () => {
      const { result } = renderHook(() => usePdfs(null));

      expect(result.current.pdfs).toEqual([]);
      expect(result.current.loading).toBe(false);
      expect(result.current.error).toBeNull();
    });

    it('does not fetch when gameId is null', () => {
      renderHook(() => usePdfs(null));

      expect(global.fetch).not.toHaveBeenCalled();
    });
  });

  describe('Fetching PDFs', () => {
    it('fetches PDFs when gameId is provided', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      expect(result.current.loading).toBe(true);

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(result.current.pdfs).toEqual(mockPdfs);
      expect(global.fetch).toHaveBeenCalledWith(
        'http://localhost:5080/api/v1/games/game-1/pdfs',
        { credentials: 'include' }
      );
    });

    it('handles successful fetch with empty array', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: [] })
      });

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(result.current.pdfs).toEqual([]);
        expect(result.current.loading).toBe(false);
      });
    });

    it('handles response without pdfs property', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({})
      });

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(result.current.pdfs).toEqual([]);
      });
    });
  });

  describe('Error Handling', () => {
    it('sets error when fetch fails', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        statusText: 'Not Found'
      });

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(result.current.error).toBe('Unable to load uploaded PDFs. Please try again.');
        expect(result.current.loading).toBe(false);
      });
    });

    it('logs error to console when fetch fails', async () => {
      const consoleSpy = jest.spyOn(console, 'error').mockImplementation();
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: false,
        statusText: 'Server Error'
      });

      renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(consoleSpy).toHaveBeenCalledWith('Failed to load PDFs:', 'Server Error');
      });

      consoleSpy.mockRestore();
    });

    it('handles network error', async () => {
      (global.fetch as jest.Mock).mockRejectedValue(new Error('Network error'));

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(result.current.error).toBe('Unable to load uploaded PDFs. Please try again.');
      });
    });
  });

  describe('Game ID Changes', () => {
    it('refetches when gameId changes', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      const { result, rerender } = renderHook(
        ({ gameId }) => usePdfs(gameId),
        { initialProps: { gameId: '770e8400-e29b-41d4-a716-000000000001' } }
      );

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(global.fetch).toHaveBeenCalledTimes(1);

      // Change gameId
      rerender({ gameId: '770e8400-e29b-41d4-a716-000000000002' });

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledTimes(2);
      });

      expect(global.fetch).toHaveBeenLastCalledWith(
        'http://localhost:5080/api/v1/games/game-2/pdfs',
        { credentials: 'include' }
      );
    });

    it('clears PDFs when gameId becomes null', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      const { result, rerender } = renderHook(
        ({ gameId }: { gameId: string | null }) => usePdfs(gameId),
        { initialProps: { gameId: '770e8400-e29b-41d4-a716-000000000001' as string | null } }
      );

      await waitFor(() => {
        expect(result.current.pdfs).toHaveLength(2);
      });

      rerender({ gameId: null });

      expect(result.current.pdfs).toEqual([]);
      expect(result.current.error).toBeNull();
    });

    it('does not refetch if gameId changes to same value', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      const { rerender } = renderHook(
        ({ gameId }) => usePdfs(gameId),
        { initialProps: { gameId: '770e8400-e29b-41d4-a716-000000000001' } }
      );

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledTimes(1);
      });

      rerender({ gameId: '770e8400-e29b-41d4-a716-000000000001' });

      // Should not trigger additional fetch (useCallback dependency)
      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledTimes(1);
      });
    });
  });

  describe('Manual Refetch', () => {
    it('provides refetch function', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });

      expect(typeof result.current.refetch).toBe('function');
    });

    it('refetches PDFs when refetch is called', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledTimes(1);
      });

      await waitFor(async () => {
        await result.current.refetch();
      });

      expect(global.fetch).toHaveBeenCalledTimes(2);
    });

    it('updates loading state during refetch', async () => {
      let resolveFirst: (value: any) => void;
      const firstPromise = new Promise((resolve) => {
        resolveFirst = resolve;
      });
      (global.fetch as jest.Mock).mockReturnValueOnce(firstPromise);

      const { result } = renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      expect(result.current.loading).toBe(true);

      await waitFor(async () => {
        resolveFirst!({
          ok: true,
          json: async () => ({ pdfs: mockPdfs })
        });
      });

      await waitFor(() => {
        expect(result.current.loading).toBe(false);
      });
    });
  });

  describe('API Base URL', () => {
    it('uses NEXT_PUBLIC_API_BASE from environment', async () => {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          'https://api.example.com/api/v1/games/game-1/pdfs',
          { credentials: 'include' }
        );
      });

      delete process.env.NEXT_PUBLIC_API_BASE;
    });

    it('falls back to localhost when env var not set', async () => {
      delete process.env.NEXT_PUBLIC_API_BASE;

      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          'http://localhost:5080/api/v1/games/game-1/pdfs',
          { credentials: 'include' }
        );
      });
    });
  });

  describe('Credentials', () => {
    it('includes credentials in fetch request', async () => {
      (global.fetch as jest.Mock).mockResolvedValue({
        ok: true,
        json: async () => ({ pdfs: mockPdfs })
      });

      renderHook(() => usePdfs('770e8400-e29b-41d4-a716-000000000001'));

      await waitFor(() => {
        expect(global.fetch).toHaveBeenCalledWith(
          expect.any(String),
          expect.objectContaining({ credentials: 'include' })
        );
      });
    });
  });
});
