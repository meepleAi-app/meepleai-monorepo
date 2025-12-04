import React from 'react';
import Editor from '@monaco-editor/react';

export interface PromptEditorProps {
  value: string;
  onChange?: (value: string | undefined) => void;
  readonly?: boolean;
  height?: string;
  language?: string;
  placeholder?: string;
}

/**
 * Reusable Monaco Editor wrapper component for prompt editing
 * Supports markdown syntax highlighting and readonly mode
 */
export default function PromptEditor({
  value,
  onChange,
  readonly = false,
  height = '400px',
  language = 'markdown',
  placeholder = 'Enter your prompt here...',
}: PromptEditorProps) {
  const handleEditorChange = (value: string | undefined) => {
    if (onChange && !readonly) {
      onChange(value);
    }
  };

  return (
    <div className="border border-gray-300 rounded-md overflow-hidden">
      <Editor
        height={height}
        defaultLanguage={language}
        value={value}
        onChange={handleEditorChange}
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
        }}
        loading={
          <div className="flex items-center justify-center h-full">
            <div className="text-gray-500">Loading editor...</div>
          </div>
        }
      />
      {!value && !readonly && (
        <div
          className="absolute top-12 left-16 text-gray-400 pointer-events-none"
          style={{ fontSize: '14px' }}
        >
          {placeholder}
        </div>
      )}
    </div>
  );
}
