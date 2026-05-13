import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { ToolkitsTabPanel } from '../ToolkitsTabPanel';

const labels = {
  title: 'Toolkit',
  comingSoon: 'I toolkit del giocatore arriveranno presto',
};

describe('ToolkitsTabPanel', () => {
  it('renders the panel root with data-slot="toolkits-tab-panel"', () => {
    const { container } = render(<ToolkitsTabPanel labels={labels} />);
    expect(container.querySelector('[data-slot="toolkits-tab-panel"]')).not.toBeNull();
  });

  it('shows the coming-soon label', () => {
    render(<ToolkitsTabPanel labels={labels} />);
    expect(screen.getByText(/i toolkit del giocatore arriveranno presto/i)).toBeInTheDocument();
  });
});
