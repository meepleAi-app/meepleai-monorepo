'use client';

import React, { useCallback, useEffect, useRef } from 'react';

import { motion } from 'framer-motion';
import { X, CheckCircle, AlertCircle, Lightbulb, Settings } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { ScrollArea } from '@/components/ui/primitives/scroll-area';
import { cn } from '@/lib/utils';

import { RETRIEVAL_STRATEGIES } from './strategy-data';
import { getStrategyDetails } from './strategy-details-data';
import { StrategyExample } from './StrategyExample';
import { StrategyFlowDiagram } from './StrategyFlowDiagram';

import type { RetrievalStrategyType, RetrievalStrategy } from './types';

/**
 * Props for StrategyDetailModal component.
 */
export interface StrategyDetailModalProps {
  strategy: RetrievalStrategyType | null;
  isOpen: boolean;
  onClose: () => void;
  onConfigureClick?: (strategy: RetrievalStrategyType) => void;
}

/**
 * Color mapping for modal accents.
 */
const ACCENT_COLORS: Record<string, string> = {
  blue: 'from-blue-500/20 to-transparent',
  purple: 'from-purple-500/20 to-transparent',
  green: 'from-green-500/20 to-transparent',
  orange: 'from-orange-500/20 to-transparent',
  cyan: 'from-cyan-500/20 to-transparent',
  red: 'from-red-500/20 to-transparent',
};

const BORDER_COLORS: Record<string, string> = {
  blue: 'border-blue-500/30',
  purple: 'border-purple-500/30',
  green: 'border-green-500/30',
  orange: 'border-orange-500/30',
  cyan: 'border-cyan-500/30',
  red: 'border-red-500/30',
};

/**
 * StrategyDetailModal Component
 *
 * A comprehensive modal that displays detailed information about a
 * RAG retrieval strategy. Features:
 * - Animated open/close with scale and fade
 * - Tabbed navigation for different content sections
 * - Description, Pros/Cons, Use Cases
 * - Flow diagram visualization
 * - Example query and response
 * - Keyboard navigation (ESC to close)
 * - Focus trap for accessibility
 * - Responsive design (fullscreen on mobile)
 */
