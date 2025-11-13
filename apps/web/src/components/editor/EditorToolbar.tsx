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
      className={`
        px-2.5 py-1.5 border border-gray-200 rounded text-sm min-w-8
        flex items-center justify-center transition-all duration-150
        ${isActive ? 'bg-primary text-white font-bold' : ''}
        ${disabled ? 'bg-gray-100 text-gray-400 cursor-not-allowed' : 'bg-white text-gray-700 cursor-pointer'}
        ${!disabled && !isActive ? 'hover:bg-gray-100' : ''}
      `}
    >
      {children}
    </button>
  );
}

function ToolbarDivider() {
  return <div className="w-px h-6 bg-gray-200 mx-2" />;
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
    <div className="px-3 py-2 bg-gray-50 border-b border-gray-200 flex flex-wrap gap-1 items-center">
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
        <span className="line-through">S</span>
      </ToolbarButton>

      <ToolbarButton
        onClick={() => editor.chain().focus().toggleCode().run()}
        isActive={editor.isActive("code")}
        disabled={!editor.can().chain().focus().toggleCode().run()}
        title="Codice inline (Ctrl+E)"
      >
        <span className="font-mono">&lt;/&gt;</span>
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
        <span className="font-mono">{"{ }"}</span>
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