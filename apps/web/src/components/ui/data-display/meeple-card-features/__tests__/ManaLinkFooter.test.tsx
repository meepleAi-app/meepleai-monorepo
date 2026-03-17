import { render, screen, fireEvent } from '@testing-library/react';
import { ManaLinkFooter } from '../ManaLinkFooter';

describe('ManaLinkFooter', () => {
  const mockOnPipClick = vi.fn();

  beforeEach(() => {
    mockOnPipClick.mockClear();
  });

  it('renders mana pips for linked entity types', () => {
    render(
      <ManaLinkFooter
        linkedEntities={[
          { entityType: 'session', count: 3 },
          { entityType: 'kb', count: 1 },
        ]}
        onPipClick={mockOnPipClick}
      />
    );
    expect(screen.getByTestId('mana-pip-session')).toBeInTheDocument();
    expect(screen.getByTestId('mana-pip-kb')).toBeInTheDocument();
  });

  it('calls onPipClick with entity type when pip is clicked', () => {
    render(
      <ManaLinkFooter
        linkedEntities={[{ entityType: 'session', count: 3 }]}
        onPipClick={mockOnPipClick}
      />
    );
    fireEvent.click(screen.getByTestId('mana-pip-session'));
    expect(mockOnPipClick).toHaveBeenCalledWith('session');
  });

  it('shows overflow count when more than maxVisible links', () => {
    render(
      <ManaLinkFooter
        linkedEntities={[
          { entityType: 'session', count: 3 },
          { entityType: 'kb', count: 1 },
          { entityType: 'agent', count: 2 },
          { entityType: 'expansion', count: 1 },
          { entityType: 'note', count: 4 },
        ]}
        onPipClick={mockOnPipClick}
        maxVisible={4}
      />
    );
    expect(screen.getByText('+1')).toBeInTheDocument();
  });

  it('renders nothing when linkedEntities is empty', () => {
    const { container } = render(
      <ManaLinkFooter linkedEntities={[]} onPipClick={mockOnPipClick} />
    );
    expect(container.firstChild).toBeNull();
  });
});
