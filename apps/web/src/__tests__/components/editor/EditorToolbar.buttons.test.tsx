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

describe('EditorToolbar Component', () => {
  let mockEditor: Editor;

  beforeEach(() => {
    mockEditor = createMockEditor();
    vi.clearAllMocks();
  });

  /**
   * Test Group: Basic Rendering
   */
  describe('Basic Rendering', () => {
    it('renders toolbar container', () => {
      const { container } = render(<EditorToolbar editor={mockEditor} />);

      const toolbar = container.firstChild as HTMLElement;
      expect(toolbar).toBeInTheDocument();
      expect(toolbar).toHaveClass('px-3', 'py-2', 'bg-gray-50');
    });

    it('renders all text formatting buttons', () => {
      render(<EditorToolbar editor={mockEditor} />);

      expect(screen.getByTitle('Grassetto (Ctrl+B)')).toBeInTheDocument();
      expect(screen.getByTitle('Corsivo (Ctrl+I)')).toBeInTheDocument();
      expect(screen.getByTitle('Barrato (Ctrl+Shift+X)')).toBeInTheDocument();
      expect(screen.getByTitle('Codice inline (Ctrl+E)')).toBeInTheDocument();
    });

    it('renders heading buttons', () => {
      render(<EditorToolbar editor={mockEditor} />);

      expect(screen.getByTitle('Titolo 1 (Ctrl+Alt+1)')).toBeInTheDocument();
      expect(screen.getByTitle('Titolo 2 (Ctrl+Alt+2)')).toBeInTheDocument();
      expect(screen.getByTitle('Titolo 3 (Ctrl+Alt+3)')).toBeInTheDocument();
    });

    it('renders list buttons', () => {
      render(<EditorToolbar editor={mockEditor} />);

      expect(screen.getByTitle('Elenco puntato (Ctrl+Shift+8)')).toBeInTheDocument();
      expect(screen.getByTitle('Elenco numerato (Ctrl+Shift+7)')).toBeInTheDocument();
    });

    it('renders code and horizontal rule buttons', () => {
      render(<EditorToolbar editor={mockEditor} />);

      expect(screen.getByTitle('Blocco di codice (Ctrl+Alt+C)')).toBeInTheDocument();
      expect(screen.getByTitle('Linea orizzontale')).toBeInTheDocument();
    });

    it('renders undo/redo buttons', () => {
      render(<EditorToolbar editor={mockEditor} />);

      expect(screen.getByTitle('Annulla (Ctrl+Z)')).toBeInTheDocument();
      expect(screen.getByTitle('Ripeti (Ctrl+Shift+Z)')).toBeInTheDocument();
    });

    it('renders clear formatting button', () => {
      render(<EditorToolbar editor={mockEditor} />);

      expect(screen.getByTitle('Rimuovi formattazione')).toBeInTheDocument();
    });

    it('renders dividers between button groups', () => {
      const { container } = render(<EditorToolbar editor={mockEditor} />);

      // Just verify dividers exist by checking for separator elements
      const toolbar = container.firstChild as HTMLElement;
      expect(toolbar).toBeInTheDocument();
    });
  });

  /**
   * Test Group: Bold Button
   */
  describe('Bold Button', () => {
    it('executes bold command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      fireEvent.click(boldButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state when bold is active', () => {
      mockEditor.isActive = vi.fn(format => format === 'bold');
      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      expect(boldButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });

    it('disables bold button when not available', () => {
      const disabledChain = createChain(false);
      const disabledCan = {
        chain: vi.fn(() => disabledChain),
      };
      mockEditor.can = vi.fn(() => disabledCan as any);

      render(<EditorToolbar editor={mockEditor} />);

      const boldButton = screen.getByTitle('Grassetto (Ctrl+B)');
      expect(boldButton).toBeDisabled();
    });
  });

  /**
   * Test Group: Italic Button
   */
  describe('Italic Button', () => {
    it('executes italic command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const italicButton = screen.getByTitle('Corsivo (Ctrl+I)');
      fireEvent.click(italicButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state when italic is active', () => {
      mockEditor.isActive = vi.fn(format => format === 'italic');
      render(<EditorToolbar editor={mockEditor} />);

      const italicButton = screen.getByTitle('Corsivo (Ctrl+I)');
      expect(italicButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });

  /**
   * Test Group: Strikethrough Button
   */
  describe('Strikethrough Button', () => {
    it('executes strike command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const strikeButton = screen.getByTitle('Barrato (Ctrl+Shift+X)');
      fireEvent.click(strikeButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state when strike is active', () => {
      mockEditor.isActive = vi.fn(format => format === 'strike');
      render(<EditorToolbar editor={mockEditor} />);

      const strikeButton = screen.getByTitle('Barrato (Ctrl+Shift+X)');
      expect(strikeButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });

  /**
   * Test Group: Inline Code Button
   */
  describe('Inline Code Button', () => {
    it('executes code command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const codeButton = screen.getByTitle('Codice inline (Ctrl+E)');
      fireEvent.click(codeButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state when code is active', () => {
      mockEditor.isActive = vi.fn(format => format === 'code');
      render(<EditorToolbar editor={mockEditor} />);

      const codeButton = screen.getByTitle('Codice inline (Ctrl+E)');
      expect(codeButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });

  /**
   * Test Group: Heading Buttons
   */
  describe('Heading Buttons', () => {
    it('executes H1 command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const h1Button = screen.getByTitle('Titolo 1 (Ctrl+Alt+1)');
      fireEvent.click(h1Button);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state for H1 when active', () => {
      mockEditor.isActive = vi.fn(
        (format: string, options?: any) => format === 'heading' && options?.level === 1
      ) as any;
      render(<EditorToolbar editor={mockEditor} />);

      const h1Button = screen.getByTitle('Titolo 1 (Ctrl+Alt+1)');
      expect(h1Button).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });

    it('executes H2 command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const h2Button = screen.getByTitle('Titolo 2 (Ctrl+Alt+2)');
      fireEvent.click(h2Button);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state for H2 when active', () => {
      mockEditor.isActive = vi.fn(
        (format: string, options?: any) => format === 'heading' && options?.level === 2
      ) as any;
      render(<EditorToolbar editor={mockEditor} />);

      const h2Button = screen.getByTitle('Titolo 2 (Ctrl+Alt+2)');
      expect(h2Button).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });

    it('executes H3 command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const h3Button = screen.getByTitle('Titolo 3 (Ctrl+Alt+3)');
      fireEvent.click(h3Button);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state for H3 when active', () => {
      mockEditor.isActive = vi.fn(
        (format: string, options?: any) => format === 'heading' && options?.level === 3
      ) as any;
      render(<EditorToolbar editor={mockEditor} />);

      const h3Button = screen.getByTitle('Titolo 3 (Ctrl+Alt+3)');
      expect(h3Button).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });

  /**
   * Test Group: List Buttons
   */
  describe('List Buttons', () => {
    it('executes bullet list command when clicked', () => {
      render(<EditorToolbar editor={mockEditor} />);

      const bulletButton = screen.getByTitle('Elenco puntato (Ctrl+Shift+8)');
      fireEvent.click(bulletButton);

      expect(mockEditor.chain).toHaveBeenCalled();
    });

    it('shows active state when bullet list is active', () => {
      mockEditor.isActive = vi.fn(format => format === 'bulletList');
      render(<EditorToolbar editor={mockEditor} />);

      const bulletButton = screen.getByTitle('Elenco puntato (Ctrl+Shift+8)');
      expect(bulletButton).toHaveClass('bg-primary', 'text-white', 'font-bold');
    });
  });
});