export function StrategyDetailModal({
  strategy: strategyId,
  isOpen,
  onClose,
  onConfigureClick,
}: StrategyDetailModalProps): React.JSX.Element {
  const closeButtonRef = useRef<HTMLButtonElement>(null);

  const strategy: RetrievalStrategy | null = strategyId
    ? RETRIEVAL_STRATEGIES[strategyId]
    : null;
  const details = strategyId ? getStrategyDetails(strategyId) : null;

  // Focus close button when modal opens
  useEffect(() => {
    if (isOpen && closeButtonRef.current) {
      closeButtonRef.current.focus();
    }
  }, [isOpen]);

  // Handle configure click
  const handleConfigureClick = useCallback(() => {
    if (strategyId && onConfigureClick) {
      onConfigureClick(strategyId);
    }
  }, [strategyId, onConfigureClick]);

  if (!strategy || !details) {
    return <></>;
  }

  return (
    <Dialog open={isOpen} onOpenChange={(open) => !open && onClose()}>
      <DialogContent
        className={cn(
          'max-w-3xl max-h-[90vh] p-0 gap-0 overflow-hidden',
          'sm:rounded-2xl',
          BORDER_COLORS[strategy.color] || BORDER_COLORS.blue
        )}
        aria-describedby="strategy-modal-description"
      >
        {/* Gradient accent background */}
        <div
          className={cn(
            'absolute inset-0 bg-gradient-to-b pointer-events-none',
            ACCENT_COLORS[strategy.color] || ACCENT_COLORS.blue
          )}
          aria-hidden="true"
        />

        {/* Header */}
        <DialogHeader className="relative p-6 pb-4 border-b">
          <div className="flex items-start justify-between">
            <div className="flex items-center gap-4">
              <motion.div
                initial={{ scale: 0.8, opacity: 0 }}
                animate={{ scale: 1, opacity: 1 }}
                className="text-4xl"
              >
                {strategy.icon}
              </motion.div>
              <div>
                <DialogTitle className="text-xl font-bold">
                  {details.title}
                </DialogTitle>
                <div className="flex flex-wrap gap-2 mt-2">
                  {strategy.tags.map((tag) => (
                    <Badge
                      key={tag}
                      variant={tag === 'recommended' ? 'default' : 'secondary'}
                      className="text-xs"
                    >
                      {tag}
                    </Badge>
                  ))}
                </div>
              </div>
            </div>
            <Button
              ref={closeButtonRef}
              variant="ghost"
              size="icon"
              onClick={onClose}
              className="h-8 w-8"
              aria-label="Close modal"
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </DialogHeader>

        {/* Content */}
        <ScrollArea className="flex-1 max-h-[60vh]">
          <div className="p-6" id="strategy-modal-description">
            <Tabs defaultValue="overview" className="w-full">
              <TabsList className="grid w-full grid-cols-4 mb-6">
                <TabsTrigger value="overview">Overview</TabsTrigger>
                <TabsTrigger value="proscons">Pros & Cons</TabsTrigger>
                <TabsTrigger value="flow">Flow</TabsTrigger>
                <TabsTrigger value="example">Example</TabsTrigger>
              </TabsList>

              {/* Overview Tab */}
              <TabsContent value="overview" className="space-y-6">
                <div className="space-y-4">
                  {details.description.map((paragraph, index) => (
                    <motion.p
                      key={index}
                      initial={{ opacity: 0, y: 10 }}
                      animate={{ opacity: 1, y: 0 }}
                      transition={{ delay: index * 0.1 }}
                      className="text-sm text-muted-foreground leading-relaxed"
                    >
                      {paragraph}
                    </motion.p>
                  ))}
                </div>

                {/* Use Cases */}
                <div>
                  <h4 className="text-sm font-semibold mb-3 flex items-center gap-2">
                    <Lightbulb className="h-4 w-4 text-amber-500" />
                    Best Use Cases
                  </h4>
                  <ul className="grid gap-2">
                    {details.useCases.map((useCase, index) => (
                      <motion.li
                        key={index}
                        initial={{ opacity: 0, x: -10 }}
                        animate={{ opacity: 1, x: 0 }}
                        transition={{ delay: index * 0.05 }}
                        className="text-sm text-muted-foreground flex items-center gap-2"
                      >
                        <span className="w-1.5 h-1.5 rounded-full bg-muted-foreground/50" />
                        {useCase}
                      </motion.li>
                    ))}
                  </ul>
                </div>

                {/* Technical Notes */}
                {details.technicalNotes && details.technicalNotes.length > 0 && (
                  <div className="p-4 rounded-lg bg-muted/50 border border-border/50">
                    <h4 className="text-xs font-semibold text-muted-foreground mb-2 uppercase tracking-wider">
                      Technical Notes
                    </h4>
                    <ul className="space-y-1">
                      {details.technicalNotes.map((note, index) => (
                        <li key={index} className="text-xs text-muted-foreground">
                          • {note}
                        </li>
                      ))}
                    </ul>
                  </div>
                )}
              </TabsContent>

              {/* Pros & Cons Tab */}
              <TabsContent value="proscons" className="space-y-6">
                <div className="grid gap-6 md:grid-cols-2">
                  {/* Pros */}
                  <div>
                    <h4 className="text-sm font-semibold mb-3 flex items-center gap-2 text-green-500">
                      <CheckCircle className="h-4 w-4" />
                      Advantages
                    </h4>
                    <ul className="space-y-2">
                      {details.pros.map((pro, index) => (
                        <motion.li
                          key={index}
                          initial={{ opacity: 0, x: -10 }}
                          animate={{ opacity: 1, x: 0 }}
                          transition={{ delay: index * 0.05 }}
                          className="text-sm text-muted-foreground flex items-start gap-2"
                        >
                          <CheckCircle className="h-3.5 w-3.5 text-green-500 mt-0.5 flex-shrink-0" />
                          {pro}
                        </motion.li>
                      ))}
                    </ul>
                  </div>

                  {/* Cons */}
                  <div>
                    <h4 className="text-sm font-semibold mb-3 flex items-center gap-2 text-amber-500">
                      <AlertCircle className="h-4 w-4" />
                      Considerations
                    </h4>
                    <ul className="space-y-2">
                      {details.cons.map((con, index) => (
                        <motion.li
                          key={index}
                          initial={{ opacity: 0, x: -10 }}
                          animate={{ opacity: 1, x: 0 }}
                          transition={{ delay: index * 0.05 }}
                          className="text-sm text-muted-foreground flex items-start gap-2"
                        >
                          <AlertCircle className="h-3.5 w-3.5 text-amber-500 mt-0.5 flex-shrink-0" />
                          {con}
                        </motion.li>
                      ))}
                    </ul>
                  </div>
                </div>
              </TabsContent>

              {/* Flow Tab */}
              <TabsContent value="flow" className="space-y-6">
                <div>
                  <h4 className="text-sm font-semibold mb-4">Processing Flow</h4>
                  <StrategyFlowDiagram steps={details.flowSteps} />
                </div>
              </TabsContent>

              {/* Example Tab */}
              <TabsContent value="example" className="space-y-6">
                <div>
                  <h4 className="text-sm font-semibold mb-4">Example Interaction</h4>
                  <StrategyExample
                    example={details.example}
                    strategyColor={strategy.color}
                  />
                </div>
              </TabsContent>
            </Tabs>
          </div>
        </ScrollArea>

        {/* Footer */}
        <div className="relative p-4 border-t bg-muted/30">
          <div className="flex items-center justify-between">
            <div className="text-xs text-muted-foreground">
              <span className="font-medium">Metrics:</span>{' '}
              Latency {strategy.metrics.latencyMs.display} •{' '}
              Accuracy {strategy.metrics.accuracyPercent.display} •{' '}
              Cost {strategy.metrics.costPerQuery.display}
            </div>
            {onConfigureClick && (
              <Button
                variant="outline"
                size="sm"
                onClick={handleConfigureClick}
                className="gap-2"
              >
                <Settings className="h-3.5 w-3.5" />
                Configure
              </Button>
            )}
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
