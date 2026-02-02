/**
 * TemplateInfoModal Component Tests
 * Issue #3239: [FRONT-003] Template details modal
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import React from 'react';

import { TemplateInfoModal } from '../TemplateInfoModal';

describe('TemplateInfoModal', () => {
  const mockTemplate = {
    id: 'template-1',
    name: 'Rules Helper',
    description: 'Helps explain game rules clearly and accurately',
    systemPrompt: 'You are a helpful board game rules expert. You help players understand the rules of board games by providing clear, accurate explanations.',
    defaultStrategy: 'Step-by-step explanation',
    exampleQuestions: [
      'How does combat work?',
      'What happens during the cleanup phase?',
      'Can I play multiple cards per turn?',
    ],
  };

  const mockOnClose = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders nothing when template is null', () => {
      const { container } = render(
        <TemplateInfoModal template={null} isOpen={true} onClose={mockOnClose} />
      );
      expect(container.firstChild).toBeNull();
    });

    it('renders template name in title', () => {
      render(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.getByText('Rules Helper')).toBeInTheDocument();
    });

    it('renders description section', () => {
      render(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.getByText('Description')).toBeInTheDocument();
      expect(screen.getByText(/Helps explain game rules/)).toBeInTheDocument();
    });

    it('renders truncated system prompt preview', () => {
      render(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.getByText('Base Prompt (Preview)')).toBeInTheDocument();
      // Should show truncated version with ellipsis
      const promptPreview = screen.getByText(/You are a helpful board game rules expert/);
      expect(promptPreview).toBeInTheDocument();
    });

    it('renders default strategy section', () => {
      render(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.getByText('Default Strategy')).toBeInTheDocument();
      expect(screen.getByText('Step-by-step explanation')).toBeInTheDocument();
    });

    it('renders example questions when provided', () => {
      render(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.getByText('Example Questions')).toBeInTheDocument();
      expect(screen.getByText(/How does combat work/)).toBeInTheDocument();
      expect(screen.getByText(/What happens during the cleanup phase/)).toBeInTheDocument();
      expect(screen.getByText(/Can I play multiple cards per turn/)).toBeInTheDocument();
    });

    it('does not render example questions section when empty', () => {
      const templateWithoutExamples = {
        ...mockTemplate,
        exampleQuestions: [],
      };
      render(
        <TemplateInfoModal template={templateWithoutExamples} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.queryByText('Example Questions')).not.toBeInTheDocument();
    });

    it('does not render example questions section when undefined', () => {
      const templateWithoutExamples = {
        ...mockTemplate,
        exampleQuestions: undefined,
      };
      render(
        <TemplateInfoModal template={templateWithoutExamples} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.queryByText('Example Questions')).not.toBeInTheDocument();
    });
  });

  describe('Interaction', () => {
    it('renders "Got it" button', () => {
      render(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.getByRole('button', { name: 'Got it' })).toBeInTheDocument();
    });

    it('calls onClose when "Got it" button is clicked', () => {
      render(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      fireEvent.click(screen.getByRole('button', { name: 'Got it' }));
      expect(mockOnClose).toHaveBeenCalledTimes(1);
    });
  });

  describe('Visibility', () => {
    it('respects isOpen prop', () => {
      const { rerender } = render(
        <TemplateInfoModal template={mockTemplate} isOpen={false} onClose={mockOnClose} />
      );
      // Dialog should not be visible when closed
      expect(screen.queryByText('Rules Helper')).not.toBeInTheDocument();

      rerender(
        <TemplateInfoModal template={mockTemplate} isOpen={true} onClose={mockOnClose} />
      );
      expect(screen.getByText('Rules Helper')).toBeInTheDocument();
    });
  });
});
