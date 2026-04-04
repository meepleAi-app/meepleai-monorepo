import { render, screen } from '@testing-library/react';
import { Pencil } from 'lucide-react';

import { MeepleCardQuickActions } from '../meeple-card-quick-actions';

describe('MeepleCardQuickActions', () => {
  const actions = [
    {
      icon: Pencil,
      label: 'Modifica',
      onClick: vi.fn(),
    },
  ];

  it("renderizza l'azione visibile con il label corretto", () => {
    render(<MeepleCardQuickActions actions={actions} entityType="game" />);
    const button = screen.getByRole('button', { name: 'Modifica' });
    expect(button).toBeInTheDocument();
  });

  it("l'icon ha la classe che consuma --hover-color al hover", () => {
    render(<MeepleCardQuickActions actions={actions} entityType="player" />);
    const button = screen.getByRole('button', { name: 'Modifica' });
    const icon = button.querySelector('svg');
    // SVG className is an SVGAnimatedString — use getAttribute for a plain string
    expect(icon?.getAttribute('class')).toContain('group-hover:text-[var(--hover-color)]');
  });

  it('il button ha la classe group per abilitare group-hover', () => {
    render(<MeepleCardQuickActions actions={actions} entityType="game" />);
    const button = screen.getByRole('button', { name: 'Modifica' });
    expect(button.className).toContain('group');
  });

  it('nasconde le azioni con hidden=true', () => {
    const hiddenActions = [{ ...actions[0], hidden: true }];
    render(<MeepleCardQuickActions actions={hiddenActions} entityType="game" />);
    expect(screen.queryByRole('button', { name: 'Modifica' })).not.toBeInTheDocument();
  });
});
