/**
 * @vitest-environment jsdom
 *
 * a11y (axe AA) coverage for the cover-editor overlay surfaces. The A11y gate is
 * blocking (#2055 / a11y-baseline), so any ARIA/contrast violation here is a real
 * regression. The Radix Dialog portals to document.body, so axe runs on the body.
 */
import { render } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/hooks/admin/useCoverCandidates', () => ({ useCoverCandidates: vi.fn() }));
vi.mock('@/hooks/admin/useAssignCover', () => ({ useAssignCover: vi.fn() }));
vi.mock('@/hooks/admin/useRemoveCoverAssignment', () => ({ useRemoveCoverAssignment: vi.fn() }));
vi.mock('@/hooks/admin/useSetManualCover', () => ({ useSetManualCover: vi.fn() }));

import { useCoverCandidates } from '@/hooks/admin/useCoverCandidates';
import { useAssignCover } from '@/hooks/admin/useAssignCover';
import { useRemoveCoverAssignment } from '@/hooks/admin/useRemoveCoverAssignment';
import { useSetManualCover } from '@/hooks/admin/useSetManualCover';

import { AdminCoverSourceDialog } from '../AdminCoverSourceDialog';
import { CoverFocalPointPicker } from '../CoverFocalPointPicker';

const GID = '550e8400-e29b-41d4-a716-446655440000';

beforeEach(() => {
  (useAssignCover as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
  });
  (useRemoveCoverAssignment as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
  });
  (useSetManualCover as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
    mutate: vi.fn(),
    isPending: false,
    isError: false,
    isSuccess: false,
  });
  (useCoverCandidates as unknown as ReturnType<typeof vi.fn>).mockReturnValue({
    isLoading: false,
    isError: false,
    data: {
      gameId: GID,
      candidates: [
        {
          source: 'Pdf',
          previewUrl: 'https://r2.example/pdf.webp',
          license: null,
          attribution: null,
          sourceUrl: null,
        },
        {
          source: 'Wikidata',
          previewUrl: 'https://r2.example/wiki.webp',
          license: 'CC BY-SA 4.0',
          attribution: 'Jane',
          sourceUrl: 'https://commons.example/x',
        },
      ],
      assignments: { card: { source: 'Pdf', focalX: 0.5, focalY: 0.5 }, hero: null, social: null },
    },
  });
});

describe('cover-editor a11y (axe AA)', () => {
  it('AdminCoverSourceDialog has no violations when open', async () => {
    render(<AdminCoverSourceDialog gameId={GID} title="Catan" open onClose={vi.fn()} />);
    expect(await axe(document.body)).toHaveNoViolations();
  });

  it('CoverFocalPointPicker has no violations', async () => {
    const { container } = render(
      <div className="relative">
        <CoverFocalPointPicker
          imageUrl="https://r2.example/wiki.webp"
          alt="Anteprima Wikidata"
          x={0.5}
          y={0.5}
          onChange={vi.fn()}
          label="Punto focale copertina"
        />
      </div>
    );
    expect(await axe(container)).toHaveNoViolations();
  });
});
