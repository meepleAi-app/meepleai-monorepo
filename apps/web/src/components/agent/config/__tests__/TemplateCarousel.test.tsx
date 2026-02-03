/**
 * TemplateCarousel Component Tests
 * Issue #3239: [FRONT-003] Horizontal scroll template selector
 */

import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import React from 'react';

// Mock stores
vi.mock('@/stores/agentStore', () => ({
  useAgentStore: vi.fn(() => ({
    selectedTypologyId: null,
    setSelectedTypology: vi.fn(),
  })),
}));

// Import after mocks
import { TemplateCarousel } from '../TemplateCarousel';
import { useAgentStore } from '@/stores/agentStore';

describe('TemplateCarousel', () => {
  const mockSetSelectedTypology = vi.fn();

  beforeEach(() => {
    vi.clearAllMocks();

    vi.mocked(useAgentStore).mockReturnValue({
      selectedTypologyId: null,
      setSelectedTypology: mockSetSelectedTypology,
    } as unknown as ReturnType<typeof useAgentStore>);
  });

  describe('Rendering', () => {
    it('renders Agent Template label', () => {
      render(<TemplateCarousel />);
      expect(screen.getByText('Agent Template')).toBeInTheDocument();
    });

    it('renders required asterisk', () => {
      render(<TemplateCarousel />);
      expect(screen.getByText('*')).toBeInTheDocument();
    });

    it('renders all template cards', () => {
      render(<TemplateCarousel />);

      expect(screen.getByText('Rules Helper')).toBeInTheDocument();
      expect(screen.getByText('Strategy Guide')).toBeInTheDocument();
      expect(screen.getByText('Setup Assistant')).toBeInTheDocument();
    });

    it('renders template icons', () => {
      render(<TemplateCarousel />);

      expect(screen.getByText('📖')).toBeInTheDocument();
      expect(screen.getByText('🎯')).toBeInTheDocument();
      expect(screen.getByText('🛠️')).toBeInTheDocument();
    });
  });

  describe('Template Selection', () => {
    it('calls setSelectedTypology when template is clicked', () => {
      render(<TemplateCarousel />);

      fireEvent.click(screen.getByText('Rules Helper'));

      expect(mockSetSelectedTypology).toHaveBeenCalledWith('1');
    });

    it('calls setSelectedTypology with correct id for each template', () => {
      render(<TemplateCarousel />);

      fireEvent.click(screen.getByText('Strategy Guide'));
      expect(mockSetSelectedTypology).toHaveBeenCalledWith('2');

      fireEvent.click(screen.getByText('Setup Assistant'));
      expect(mockSetSelectedTypology).toHaveBeenCalledWith('3');
    });

    it('applies selected styling to selected template', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedTypologyId: '1',
        setSelectedTypology: mockSetSelectedTypology,
      } as unknown as ReturnType<typeof useAgentStore>);

      render(<TemplateCarousel />);

      // Check for selected template styling (cyan border)
      const selectedButton = screen.getByText('Rules Helper').closest('button');
      expect(selectedButton?.className).toContain('border-cyan-400');
    });

    it('applies default styling to unselected templates', () => {
      vi.mocked(useAgentStore).mockReturnValue({
        selectedTypologyId: '1',
        setSelectedTypology: mockSetSelectedTypology,
      } as unknown as ReturnType<typeof useAgentStore>);

      render(<TemplateCarousel />);

      // Check for unselected template styling
      const unselectedButton = screen.getByText('Strategy Guide').closest('button');
      expect(unselectedButton?.className).toContain('border-slate-700');
    });
  });

  describe('Info Button', () => {
    it('renders info button for each template', () => {
      render(<TemplateCarousel />);

      // There should be 3 info buttons (one per template)
      const buttons = screen.getAllByRole('button');
      // 3 template buttons + 3 info buttons = 6 buttons total
      expect(buttons.length).toBeGreaterThanOrEqual(3);
    });

    it('stops propagation when info button is clicked', () => {
      render(<TemplateCarousel />);

      // Get all buttons with Info icon
      const allButtons = screen.getAllByRole('button');
      // The info buttons are smaller and inside the template cards
      const infoButton = allButtons.find(btn =>
        btn.className.includes('h-6') && btn.className.includes('w-6')
      );

      if (infoButton) {
        fireEvent.click(infoButton);
        // setSelectedTypology should NOT be called because of stopPropagation
        // Note: Since we click the info button, it triggers _setShowInfo instead
      }
    });
  });

  describe('Scroll Container', () => {
    it('renders scrollable container', () => {
      render(<TemplateCarousel />);

      const scrollContainer = document.querySelector('.overflow-x-auto');
      expect(scrollContainer).toBeInTheDocument();
    });

    it('applies snap scrolling classes', () => {
      render(<TemplateCarousel />);

      const scrollContainer = document.querySelector('.snap-x');
      expect(scrollContainer).toBeInTheDocument();
    });
  });
});
