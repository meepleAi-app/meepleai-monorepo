/**
 * Agent Config Sheet - Main Configuration Container
 * Issue #3238 (FRONT-002)
 *
 * Responsive layout:
 * - Mobile (0-640px): Bottom sheet, 90vh
 * - Tablet (641-1024px): Right drawer, 500px
 * - Desktop (1025px+): Right drawer, 600px
 *
 * State machine: 'closed' | 'config' | 'template-info' | 'model-pricing'
 */

'use client';

import { ArrowLeft, HelpCircle } from 'lucide-react';
import { useState } from 'react';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { GameSelector } from './GameSelector';
import { TemplateCarousel } from './TemplateCarousel';
import { ModelSelector } from './ModelSelector';

interface AgentConfigSheetProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string;
  gameTitle: string;
}

type ViewState = 'config' | 'template-info' | 'model-pricing';

export function AgentConfigSheet({ isOpen, onClose, gameId, gameTitle }: AgentConfigSheetProps) {
  const [view, setView] = useState<ViewState>('config');

  const handleBack = () => {
    if (view === 'template-info' || view === 'model-pricing') {
      setView('config');
    } else {
      onClose();
    }
  };

  const showBackButton = view !== 'config';

  return (
    <Sheet open={isOpen} onOpenChange={open => !open && onClose()}>
      <SheetContent
        side="bottom"
        className="h-[90vh] sm:h-auto sm:max-h-[90vh] md:h-screen md:max-w-[500px] lg:max-w-[600px] flex flex-col"
      >
        {/* Header */}
        <SheetHeader className="border-b border-slate-800 pb-4">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-3">
              {showBackButton && (
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={handleBack}
                  className="h-8 w-8"
                  aria-label="Back"
                >
                  <ArrowLeft className="h-4 w-4" />
                </Button>
              )}
              <SheetTitle className="agent-heading text-xl">
                {view === 'config' && 'Configure Agent'}
                {view === 'template-info' && 'Template Info'}
                {view === 'model-pricing' && 'Model Pricing'}
              </SheetTitle>
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8"
              aria-label="Help"
            >
              <HelpCircle className="h-4 w-4 text-slate-400" />
            </Button>
          </div>
          <p className="text-sm text-slate-400 mt-1">{gameTitle}</p>
        </SheetHeader>

        {/* Content Area - Scrollable */}
        <div className="flex-1 overflow-y-auto py-6 px-1">
          {view === 'config' && (
            <div className="space-y-6">
              {/* Game Selection - Issue #3239 */}
              <GameSelector />

              {/* Template Carousel - Issue #3239 */}
              <TemplateCarousel />

              {/* Model Selection - Issue #3239 */}
              <ModelSelector />

              {/* Token Quota - Placeholder for #3240 */}
              <div className="rounded-lg border border-slate-800 bg-slate-900/50 p-6">
                <h3 className="mb-2 font-semibold text-slate-200">Token Quota</h3>
                <p className="text-sm text-slate-400">
                  Component from #3240 (FRONT-004)
                </p>
              </div>

              {/* Slot Cards - Placeholder for #3240 */}
              <div className="rounded-lg border border-slate-800 bg-slate-900/50 p-6">
                <h3 className="mb-2 font-semibold text-slate-200">Slot Cards</h3>
                <p className="text-sm text-slate-400">
                  Component from #3240 (FRONT-004)
                </p>
              </div>
            </div>
          )}

          {view === 'template-info' && (
            <div className="space-y-4">
              <h3 className="text-lg font-semibold">Template Information</h3>
              <p className="text-sm text-slate-400">
                Detailed template descriptions will be shown here.
              </p>
            </div>
          )}

          {view === 'model-pricing' && (
            <div className="space-y-4">
              <h3 className="text-lg font-semibold">Model Pricing</h3>
              <p className="text-sm text-slate-400">
                Pricing comparison for different AI models.
              </p>
            </div>
          )}
        </div>

        {/* Footer - Action Bar */}
        <SheetFooter className="border-t border-slate-800 pt-4">
          <div className="w-full space-y-2">
            {/* Placeholder for #3241 (Action Bar) */}
            <div className="rounded-lg border border-cyan-500/30 bg-cyan-500/10 p-4">
              <p className="text-center text-sm text-slate-400">
                Contextual Action Bar (Issue #3241)
              </p>
            </div>
          </div>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
