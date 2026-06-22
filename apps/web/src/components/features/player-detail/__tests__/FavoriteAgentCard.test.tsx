/**
 * FavoriteAgentCard unit tests — Wave 3 /players/[id] v2 (Task 2)
 *
 * Tests:
 *   1. Renders agentName and gameName when provided
 *   2. Renders "none" label when agentName is null
 *   3. onClick callback fires when button is clicked
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { axe } from 'jest-axe';
import { describe, it, expect, vi } from 'vitest';

import { FavoriteAgentCard } from '../FavoriteAgentCard';
import type { FavoriteAgentCardLabels } from '../FavoriteAgentCard';

const labels: FavoriteAgentCardLabels = {
  title: 'Favourite Agent',
  none: 'No agent used yet',
  ariaLabel: 'Open {agentName}',
};

describe('FavoriteAgentCard', () => {
  it('renders agent name and game name when provided', () => {
    render(<FavoriteAgentCard agentName="Wingspan Rules AI" gameName="Wingspan" labels={labels} />);
    expect(screen.getByText('Wingspan Rules AI')).toBeInTheDocument();
    expect(screen.getByText('Wingspan')).toBeInTheDocument();
  });

  it('renders "none" label when agentName is null', () => {
    render(<FavoriteAgentCard agentName={null} gameName={null} labels={labels} />);
    expect(screen.getByText('No agent used yet')).toBeInTheDocument();
  });

  it('fires onClick callback when open button is clicked', () => {
    const onClick = vi.fn();
    render(
      <FavoriteAgentCard
        agentName="Azul Rules AI"
        gameName="Azul"
        onClick={onClick}
        labels={labels}
      />
    );
    fireEvent.click(screen.getByRole('button', { name: 'Open Azul Rules AI' }));
    expect(onClick).toHaveBeenCalledTimes(1);
  });

  it('passes axe a11y scan with an agent set', async () => {
    const { container } = render(
      <FavoriteAgentCard
        agentName="Wingspan Rules AI"
        gameName="Wingspan"
        onClick={() => {}}
        labels={labels}
      />
    );
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
