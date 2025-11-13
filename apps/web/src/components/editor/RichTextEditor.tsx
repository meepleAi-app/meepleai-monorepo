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
      <div className="p-3 text-gray-400">
        Caricamento editor...
      </div>
    );
  }

  return (
    <div
      className={`
        flex flex-col h-full rounded bg-white
        ${isValid ? 'border-2 border-gray-300' : 'border-2 border-red-500'}
      `}
    >
      <EditorToolbar editor={editor} />

      <div className="flex-1 overflow-y-auto border-t border-gray-200">
        <EditorContent editor={editor} />
      </div>

      <div className="px-3 py-2 border-t border-gray-200 bg-gray-50 text-xs text-gray-600 flex justify-between items-center">
        <span>
          {characters} caratteri • {words} parole
        </span>
        <span className="text-[11px] text-gray-400">
          Usa Ctrl+Z per annullare, Ctrl+Shift+Z per ripetere
        </span>
      </div>
    </div>
  );
}