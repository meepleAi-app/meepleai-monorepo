import { render, screen } from '@testing-library/react';
import { ManaBadge } from '../ManaBadge';

describe('ManaBadge', () => {
  it('renders mana symbol and entity display name', () => {
    render(<ManaBadge entity="game" />);
    expect(screen.getByText('Game')).toBeInTheDocument();
    expect(screen.getByTestId('mana-symbol-game')).toBeInTheDocument();
  });

  it('renders kb as Knowledge', () => {
    render(<ManaBadge entity="kb" />);
    expect(screen.getByText('Knowledge')).toBeInTheDocument();
  });

  it('renders chatSession as Chat', () => {
    render(<ManaBadge entity="chatSession" />);
    expect(screen.getByText('Chat')).toBeInTheDocument();
  });

  it('has backdrop-blur styling', () => {
    const { container } = render(<ManaBadge entity="game" />);
    const badge = container.firstChild as HTMLElement;
    expect(badge.className).toContain('backdrop-blur');
  });
});
