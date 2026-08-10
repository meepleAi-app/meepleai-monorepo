/**
 * @vitest-environment jsdom
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';

vi.mock('@/hooks/admin/useCoverGap', () => ({ useCoverGap: vi.fn() }));

import { useCoverGap } from '@/hooks/admin/useCoverGap';

import CoverGapPage from '../page';

const mockUseCoverGap = useCoverGap as unknown as ReturnType<typeof vi.fn>;
const GAME_ID = '550e8400-e29b-41d4-a716-446655440000';

const gapData = {
  items: [
    {
      gameId: GAME_ID,
      title: 'Catan',
      bggId: null,
      cause: 'no_source' as const,
      pdfFileName: null,
      pdfSizeBytes: null,
      errorCategory: null,
    },
  ],
  total: 1,
  page: 1,
  pageSize: 100,
};

function renderCoverGapPage() {
  mockUseCoverGap.mockReturnValue({
    data: gapData,
    isLoading: false,
    isError: false,
    error: null,
  });
  return render(<CoverGapPage />);
}

describe('CoverGapPage', () => {
  it("il CTA porta al gioco con l'editor già aperto (#3611)", async () => {
    renderCoverGapPage();
    const link = await screen.findByRole('link', { name: /assegna cover/i });
    expect(link).toHaveAttribute('href', `/shared-games/${GAME_ID}?cover=edit`);
  });
});
