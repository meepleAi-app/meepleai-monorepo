/* eslint-disable local/no-hardcoded-color-utility -- text-white / button color on style-prop colored bg or entity-colored CTA; mockup .e-bg pattern. DS-12 will introduce primitives encoding bg via className. */
/**
 * Template Info Modal - Display template details
 * Issue #3239 (FRONT-003)
 */

'use client';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
  DialogFooter,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';

interface Template {
  id: string;
  name: string;
  description: string;
  systemPrompt: string;
  defaultStrategy: string;
  exampleQuestions?: string[];
}

interface TemplateInfoModalProps {
  template: Template | null;
  isOpen: boolean;
  onClose: () => void;
}

export function TemplateInfoModal({ template, isOpen, onClose }: TemplateInfoModalProps) {
  if (!template) return null;

  return (
    <Dialog open={isOpen} onOpenChange={onClose}>
      <DialogContent className="sm:max-w-[500px] bg-card border-border">
        <DialogHeader>
          <DialogTitle className="text-xl font-bold text-cyan-400">
            {template.name}
          </DialogTitle>
        </DialogHeader>

        <div className="space-y-4">
          <div>
            <h3 className="text-sm font-semibold text-slate-200 mb-1">Description</h3>
            <p className="text-sm text-muted-foreground">{template.description}</p>
          </div>

          <div>
            <h3 className="text-sm font-semibold text-slate-200 mb-1">Base Prompt (Preview)</h3>
            <p className="text-xs text-muted-foreground font-mono bg-card p-3 rounded border border-border">
              {template.systemPrompt.substring(0, 150)}...
            </p>
          </div>

          <div>
            <h3 className="text-sm font-semibold text-slate-200 mb-1">Default Strategy</h3>
            <p className="text-sm text-muted-foreground">{template.defaultStrategy}</p>
          </div>

          {template.exampleQuestions && template.exampleQuestions.length > 0 && (
            <div>
              <h3 className="text-sm font-semibold text-slate-200 mb-1">Example Questions</h3>
              <ul className="space-y-1">
                {template.exampleQuestions.map((q, i) => (
                  <li key={i} className="text-sm text-muted-foreground">
                    • {q}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>

        <DialogFooter>
          <Button onClick={onClose} className="w-full">
            Got it
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
