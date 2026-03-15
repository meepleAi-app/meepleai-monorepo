import { render, screen, fireEvent } from '@testing-library/react';
import { PrimaryActions } from '../PrimaryActions';

describe('PrimaryActions', () => {
  it('renders up to 2 action buttons', () => {
    const actions = [
      { icon: '▶', label: 'Play', onClick: vi.fn() },
      { icon: '💬', label: 'Ask AI', onClick: vi.fn() },
    ];
    render(<PrimaryActions actions={actions} />);
    expect(screen.getAllByRole('button')).toHaveLength(2);
  });

  it('truncates to 2 max even if more provided', () => {
    const actions = [
      { icon: '▶', label: 'Play', onClick: vi.fn() },
      { icon: '💬', label: 'Ask AI', onClick: vi.fn() },
      { icon: '⚙', label: 'Config', onClick: vi.fn() },
    ];
    render(<PrimaryActions actions={actions} />);
    expect(screen.getAllByRole('button')).toHaveLength(2);
  });

  it('calls onClick when action clicked', () => {
    const onClick = vi.fn();
    render(<PrimaryActions actions={[{ icon: '▶', label: 'Play', onClick }]} />);
    fireEvent.click(screen.getByRole('button'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('renders nothing when no actions', () => {
    const { container } = render(<PrimaryActions actions={[]} />);
    expect(container.firstChild).toBeNull();
  });

  it('stops event propagation on click', () => {
    const parentClick = vi.fn();
    const actionClick = vi.fn();
    render(
      <div onClick={parentClick}>
        <PrimaryActions actions={[{ icon: '▶', label: 'Play', onClick: actionClick }]} />
      </div>
    );
    fireEvent.click(screen.getByRole('button'));
    expect(actionClick).toHaveBeenCalledOnce();
    expect(parentClick).not.toHaveBeenCalled();
  });
});
