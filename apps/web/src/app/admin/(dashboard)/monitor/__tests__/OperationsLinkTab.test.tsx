import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';
import { OperationsLinkTab } from '../OperationsLinkTab';

describe('OperationsLinkTab', () => {
  it('mostra link alla Operations Console', () => {
    render(<OperationsLinkTab />);
    expect(screen.getByRole('link', { name: /apri operations console/i })).toHaveAttribute(
      'href',
      '/admin/monitor/operations'
    );
  });

  it('mostra le 4 sezioni disponibili', () => {
    render(<OperationsLinkTab />);
    expect(screen.getByText('Resources')).toBeInTheDocument();
    expect(screen.getByText('Queue')).toBeInTheDocument();
    expect(screen.getByText('Emergency')).toBeInTheDocument();
    expect(screen.getByText('Audit')).toBeInTheDocument();
  });
});
