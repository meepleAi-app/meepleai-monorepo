import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CompactIconButton } from '../compact-icon-button';
import { Gamepad2 } from 'lucide-react';

describe('CompactIconButton', () => {
  it('renders icon with badge count', () => {
    render(
      <CompactIconButton icon={Gamepad2} count={12} label="Sessioni" entityColor="240 60% 55%" />
    );
    expect(screen.getByText('12')).toBeInTheDocument();
    expect(screen.getByLabelText('12 Sessioni')).toBeInTheDocument();
  });

  it('shows tooltip on hover', async () => {
    render(
      <CompactIconButton icon={Gamepad2} count={3} label="Documenti KB" entityColor="174 60% 40%" />
    );
    await userEvent.hover(screen.getByLabelText('3 Documenti KB'));
    expect(screen.getByText('3 Documenti KB')).toBeVisible();
  });

  it('calls onClick when tapped', async () => {
    const onClick = vi.fn();
    render(
      <CompactIconButton
        icon={Gamepad2}
        count={1}
        label="Agent"
        entityColor="38 92% 50%"
        onClick={onClick}
      />
    );
    await userEvent.click(screen.getByLabelText('1 Agent'));
    expect(onClick).toHaveBeenCalledOnce();
  });

  it('does not render when count is 0', () => {
    const { container } = render(
      <CompactIconButton icon={Gamepad2} count={0} label="Test" entityColor="0 0% 50%" />
    );
    expect(container.firstChild).toBeNull();
  });
});
