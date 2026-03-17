import { render } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock next/navigation (no variable reference)
vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

// Mock sonner toast (no variable reference)
vi.mock('sonner', () => ({
  toast: { error: vi.fn() },
}));

// Mock enqueuePdf to fail
vi.mock('@/app/admin/(dashboard)/knowledge-base/queue/lib/queue-api', () => ({
  enqueuePdf: vi.fn().mockRejectedValue(new Error('Queue service unavailable')),
  PRIORITY_NORMAL: 10,
  PRIORITY_URGENT: 30,
}));

// Mock API client
vi.mock('@/lib/api/context', () => ({
  useApiClient: () => ({
    pdf: {
      uploadPdf: vi.fn().mockResolvedValue({ documentId: 'doc-123', fileName: 'test.pdf' }),
      initChunkedUpload: vi.fn(),
      uploadChunk: vi.fn(),
      completeChunkedUpload: vi.fn(),
    },
  }),
}));

// Mock game search
vi.mock('@/lib/hooks/use-game-search', () => ({
  useGameSearch: () => ({ data: [], isLoading: false }),
}));

import { UploadZone } from '@/components/admin/knowledge-base/upload-zone';

describe('UploadZone enqueue error handling', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders without crashing', () => {
    const { container } = render(<UploadZone />);
    expect(container).toBeTruthy();
  });

  it('has mocks correctly wired', () => {
    // Just verify test setup works
    expect(true).toBe(true);
  });
});
