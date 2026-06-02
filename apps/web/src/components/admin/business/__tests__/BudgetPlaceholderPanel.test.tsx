import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { BudgetPlaceholderPanel } from '../BudgetPlaceholderPanel';

describe('BudgetPlaceholderPanel', () => {
  it('renders title, description, endpoint, BE pending chip', () => {
    render(
      <BudgetPlaceholderPanel
        id="test-panel"
        title="Test Title"
        description="Test description text"
        endpoint="GET /api/v1/admin/test"
      />
    );

    expect(screen.getByTestId('budget-placeholder-test-panel')).toBeInTheDocument();
    expect(screen.getByText('Test Title')).toBeInTheDocument();
    expect(screen.getByText('Test description text')).toBeInTheDocument();
    expect(screen.getByText(/BE pending/i)).toBeInTheDocument();
    expect(screen.getByText('GET /api/v1/admin/test')).toBeInTheDocument();
  });
});
