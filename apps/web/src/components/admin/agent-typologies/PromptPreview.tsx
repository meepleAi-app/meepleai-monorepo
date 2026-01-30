'use client';

import { useEffect, useState } from 'react';

import { Eye, EyeOff, Sparkles } from 'lucide-react';

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

export interface PromptPreviewProps {
  prompt: string;
  className?: string;
}

// Sample data for variable replacement
const SAMPLE_DATA: Record<string, string> = {
  '{{gameTitle}}': 'Catan',
  '{{userQuestion}}': 'Come si prepara il gioco?',
  '{{gameState}}': '{"turn": 1, "phase": "setup", "resources": {"wood": 2, "brick": 1}}',
  '{{playerName}}': 'Mario',
};

/**
 * Live preview component for typology prompts
 * Replaces template variables with sample data and displays the result
 */
export function PromptPreview({ prompt, className }: PromptPreviewProps) {
  const [isVisible, setIsVisible] = useState(true);
  const [renderedPrompt, setRenderedPrompt] = useState('');

  useEffect(() => {
    // Replace all template variables with sample data
    let rendered = prompt || '';
    Object.entries(SAMPLE_DATA).forEach(([variable, sampleValue]) => {
      // Use string replace all for security (avoid RegExp with user input)
      rendered = rendered.split(variable).join(sampleValue);
    });
    setRenderedPrompt(rendered);
  }, [prompt]);

  // Check if prompt contains any variables
  const hasVariables = Object.keys(SAMPLE_DATA).some(variable => prompt?.includes(variable));

  return (
    <Card className={className}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle className="flex items-center gap-2">
              <Sparkles className="h-5 w-5 text-primary" />
              Anteprima Prompt
            </CardTitle>
            <CardDescription>
              Visualizzazione in tempo reale del prompt con dati di esempio
            </CardDescription>
          </div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            onClick={() => setIsVisible(!isVisible)}
            className="gap-2"
          >
            {isVisible ? (
              <>
                <EyeOff className="h-4 w-4" />
                Nascondi
              </>
            ) : (
              <>
                <Eye className="h-4 w-4" />
                Mostra
              </>
            )}
          </Button>
        </div>
      </CardHeader>

      {isVisible && (
        <CardContent>
          {!prompt || prompt.trim() === '' ? (
            <div className="text-center py-8 text-muted-foreground">
              <p>Il prompt è vuoto</p>
              <p className="text-sm mt-2">
                Inizia a scrivere il prompt nell&apos;editor sopra per vederne l&apos;anteprima
              </p>
            </div>
          ) : (
            <div className="space-y-4">
              {/* Rendered prompt */}
              <div className="bg-muted/50 rounded-lg p-4 border">
                <pre className="whitespace-pre-wrap font-mono text-sm leading-relaxed">
                  {renderedPrompt}
                </pre>
              </div>

              {/* Sample data legend */}
              {hasVariables && (
                <div className="border-t pt-4">
                  <p className="text-sm font-medium mb-2">Dati di esempio utilizzati:</p>
                  <div className="grid grid-cols-2 gap-2 text-sm">
                    {Object.entries(SAMPLE_DATA).map(([variable, value]) => {
                      if (prompt.includes(variable)) {
                        return (
                          <div key={variable} className="flex gap-2">
                            <code className="px-2 py-1 bg-muted rounded text-xs font-mono">
                              {variable}
                            </code>
                            <span className="text-muted-foreground">→</span>
                            <code className="px-2 py-1 bg-primary/10 rounded text-xs font-mono">
                              {value.length > 30 ? `${value.substring(0, 30)}...` : value}
                            </code>
                          </div>
                        );
                      }
                      return null;
                    })}
                  </div>
                </div>
              )}

              {/* Info about missing variables */}
              {!hasVariables && (
                <p className="text-xs text-muted-foreground">
                  💡 Suggerimento: Aggiungi variabili come <code>{'{{gameTitle}}'}</code> per
                  personalizzare il prompt
                </p>
              )}
            </div>
          )}
        </CardContent>
      )}
    </Card>
  );
}
