/** @vitest-environment jsdom */
import { render, screen, fireEvent } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import {
  MechanicSection,
  type PublishedMechanicCardDto,
} from '@/lib/api/schemas/mechanic-card.schemas';

// ─── Mock the responsive citation panel (#530). The real panel mounts pdfjs +
// Sheet/Drawer primitives; we stub it and assert the props MechanicCardView
// drives it with. The real MechanicCitationBadge still renders (owns the testid).
const mockPanel = vi.hoisted(() => vi.fn());
vi.mock('../MechanicCitationPanel', () => ({
  MechanicCitationPanel: (props: Record<string, unknown>) => {
    mockPanel(props);
    return props.isOpen ? (
      <div data-testid="citation-panel-open">
        {(props.citation as { pdfPage?: number } | null)?.pdfPage}
      </div>
    ) : null;
  },
}));

// ─── Mock the data hook ────────────────────────────────────────────────────────
type MockCardReturn = {
  data?: PublishedMechanicCardDto | null;
  isLoading: boolean;
  isError: boolean;
  refetch: ReturnType<typeof vi.fn>;
};
const cardMockState: MockCardReturn = {
  data: undefined,
  isLoading: false,
  isError: false,
  refetch: vi.fn(),
};
vi.mock('@/hooks/queries/useMechanicCard', () => ({
  useMechanicCard: () => cardMockState,
}));

// ─── notFound spy (called on data === null) ────────────────────────────────────
const notFoundSpy = vi.hoisted(() => vi.fn());
vi.mock('next/navigation', () => ({
  notFound: () => {
    notFoundSpy();
    // Throw like the real notFound() so control flow stops.
    throw new Error('NEXT_NOT_FOUND');
  },
}));

import { MechanicCardView } from '../MechanicCardView';

const SAMPLE_CARD: PublishedMechanicCardDto = {
  cardId: '11111111-1111-4111-8111-111111111111',
  sharedGameId: '22222222-2222-4222-8222-222222222222',
  title: 'Catan — Mechanics',
  version: 3,
  publishedAt: '2026-05-01T00:00:00Z',
  gameName: 'Catan',
  publisher: 'Kosmos',
  language: 'en',
  sourceAnalysisId: '99999999-9999-4999-8999-999999999999',
  publicationYear: 1995,
  documentName: 'Catan Rulebook',
  sections: [
    {
      // Parsed DTO carries the numeric enum (zod preprocesses the "Summary" string).
      section: MechanicSection.Summary,
      claims: [
        {
          id: 'aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaaa',
          claim: 'Players collect resources to build settlements.',
          citations: [
            {
              pdfId: 'ffffffff-ffff-4fff-8fff-ffffffffffff',
              pdfPage: 2,
              quote: 'gather resources',
            },
          ],
        },
      ],
    },
    {
      // Faq enum member must display as "FAQ".
      section: MechanicSection.Faq,
      claims: [
        {
          id: 'bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbbb',
          claim: 'Can you trade on another player’s turn? No.',
          citations: [
            {
              pdfId: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
              pdfPage: 12,
              quote: 'only on your own turn',
            },
          ],
        },
      ],
    },
  ],
};

describe('MechanicCardView', () => {
  beforeEach(() => {
    cardMockState.data = undefined;
    cardMockState.isLoading = false;
    cardMockState.isError = false;
    cardMockState.refetch = vi.fn();
    mockPanel.mockClear();
    notFoundSpy.mockClear();
  });

  it('renders game name, section labels (Faq → FAQ) and claims', () => {
    cardMockState.data = SAMPLE_CARD;
    render(<MechanicCardView gameId={SAMPLE_CARD.sharedGameId} />);

    expect(screen.getByTestId('mechanic-card-title')).toHaveTextContent('Catan');
    // Summary section label
    expect(screen.getByText('Summary')).toBeInTheDocument();
    // Faq enum member displayed as "FAQ"
    expect(screen.getByText('FAQ')).toBeInTheDocument();
    // Claims text present
    expect(screen.getByText('Players collect resources to build settlements.')).toBeInTheDocument();
    expect(screen.getByText(/Can you trade on another player/)).toBeInTheDocument();
    // Attribution footer present
    expect(screen.getByText(/MeepleAI/)).toBeInTheDocument();
    // "AI-generated, human-reviewed" badge links to the explainer
    const badgeLink = screen.getByRole('link', { name: /AI-generated, human-reviewed/ });
    expect(badgeLink).toHaveAttribute('href', '/how-it-works/game-comprehension');
  });

  it('renders a public takedown link in the card footer (#529)', () => {
    cardMockState.data = SAMPLE_CARD;
    render(<MechanicCardView gameId={SAMPLE_CARD.sharedGameId} />);

    const takedownLink = screen.getByTestId('mechanic-card-takedown-link');
    expect(takedownLink).toHaveAttribute('href', '/legal/takedown');
    expect(takedownLink).toHaveTextContent(/copyright/i);
  });

  it('renders a [p.N] citation badge per citation', () => {
    cardMockState.data = SAMPLE_CARD;
    render(<MechanicCardView gameId={SAMPLE_CARD.sharedGameId} />);

    expect(
      screen.getByTestId('mechanic-card-citation-ffffffff-ffff-4fff-8fff-ffffffffffff-2')
    ).toHaveTextContent('p.2');
    expect(
      screen.getByTestId('mechanic-card-citation-eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee-12')
    ).toHaveTextContent('p.12');
  });

  it('opens the citation panel with the clicked citation + card provenance', () => {
    cardMockState.data = SAMPLE_CARD;
    render(<MechanicCardView gameId={SAMPLE_CARD.sharedGameId} />);

    // Before any click, the panel is not open.
    expect(mockPanel).not.toHaveBeenCalledWith(expect.objectContaining({ isOpen: true }));

    fireEvent.click(
      screen.getByTestId('mechanic-card-citation-eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee-12')
    );

    expect(mockPanel).toHaveBeenCalledWith(
      expect.objectContaining({
        isOpen: true,
        citation: expect.objectContaining({
          pdfId: 'eeeeeeee-eeee-4eee-8eee-eeeeeeeeeeee',
          pdfPage: 12,
          quote: 'only on your own turn',
        }),
        card: expect.objectContaining({
          gameName: 'Catan',
          publisher: 'Kosmos',
          publicationYear: 1995,
          documentName: 'Catan Rulebook',
          sourceAnalysisId: '99999999-9999-4999-8999-999999999999',
        }),
      })
    );
    expect(screen.getByTestId('citation-panel-open')).toBeInTheDocument();
  });

  it('shows a loading shell while fetching', () => {
    cardMockState.isLoading = true;
    render(<MechanicCardView gameId={SAMPLE_CARD.sharedGameId} />);
    expect(screen.getByTestId('mechanic-card-loading')).toBeInTheDocument();
  });

  it('shows an error shell with a retry action on error', () => {
    cardMockState.isError = true;
    render(<MechanicCardView gameId={SAMPLE_CARD.sharedGameId} />);
    fireEvent.click(screen.getByTestId('mechanic-card-error-retry'));
    expect(cardMockState.refetch).toHaveBeenCalledTimes(1);
  });

  it('calls notFound() when the card is null (404 / suppressed)', () => {
    cardMockState.data = null;
    // notFound() throws (mimicking Next) — the render throws too.
    expect(() => render(<MechanicCardView gameId={SAMPLE_CARD.sharedGameId} />)).toThrow(
      'NEXT_NOT_FOUND'
    );
    expect(notFoundSpy).toHaveBeenCalled();
  });
});
