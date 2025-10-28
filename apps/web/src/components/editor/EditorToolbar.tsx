import { type Editor } from "@tiptap/react";

type EditorToolbarProps = {
  editor: Editor;
};

type ToolbarButtonProps = {
  onClick: () => void;
  isActive?: boolean;
  disabled?: boolean;
  title: string;
  children: React.ReactNode;
};

function ToolbarButton({ onClick, isActive, disabled, title, children }: ToolbarButtonProps) {
  return (
    <button
      onClick={onClick}
      disabled={disabled}
      title={title}
      style={{
        padding: "6px 10px",
        background: isActive ? "#0070f3" : disabled ? "#f0f0f0" : "white",
        color: isActive ? "white" : disabled ? "#999" : "#333",
        border: "1px solid #e0e0e0",
        borderRadius: 4,
        cursor: disabled ? "not-allowed" : "pointer",
        fontSize: 14,
        fontWeight: isActive ? "bold" : "normal",
        transition: "all 0.15s ease",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        minWidth: 32
      }}
      onMouseEnter={(e) => {
        if (!disabled && !isActive) {
          e.currentTarget.style.background = "#f5f5f5";
        }
      }}
      onMouseLeave={(e) => {
        if (!isActive) {
          e.currentTarget.style.background = disabled ? "#f0f0f0" : "white";
        }
      }}
    >
      {children}
    </button>
  );
}

function ToolbarDivider() {
  return (
    <div
      style={{
        width: 1,
        height: 24,
        background: "#e0e0e0",
        margin: "0 8px"
      }}
    />
  );
}

/**
 * EditorToolbar Component
 *
 * Toolbar with formatting controls for the rich text editor.
 * Provides buttons for text formatting, lists, code blocks, headings, and undo/redo.
 *
 * Features:
 * - Bold, italic, underline, strikethrough
 * - Ordered and unordered lists
 * - Code blocks and inline code
 * - Headings (H1-H6)
 * - Undo/redo with keyboard shortcut tooltips
 * - Visual feedback for active formatting
 *
 * @param editor - TipTap editor instance
 */
export default function EditorToolbar({ editor }: EditorToolbarProps) {
  return (
    <div
      style={{
        padding: "8px 12px",
        background: "#fafafa",
        borderBottom: "1px solid #e0e0e0",
        display: "flex",
        flexWrap: "wrap",
        gap: 4,
        alignItems: "center"
      }}
    >
      {/* Text Formatting */}
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleBold().run()}
        isActive={editor.isActive("bold")}
        disabled={!editor.can().chain().focus().toggleBold().run()}
        title="Grassetto (Ctrl+B)"
      >
        <strong>B</strong>
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleItalic().run()}
        isActive={editor.isActive("italic")}
        disabled={!editor.can().chain().focus().toggleItalic().run()}
        title="Corsivo (Ctrl+I)"
      >
        <em>I</em>
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleStrike().run()}
        isActive={editor.isActive("strike")}
        disabled={!editor.can().chain().focus().toggleStrike().run()}
        title="Barrato (Ctrl+Shift+X)"
      >
        <span style={{ textDecoration: "line-through" }}>S</span>
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleCode().run()}
        isActive={editor.isActive("code")}
        disabled={!editor.can().chain().focus().toggleCode().run()}
        title="Codice inline (Ctrl+E)"
      >
        <span style={{ fontFamily: "monospace" }}>&lt;/&gt;</span>
      </ToolbarButton>

      <ToolbarDivider />

      {/* Headings */}
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleHeading({ level: 1 }).run()}
        isActive={editor.isActive("heading", { level: 1 })}
        title="Titolo 1 (Ctrl+Alt+1)"
      >
        H1
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleHeading({ level: 2 }).run()}
        isActive={editor.isActive("heading", { level: 2 })}
        title="Titolo 2 (Ctrl+Alt+2)"
      >
        H2
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleHeading({ level: 3 }).run()}
        isActive={editor.isActive("heading", { level: 3 })}
        title="Titolo 3 (Ctrl+Alt+3)"
      >
        H3
      </ToolbarButton>

      <ToolbarDivider />

      {/* Lists */}
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleBulletList().run()}
        isActive={editor.isActive("bulletList")}
        title="Elenco puntato (Ctrl+Shift+8)"
      >
        • Lista
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleOrderedList().run()}
        isActive={editor.isActive("orderedList")}
        title="Elenco numerato (Ctrl+Shift+7)"
      >
        1. Lista
      </ToolbarButton>

      <ToolbarDivider />

      {/* Code Block */}
      <ToolbarButton
        onClick={() => editor.chain().focus().toggleCodeBlock().run()}
        isActive={editor.isActive("codeBlock")}
        title="Blocco di codice (Ctrl+Alt+C)"
      >
        <span style={{ fontFamily: "monospace" }}>{"{ }"}</span>
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().setHorizontalRule().run()}
        title="Linea orizzontale"
      >
        ―
      </ToolbarButton>

      <ToolbarDivider />

      {/* Undo/Redo */}
      <ToolbarButton
        onClick={() => editor.chain().focus().undo().run()}
        disabled={!editor.can().chain().focus().undo().run()}
        title="Annulla (Ctrl+Z)"
      >
        ↶
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().redo().run()}
        disabled={!editor.can().chain().focus().redo().run()}
        title="Ripeti (Ctrl+Shift+Z)"
      >
        ↷
      </ToolbarButton>

      <ToolbarDivider />

      {/* Clear Formatting */}
      <ToolbarButton
        onClick={() => editor.chain().focus().unsetAllMarks().run()}
        title="Rimuovi formattazione"
      >
        ✕
      </ToolbarButton>
    </div>
  );
}
