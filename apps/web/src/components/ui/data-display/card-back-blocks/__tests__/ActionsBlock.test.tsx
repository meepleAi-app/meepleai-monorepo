import { render, screen, fireEvent } from '@testing-library/react';
import { ActionsBlock } from '../blocks/ActionsBlock';

describe('ActionsBlock', () => {
  it('renders action buttons', () => {
    const onClick = vi.fn();
    render(
      <ActionsBlock
        title="Azioni"
        entityColor="25 95% 45%"
        data={{
          type: 'actions',
          actions: [
            { label: 'Nuova sessione', onClick, icon: '▶' },
            { label: 'Condividi', onClick },
          ],
        }}
      />
    );
    expect(screen.getByText('→ Nuova sessione')).toBeInTheDocument();
    expect(screen.getByText('→ Condividi')).toBeInTheDocument();
  });

  it('calls onClick when action is clicked', () => {
    const onClick = vi.fn();
    render(
      <ActionsBlock
        title="Azioni"
        entityColor="25 95% 45%"
        data={{
          type: 'actions',
          actions: [{ label: 'Play', onClick }],
        }}
      />
    );
    fireEvent.click(screen.getByText('→ Play'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('renders danger variant with different styling', () => {
    render(
      <ActionsBlock
        title="Azioni"
        entityColor="25 95% 45%"
        data={{
          type: 'actions',
          actions: [{ label: 'Delete', onClick: vi.fn(), variant: 'danger' }],
        }}
      />
    );
    const button = screen.getByText('→ Delete');
    expect(button.className).toContain('text-red');
  });
});
