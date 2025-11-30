import { render, screen, fireEvent } from "@testing-library/react";
import EditorToolbar from "../EditorToolbar";
import { type Editor } from "@tiptap/react";

describe("EditorToolbar", () => {
  let mockEditor: Partial<Editor>;

  beforeEach(() => {
    const createChain = (canPerform = true) => {
      const focusableActions = {
        toggleBold: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        toggleItalic: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        toggleStrike: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        toggleCode: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        toggleHeading: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        toggleBulletList: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        toggleOrderedList: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        toggleCodeBlock: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        setHorizontalRule: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        unsetAllMarks: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        undo: vi.fn(() => ({ run: vi.fn(() => canPerform) })),
        redo: vi.fn(() => ({ run: vi.fn(() => canPerform) }))
      };

      return {
        focus: vi.fn(() => focusableActions)
      };
    };

    mockEditor = {
      isActive: vi.fn(() => false),
      can: vi.fn(() => ({ chain: vi.fn(() => createChain(true)) })),
      chain: vi.fn(() => createChain(true))
    } as any;
  });

  it("renders all formatting buttons", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    expect(screen.getByTitle(/Grassetto/)).toBeInTheDocument();
    expect(screen.getByTitle(/Corsivo/)).toBeInTheDocument();
    expect(screen.getByTitle(/Barrato/)).toBeInTheDocument();
    expect(screen.getByTitle(/Codice inline/)).toBeInTheDocument();
  });

  it("renders heading buttons (H1, H2, H3)", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    expect(screen.getByTitle(/Titolo 1/)).toBeInTheDocument();
    expect(screen.getByTitle(/Titolo 2/)).toBeInTheDocument();
    expect(screen.getByTitle(/Titolo 3/)).toBeInTheDocument();
  });

  it("renders list buttons", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    expect(screen.getByTitle(/Elenco puntato/)).toBeInTheDocument();
    expect(screen.getByTitle(/Elenco numerato/)).toBeInTheDocument();
  });

  it("renders code block and horizontal rule buttons", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    expect(screen.getByTitle(/Blocco di codice/)).toBeInTheDocument();
    expect(screen.getByTitle(/Linea orizzontale/)).toBeInTheDocument();
  });

  it("renders undo/redo buttons", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    expect(screen.getByTitle(/Annulla \(Ctrl\+Z\)/)).toBeInTheDocument();
    expect(screen.getByTitle(/Ripeti \(Ctrl\+Shift\+Z\)/)).toBeInTheDocument();
  });

  it("renders clear formatting button", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    expect(screen.getByTitle(/Rimuovi formattazione/)).toBeInTheDocument();
  });

  it("calls toggleBold when bold button is clicked", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    const boldButton = screen.getByTitle(/Grassetto/);
    fireEvent.click(boldButton);

    expect(mockEditor.chain).toHaveBeenCalled();
  });

  it("calls toggleItalic when italic button is clicked", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    const italicButton = screen.getByTitle(/Corsivo/);
    fireEvent.click(italicButton);

    expect(mockEditor.chain).toHaveBeenCalled();
  });

  it("calls toggleHeading when heading button is clicked", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    const h1Button = screen.getByTitle(/Titolo 1/);
    fireEvent.click(h1Button);

    expect(mockEditor.chain).toHaveBeenCalled();
  });

  it("calls toggleBulletList when bullet list button is clicked", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    const bulletListButton = screen.getByTitle(/Elenco puntato/);
    fireEvent.click(bulletListButton);

    expect(mockEditor.chain).toHaveBeenCalled();
  });

  it("calls undo when undo button is clicked", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    const undoButton = screen.getByTitle(/Annulla/);
    fireEvent.click(undoButton);

    expect(mockEditor.chain).toHaveBeenCalled();
  });

  it("calls redo when redo button is clicked", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    const redoButton = screen.getByTitle(/Ripeti/);
    fireEvent.click(redoButton);

    expect(mockEditor.chain).toHaveBeenCalled();
  });

  it("highlights active formatting buttons", () => {
    mockEditor.isActive = vi.fn((name: string) => name === "bold");

    render(<EditorToolbar editor={mockEditor as Editor} />);

    const boldButton = screen.getByTitle(/Grassetto/);
    // Check for Tailwind classes instead of inline styles
    expect(boldButton).toHaveClass("bg-primary", "text-white", "font-bold");
  });

  it("disables buttons when editor cannot perform action", () => {
    // Create a new mock where actions return false (cannot perform)
    const createChainFalse = () => {
      const focusableActions = {
        toggleBold: vi.fn(() => ({ run: vi.fn(() => true) })),
        toggleItalic: vi.fn(() => ({ run: vi.fn(() => true) })),
        toggleStrike: vi.fn(() => ({ run: vi.fn(() => true) })),
        toggleCode: vi.fn(() => ({ run: vi.fn(() => true) })),
        toggleHeading: vi.fn(() => ({ run: vi.fn(() => true) })),
        toggleBulletList: vi.fn(() => ({ run: vi.fn(() => true) })),
        toggleOrderedList: vi.fn(() => ({ run: vi.fn(() => true) })),
        toggleCodeBlock: vi.fn(() => ({ run: vi.fn(() => true) })),
        setHorizontalRule: vi.fn(() => ({ run: vi.fn(() => true) })),
        unsetAllMarks: vi.fn(() => ({ run: vi.fn(() => true) })),
        undo: vi.fn(() => ({ run: vi.fn(() => false) })), // Cannot undo
        redo: vi.fn(() => ({ run: vi.fn(() => false) }))  // Cannot redo
      };

      return {
        focus: vi.fn(() => focusableActions)
      };
    };

    mockEditor.can = vi.fn(() => ({
      chain: vi.fn(() => createChainFalse())
    })) as any;

    render(<EditorToolbar editor={mockEditor as Editor} />);

    const undoButton = screen.getByTitle(/Annulla/);
    expect(undoButton).toBeDisabled();
    // Check Tailwind class instead of inline style
    expect(undoButton).toHaveClass("cursor-not-allowed");
  });

  it("calls unsetAllMarks when clear formatting is clicked", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    const clearButton = screen.getByTitle(/Rimuovi formattazione/);
    fireEvent.click(clearButton);

    expect(mockEditor.chain).toHaveBeenCalled();
  });

  it("shows keyboard shortcuts in tooltips", () => {
    render(<EditorToolbar editor={mockEditor as Editor} />);

    expect(screen.getByTitle(/Ctrl\+B/)).toBeInTheDocument();
    expect(screen.getByTitle(/Ctrl\+I/)).toBeInTheDocument();
    expect(screen.getByTitle(/Ctrl\+Z/)).toBeInTheDocument();
    expect(screen.getByTitle(/Ctrl\+Shift\+Z/)).toBeInTheDocument();
  });

  it("renders toolbar dividers for visual grouping", () => {
    const { container } = render(<EditorToolbar editor={mockEditor as Editor} />);

    // Dividers now use Tailwind classes (bg-gray-200)
    const dividers = container.querySelectorAll("div.bg-gray-200");
    expect(dividers.length).toBeGreaterThan(0);
  });
});