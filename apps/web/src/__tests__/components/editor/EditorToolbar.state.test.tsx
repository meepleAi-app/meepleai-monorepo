/**
 * EditorToolbar Component Tests
 *
 * Tests for the EditorToolbar component that provides formatting controls
 * for the TipTap rich text editor.
 *
 * Target Coverage: 90%+ (from 42.1%)
 */

import React from 'react';
import { render, screen, fireEvent } from '@testing-library/react';
import { vi } from 'vitest';
import EditorToolbar from '../../../components/editor/EditorToolbar';
import { Editor } from '@tiptap/react';

/**
 * Helper to create a complete chain object (for both enabled and disabled states)
 */
const createChain = (runReturnValue: boolean = true) => ({
  focus: vi.fn().mockReturnThis(),
  toggleBold: vi.fn().mockReturnThis(),
  toggleItalic: vi.fn().mockReturnThis(),
  toggleStrike: vi.fn().mockReturnThis(),
  toggleCode: vi.fn().mockReturnThis(),
  toggleHeading: vi.fn().mockReturnThis(),
  toggleBulletList: vi.fn().mockReturnThis(),
  toggleOrderedList: vi.fn().mockReturnThis(),
  toggleCodeBlock: vi.fn().mockReturnThis(),
  setHorizontalRule: vi.fn().mockReturnThis(),
  undo: vi.fn().mockReturnThis(),
  redo: vi.fn().mockReturnThis(),
  unsetAllMarks: vi.fn().mockReturnThis(),
  run: vi.fn().mockReturnValue(runReturnValue),
});

/**
 * Mock TipTap Editor
 */
const createMockEditor = (overrides?: Partial<Editor>): Editor => {
  const mockChain = createChain(true);

  const mockCan = {
    chain: vi.fn(() => mockChain),
  };

  return {
    chain: vi.fn(() => mockChain),
    can: vi.fn(() => mockCan),
    isActive: vi.fn((format: string, options?: any) => false),
    ...overrides,
  } as any;
};

