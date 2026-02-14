/**
 * Agent Config Sheet - Main Configuration Container
 * Issue #3238 (FRONT-002)
 * Issue #3375 - Agent Session Launch API Integration
 * Issue #3376 - Added Strategy, ModelTier, CostPreview
 *
 * Responsive layout:
 * - Mobile (0-640px): Bottom sheet, 90vh
 * - Tablet (641-1024px): Right drawer, 500px
 * - Desktop (1025px+): Right drawer, 600px
 *
 * State machine: 'closed' | 'config' | 'template-info' | 'model-pricing'
 */

'use client';

import { useState } from 'react';

import { ArrowLeft, HelpCircle } from 'lucide-react';

import {
  Sheet,
  SheetContent,
  SheetHeader,
  SheetTitle,
  SheetFooter,
} from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';

import { CostPreview } from './CostPreview';
import { GameSelector } from './GameSelector';
import { ModelTierSelector } from './ModelTierSelector';
import { SlotCards } from './SlotCards';
import { StrategySelector } from './StrategySelector';
import { TemplateCarousel } from './TemplateCarousel';
import { TokenQuotaDisplay } from './TokenQuotaDisplay';

interface AgentConfigSheetProps {
  isOpen: boolean;
  onClose: () => void;
  gameId: string;
  gameTitle: string;
}

type ViewState = 'config' | 'template-info' | 'model-pricing';

export function AgentConfigSheet({
  isOpen,
  onClose,
  gameId,
  gameTitle,
}: AgentConfigSheetProps) {
  const [view, setView] = useState<ViewState>('config');

  const isLaunching = false;

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

              {/* Strategy Selection - Issue #3 */}
              <StrategySelector onChange={() => {}} />

              {/* Template Carousel - Issue #3239 */}
              <TemplateCarousel />

              {/* Model Tier Selection - Issue #3376 */}
              <ModelTierSelector userTier="free" />

              {/* Cost Preview - Issue #3376 */}
              <CostPreview />

              {/* Token Quota - Issue #3240 */}
              <TokenQuotaDisplay />

              {/* Slot Cards - Issue #3240 */}
              <SlotCards />
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

        {/* Footer */}
        <SheetFooter className="border-t border-slate-800 pt-4">
          <div className="w-full flex gap-3 justify-end">
            <Button variant="outline" onClick={onClose} disabled={isLaunching}>
              Cancel
            </Button>
            <Button
              onClick={() => {
                // Navigate to unified chat with game context
                window.location.href = `/chat/new?gameId=${gameId}`;
              }}
              disabled={isLaunching}
            >
              Start Chat
            </Button>
          </div>
        </SheetFooter>
      </SheetContent>
    </Sheet>
  );
}
