import { render, screen, fireEvent } from "@testing-library/react";
import EditorToolbar from "../EditorToolbar";
import { type Editor } from "@tiptap/react";

describe("EditorToolbar", () => {
  let mockEditor: Partial<Editor>;

  beforeEach(() => {
    const createChain = (canPerform = true) => {
      const focusableActions = {
        toggleBold: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        toggleItalic: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        toggleStrike: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        toggleCode: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        toggleHeading: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        toggleBulletList: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        toggleOrderedList: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        toggleCodeBlock: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        setHorizontalRule: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        unsetAllMarks: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        undo: jest.fn(() => ({ run: jest.fn(() => canPerform) })),
        redo: jest.fn(() => ({ run: jest.fn(() => canPerform) }))
      };

      return {
        focus: jest.fn(() => focusableActions)
      };
    };

    mockEditor = {
      isActive: jest.fn(() => false),
      can: jest.fn(() => ({ chain: jest.fn(() => createChain(true)) })),
      chain: jest.fn(() => createChain(true))
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
    mockEditor.isActive = jest.fn((name: string) => name === "bold");

    render(<EditorToolbar editor={mockEditor as Editor} />);

    const boldButton = screen.getByTitle(/Grassetto/);
    // Check for the hex color and white text
    expect(boldButton).toHaveStyle({ background: "#0070f3" });
    expect(boldButton).toHaveStyle({ fontWeight: "bold" });
  });

  it("disables buttons when editor cannot perform action", () => {
    // Create a new mock where actions return false (cannot perform)
    const createChainFalse = () => {
      const focusableActions = {
        toggleBold: jest.fn(() => ({ run: jest.fn(() => true) })),
        toggleItalic: jest.fn(() => ({ run: jest.fn(() => true) })),
        toggleStrike: jest.fn(() => ({ run: jest.fn(() => true) })),
        toggleCode: jest.fn(() => ({ run: jest.fn(() => true) })),
        toggleHeading: jest.fn(() => ({ run: jest.fn(() => true) })),
        toggleBulletList: jest.fn(() => ({ run: jest.fn(() => true) })),
        toggleOrderedList: jest.fn(() => ({ run: jest.fn(() => true) })),
        toggleCodeBlock: jest.fn(() => ({ run: jest.fn(() => true) })),
        setHorizontalRule: jest.fn(() => ({ run: jest.fn(() => true) })),
        unsetAllMarks: jest.fn(() => ({ run: jest.fn(() => true) })),
        undo: jest.fn(() => ({ run: jest.fn(() => false) })), // Cannot undo
        redo: jest.fn(() => ({ run: jest.fn(() => false) }))  // Cannot redo
      };

      return {
        focus: jest.fn(() => focusableActions)
      };
    };

    mockEditor.can = jest.fn(() => ({
      chain: jest.fn(() => createChainFalse())
    }));

    render(<EditorToolbar editor={mockEditor as Editor} />);

    const undoButton = screen.getByTitle(/Annulla/);
    expect(undoButton).toBeDisabled();
    expect(undoButton).toHaveStyle({ cursor: "not-allowed" });
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

    // Dividers are rendered as div elements with specific styling
    const dividers = container.querySelectorAll("div[style*='background']");
    expect(dividers.length).toBeGreaterThan(0);
  });
});