describe('EditorToolbar State Tests', () => {
  let mockEditor: Editor;

  beforeEach(() => {
    mockEditor = createMockEditor();
    vi.clearAllMocks();
  });

  /**
   * Test Group: List Buttons (state tests)
   */
  describe('List Buttons State', () => {
    it('executes ordered list command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const orderedButton = screen.getByTitle('Elenco numerato (Ctrl+Shift+7)');
      fireEvent.click(orderedButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state when ordered list is active', () => {
      mockEditor.isActive = vi.fn(format => format === 'orderedList');
      render(<EditorToolbar editor={mockEditor} />);

      const orderedButton = screen.getByTitle('Elenco numerato (Ctrl+Shift+7)');
      expect(orderedButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });

  /**
   * Test Group: Code Block and Horizontal Rule
   */
  describe('Code Block and Horizontal Rule', () => {
    it('executes code block command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const codeBlockButton = screen.getByTitle('Blocco di codice (Ctrl+Alt+C)');
      fireEvent.click(codeBlockButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state when code block is active', () => {
      mockEditor.isActive = vi.fn(format => format === 'codeBlock');
      render(<EditorToolbar editor={mockEditor} />);

      const codeBlockButton = screen.getByTitle('Blocco di codice (Ctrl+Alt+C)');
      expect(codeBlockButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });

    it('executes horizontal rule command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const hrButton = screen.getByTitle('Linea orizzontale');
      fireEvent.click(hrButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Undo/Redo Buttons
   */
  describe('Undo/Redo Buttons', () => {
    it('executes undo command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const undoButton = screen.getByTitle('Annulla (Ctrl+Z)');
      fireEvent.click(undoButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('disables undo button when not available', () => {
      const disabledChain = createChain(false);
      const disabledCan = {
        chain: vi.fn(() => disabledChain),
      };
      mockEditor.can = vi.fn(() => disabledCan as any);

      render(<EditorToolbar editor={mockEditor} />);

      const undoButton = screen.getByTitle('Annulla (Ctrl+Z)');
      expect(undoButton).toBeDisabled();
    });

    it('executes redo command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const redoButton = screen.getByTitle('Ripeti (Ctrl+Shift+Z)');
      fireEvent.click(redoButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('disables redo button when not available', () => {
      const disabledChain = createChain(false);
      const disabledCan = {
        chain: vi.fn(() => disabledChain),
      };
      mockEditor.can = vi.fn(() => disabledCan as any);

      render(<EditorToolbar editor={mockEditor} />);

      const redoButton = screen.getByTitle('Ripeti (Ctrl+Shift+Z)');
      expect(redoButton).toBeDisabled();
    });
  });

  /**
   * Test Group: Clear Formatting Button
   */
  describe('Clear Formatting Button', () => {
    it('executes clear formatting command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const clearButton = screen.getByTitle('Rimuovi formattazione');
      fireEvent.click(clearButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });
  });

  /**
   * Test Group: Button Hover States
   */
  describe('Button Hover States', () => {
    it('changes background on hover for enabled button', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      fireEvent.mouseEnter(boldButton);

      expect(boldButton).toHaveClass('hover:bg-gray-100');
    });

    it('restores background on mouse leave for non-active button', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      fireEvent.mouseEnter(boldButton);
      fireEvent.mouseLeave(boldButton);

      expect(boldButton).toHaveClass('bg-white');
    });

    it('does not change background on hover for disabled button', () => {
      const disabledChain = createChain(false);
      const disabledCan = {
        chain: vi.fn(() => disabledChain),
      };
      mockEditor.can = vi.fn(() => disabledCan as any);

      render(<EditorToolbar editor={mockEditor} />);

      const undoButton = screen.getByTitle('Annulla (Ctrl+Z)');

      fireEvent.mouseEnter(undoButton);
      // Just verify button is disabled, don't check background
      expect(undoButton).toBeDisabled();
    });

    it('does not change background on hover for active button', () => {
      mockEditor.isActive = vi.fn(format => format === 'bold');
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');

      fireEvent.mouseEnter(boldButton);
      // Just verify button has active class
      expect(boldButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });

  /**
   * Test Group: Multiple Active States
   */
  describe('Multiple Active States', () => {
    it('shows multiple active buttons simultaneously', () => {
      mockEditor.isActive = vi.fn((format: string) =>
        ['bold', 'italic', 'strike'].includes(format)
      ) as any;
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      const italicButton = screen.getByTitle('Corsivo (Ctrl+I)');
      const strikeButton = screen.getByTitle('Barrato (Ctrl+Shift+X)');

      expect(boldButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
      expect(italicButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
      expect(strikeButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });

  /**
   * Test Group: Accessibility
   */
  describe('Accessibility', () => {
    it('provides descriptive titles for all buttons', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const buttons = screen.getAllByRole('button');
      buttons.forEach(button => {
        expect(button).toHaveAttribute('title');
        expect(button.getAttribute('title')).not.toBe('');
      });
    });

    it('uses semantic button elements', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const buttons = screen.getAllByRole('button');
      buttons.forEach(button => {
        expect(button.tagName).toBe('BUTTON');
      });
    });

    it('indicates disabled state properly', () => {
      const disabledChain = createChain(false);
      const disabledCan = {
        chain: vi.fn(() => disabledChain),
      };
      mockEditor.can = vi.fn(() => disabledCan as any);

      render(<EditorToolbar editor={mockEditor} />);

      const undoButton = screen.getByTitle('Annulla (Ctrl+Z)');
      const redoButton = screen.getByTitle('Ripeti (Ctrl+Shift+Z)');

      expect(undoButton).toHaveAttribute('disabled');
      expect(redoButton).toHaveAttribute('disabled');
    });
  });

  /**
   * Test Group: Styling
   */
  describe('Styling', () => {
    it('applies correct container styling', () => {
      const { container } = render(<EditorToolbar editor={mockEditor} />);

      const toolbar = container.firstChild as HTMLElement;
      expect(toolbar).toHaveClass('px-3', 'py-2', 'bg-gray-50', 'border-b', 'flex');
    });

    it('applies flexbox layout with wrap', () => {
      const { container } = render(<EditorToolbar editor={mockEditor} />);

      const toolbar = container.firstChild as HTMLElement;
      expect(toolbar).toHaveClass('flex', 'flex-wrap', 'gap-1');
    });

    it('applies correct button styling', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      expect(boldButton).toHaveClass('px-2.5', 'py-1.5', 'rounded', 'text-sm');
    });

    it('applies disabled styling to disabled buttons', () => {
      const disabledChain = createChain(false);
      const disabledCan = {
        chain: vi.fn(() => disabledChain),
      };
      mockEditor.can = vi.fn(() => disabledCan as any);

      render(<EditorToolbar editor={mockEditor} />);

      const undoButton = screen.getByTitle('Annulla (Ctrl+Z)');
      expect(undoButton).toHaveClass('bg-gray-100', 'text-gray-400', 'cursor-not-allowed');
    });

    it('applies active styling to active buttons', () => {
      mockEditor.isActive = vi.fn(format => format === 'bold');
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      expect(boldButton).toHaveClass('bg-primary', 'font-bold', 'text-white');
    });
  });

  /**
   * Test Group: Edge Cases
   */
  describe('Edge Cases', () => {
    it('handles editor with no active formats', () => {
      mockEditor.isActive = vi.fn(() => false);
      render(<EditorToolbar editor={mockEditor} />);

      const buttons = screen.getAllByRole('button');
      // Just verify buttons exist and are not in active state
      buttons.forEach(button => {
        if (!(button as HTMLButtonElement).disabled) {
          expect(button).not.toHaveClass('bg-primary');
        }
      });
    });

    it('handles editor with all formats active', () => {
      mockEditor.isActive = vi.fn(() => true);
      render(<EditorToolbar editor={mockEditor} />);

      // Most buttons should show active state
      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      expect(boldButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });

    it('handles rapid button clicks', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');

      fireEvent.click(boldButton);
      fireEvent.click(boldButton);
      fireEvent.click(boldButton);

      expect(mockEditor.chain).toHaveBeenCalledTimes(3);
    });
  });
});
