import { useCallback, useEffect } from "react";
import { EditorContent, useEditor } from "@tiptap/react";
import StarterKit from "@tiptap/starter-kit";
import Placeholder from "@tiptap/extension-placeholder";
import CharacterCount from "@tiptap/extension-character-count";
import EditorToolbar from "./EditorToolbar";

type RichTextEditorProps = {
  content: string;
  onChange: (content: string) => void;
  onBlur?: () => void;
  placeholder?: string;
  isValid?: boolean;
  autoFocus?: boolean;
};

/**
 * RichTextEditor Component
 *
 * A WYSIWYG rich text editor using TipTap for editing game rules.
 * Features: formatting toolbar, auto-save support, undo/redo, keyboard shortcuts
 *
 * @param content - JSON content to edit (will be converted to HTML)
 * @param onChange - Callback when content changes (receives HTML)
 * @param onBlur - Callback when editor loses focus
 * @param placeholder - Placeholder text for empty editor
 * @param isValid - Whether the content is valid (affects styling)
 * @param autoFocus - Whether to focus the editor on mount
 */
export default function RichTextEditor({
  content,
  onChange,
  onBlur,
  placeholder = "Inizia a scrivere le regole del gioco...",
  isValid = true,
  autoFocus = false
}: RichTextEditorProps) {
  const editor = useEditor({
    extensions: [
      StarterKit.configure({
        heading: {
          levels: [1, 2, 3, 4, 5, 6]
        },
        codeBlock: {
          HTMLAttributes: {
            class: "code-block"
          }
        }
      }),
      Placeholder.configure({
        placeholder
      }),
      CharacterCount
    ],
    content,
    autofocus: autoFocus,
    editable: true,
    onUpdate: ({ editor }) => {
      // Convert editor content to HTML and pass to parent
      const html = editor.getHTML();
      onChange(html);
    },
    onBlur: () => {
      if (onBlur) {
        onBlur();
      }
    },
    editorProps: {
      attributes: {
        class: "prose prose-sm max-w-none focus:outline-none",
        style: "min-height: 600px; padding: 12px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; font-size: 14px; line-height: 1.6;"
      }
    }
  });

  // Update editor content when prop changes (external updates)
  useEffect(() => {
    if (editor && content !== editor.getHTML()) {
      editor.commands.setContent(content);
    }
  }, [content, editor]);

  const getCharacterCount = useCallback(() => {
    if (!editor) return { characters: 0, words: 0 };
    const characters = editor.storage.characterCount.characters();
    const words = editor.storage.characterCount.words();
    return { characters, words };
  }, [editor]);

  const { characters, words } = getCharacterCount();

  if (!editor) {
    return (
      <div style={{ padding: 12, color: "#999" }}>
        Caricamento editor...
      </div>
    );
  }

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        height: "100%",
        border: `2px solid ${isValid ? "#ccc" : "#d93025"}`,
        borderRadius: 4,
        background: "white"
      }}
    >
      <EditorToolbar editor={editor} />

      <div
        style={{
          flex: 1,
          overflowY: "auto",
          borderTop: "1px solid #e0e0e0"
        }}
      >
        <EditorContent editor={editor} />
      </div>

      <div
        style={{
          padding: "8px 12px",
          borderTop: "1px solid #e0e0e0",
          background: "#f9f9f9",
          fontSize: 12,
          color: "#666",
          display: "flex",
          justifyContent: "space-between",
          alignItems: "center"
        }}
      >
        <span>
          {characters} caratteri • {words} parole
        </span>
        <span style={{ fontSize: 11, color: "#999" }}>
          Usa Ctrl+Z per annullare, Ctrl+Shift+Z per ripetere
        </span>
      </div>
    </div>
  );
}
