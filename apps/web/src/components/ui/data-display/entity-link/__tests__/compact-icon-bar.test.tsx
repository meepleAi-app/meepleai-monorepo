import { render, screen } from '@testing-library/react';
import { CompactIconBar } from '../compact-icon-bar';
import type { EntityLinkGroup } from '../use-entity-link-groups';

describe('CompactIconBar', () => {
  const groups: EntityLinkGroup[] = [
    { entityType: 'Session', count: 12 },
    { entityType: 'KbCard', count: 3 },
    { entityType: 'Agent', count: 1 },
  ];

  it('renders icon buttons for each group', () => {
    render(<CompactIconBar groups={groups} onIconClick={vi.fn()} />);
    expect(screen.getByLabelText('12 Sessioni')).toBeInTheDocument();
    expect(screen.getByLabelText('3 Documenti KB')).toBeInTheDocument();
    expect(screen.getByLabelText('1 Agent')).toBeInTheDocument();
  });

  it('renders nothing when groups is empty', () => {
    const { container } = render(<CompactIconBar groups={[]} onIconClick={vi.fn()} />);
    expect(container.firstChild).toBeNull();
  });

  it('has toolbar role and aria-label', () => {
    render(<CompactIconBar groups={groups} onIconClick={vi.fn()} />);
    expect(screen.getByRole('toolbar')).toHaveAttribute('aria-label', 'Entità collegate');
  });

  it('renders dot separators between groups', () => {
    render(<CompactIconBar groups={groups} onIconClick={vi.fn()} />);
    const bar = screen.getByRole('toolbar');
    const separators = bar.querySelectorAll('[data-separator]');
    expect(separators.length).toBeGreaterThan(0);
  });
});
