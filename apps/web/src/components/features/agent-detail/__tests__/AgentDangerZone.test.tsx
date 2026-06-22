/**
 * AgentDangerZone unit tests — Wave C.2 Task 2
 *
 * 2 tests: active variant only (renders), non-active returns null.
 */

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { AgentDangerZone } from '../AgentDangerZone';

const LABELS = {
  dangerZoneTitle: 'Zona pericolosa',
  archiveCta: 'Archivia agente',
  archiveConfirmTitle: 'Archivia agente?',
  archiveConfirmSubtitle: "L'agente verrà disattivato. Potrai riattivarlo in qualsiasi momento.",
  archiveConfirm: 'Archivia',
  archiveCancel: 'Annulla',
};

describe('AgentDangerZone', () => {
  it('active variant: renders danger zone with archive CTA', () => {
    render(<AgentDangerZone variant="active" onArchive={vi.fn()} labels={LABELS} />);
    expect(document.querySelector('[data-slot="agent-detail-danger-zone"]')).toBeTruthy();
    expect(screen.getByRole('button', { name: /archivia agente/i })).toBeInTheDocument();
  });

  it('non-active variant: returns null (nothing rendered)', () => {
    const { container } = render(
      <AgentDangerZone variant="archived" onArchive={vi.fn()} labels={LABELS} />
    );
    expect(container.firstChild).toBeNull();
  });
});
