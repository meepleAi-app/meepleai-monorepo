'use client';

import { useRef, useEffect } from 'react';

import Editor, { type Monaco } from '@monaco-editor/react';

export interface TypologyPromptEditorProps {
  value: string;
  onChange?: (value: string) => void;
  readonly?: boolean;
  height?: string;
  maxLength?: number;
  onValidationError?: (error: string | null) => void;
}

// Available template variables for autocomplete
const TEMPLATE_VARIABLES = [
  {
    label: '{{gameTitle}}',
    detail: 'Nome del gioco',
    documentation: 'Il titolo completo del gioco da tavolo',
  },
  {
    label: '{{userQuestion}}',
    detail: 'Domanda dell\'utente',
    documentation: 'La domanda posta dall\'utente all\'assistente AI',
  },
  {
    label: '{{gameState}}',
    detail: 'Stato partita (JSON)',
    documentation: 'Lo stato corrente della partita in formato JSON (solo per sessioni)',
  },
  {
    label: '{{playerName}}',
    detail: 'Nome giocatore',
    documentation: 'Il nome del giocatore corrente',
  },
];

/**
 * Specialized Monaco editor for typology prompts with template variable autocomplete
 */
export function TypologyPromptEditor({
  value,
  onChange,
  readonly = false,
  height = '300px',
  maxLength = 5000,
  onValidationError,
}: TypologyPromptEditorProps) {
  const editorRef = useRef<Parameters<NonNullable<Parameters<typeof Editor>[0]['onMount']>>[0] | null>(
    null
  );
  const monacoRef = useRef<Monaco | null>(null);
  const disposablesRef = useRef<Array<{ dispose: () => void }>>([]);

  // Handle editor mount and setup autocomplete
  const handleEditorDidMount = (
    editor: Parameters<NonNullable<Parameters<typeof Editor>[0]['onMount']>>[0],
    monaco: Monaco
  ) => {
    editorRef.current = editor;
    monacoRef.current = monaco;

    // Register completion provider for template variables
    const completionDisposable = monaco.languages.registerCompletionItemProvider('handlebars', {
      triggerCharacters: ['{'],
      provideCompletionItems: (model, position) => {
        const textUntilPosition = model.getValueInRange({
          startLineNumber: position.lineNumber,
          startColumn: 1,
          endLineNumber: position.lineNumber,
          endColumn: position.column,
        });

        // Check if we're typing {{ pattern
        if (textUntilPosition.endsWith('{{') || textUntilPosition.endsWith('{')) {
          const word = model.getWordUntilPosition(position);
          const range = {
            startLineNumber: position.lineNumber,
            endLineNumber: position.lineNumber,
            startColumn: word.startColumn,
            endColumn: word.endColumn,
          };

          return {
            suggestions: TEMPLATE_VARIABLES.map(variable => ({
              label: variable.label,
              kind: monaco.languages.CompletionItemKind.Variable,
              detail: variable.detail,
              documentation: variable.documentation,
              insertText: variable.label,
              range: range,
            })),
          };
        }

        return { suggestions: [] };
      },
    });
    disposablesRef.current.push(completionDisposable);

    // Setup validation
    const validateContent = () => {
      const content = editor.getValue();
      if (content.length > maxLength) {
        onValidationError?.(
          `Il prompt supera il limite di ${maxLength} caratteri (${content.length}/${maxLength})`
        );
      } else {
        onValidationError?.(null);
      }
    };

    const contentDisposable = editor.onDidChangeModelContent(validateContent);
    disposablesRef.current.push(contentDisposable);
    validateContent(); // Initial validation
  };

  // Cleanup on unmount
  useEffect(() => {
    return () => {
      disposablesRef.current.forEach(disposable => disposable.dispose());
      disposablesRef.current = [];
    };
  }, []);

  // Handle editor change
  const handleEditorChange = (value: string | undefined) => {
    if (onChange && !readonly && value !== undefined) {
      onChange(value);
    }
  };

  // Character counter display
  const charCount = value?.length || 0;
  const isOverLimit = charCount > maxLength;

  return (
    <div className="space-y-2">
      <div className="border rounded-md overflow-hidden">
        <Editor
          height={height}
          defaultLanguage="handlebars"
          value={value}
          onChange={handleEditorChange}
          onMount={handleEditorDidMount}
          theme="vs-light"
          options={{
            readOnly: readonly,
            minimap: { enabled: false },
            lineNumbers: 'on',
            fontSize: 14,
            wordWrap: 'on',
            scrollBeyondLastLine: false,
            automaticLayout: true,
            padding: { top: 10, bottom: 10 },
            renderWhitespace: 'selection',
            folding: true,
            lineDecorationsWidth: 10,
            lineNumbersMinChars: 3,
            suggestOnTriggerCharacters: true,
            quickSuggestions: true,
            tabCompletion: 'on',
          }}
          loading={
            <div className="flex items-center justify-center h-full">
              <div className="text-muted-foreground">Caricamento editor...</div>
            </div>
          }
        />
      </div>

      {/* Character counter */}
      <div className="flex justify-between items-center text-sm">
        <div className="text-muted-foreground">
          <span className="font-medium">Variabili disponibili:</span>{' '}
          {TEMPLATE_VARIABLES.map(v => v.label).join(', ')}
        </div>
        <div className={isOverLimit ? 'text-destructive font-medium' : 'text-muted-foreground'}>
          {charCount} / {maxLength} caratteri
        </div>
      </div>

      {/* Hint about autocomplete */}
      {!readonly && (
        <p className="text-xs text-muted-foreground">
          💡 Suggerimento: Digita <code className="px-1 py-0.5 bg-muted rounded">{'{'}</code> per
          visualizzare le variabili disponibili
        </p>
      )}
    </div>
  );
}
