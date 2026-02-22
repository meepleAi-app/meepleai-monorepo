'use client';

/**
 * Add Game to Collection Wizard
 * Issue #3477, #3650: Multi-step wizard for adding games to user's personal collection
 *
 * 4-step wizard (Step 2 conditional):
 * 1. Search/Select Game → SharedGameCatalog OR create custom
 * 2. Game Details → Only if custom game
 * 3. Upload Private PDF → Optional
 * 4. Review & Confirm → Submit to UserLibrary
 *
 * UX Features:
 * - Animated step transitions
 * - Progress indicator with visual feedback
 * - Keyboard navigation support
 * - Screen reader announcements
 */

import { useCallback, useEffect, useState, useRef } from 'react';

import Link from 'next/link';

import { Card } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { useAddGameWizard } from '@/hooks/useAddGameWizard';
import { useTranslation } from '@/hooks/useTranslation';
import { WIZARD_TEST_IDS } from '@/lib/test-ids';

// Step components
import { GameDetailsForm } from './steps/GameDetailsForm';
import { ReviewConfirm } from './steps/ReviewConfirm';
import { SearchSelectGame } from './steps/SearchSelectGame';
import { UploadPrivatePDF } from './steps/UploadPrivatePDF';

export function AddGameWizard() {
  const { t } = useTranslation();
  const {
    step,
    allSteps,
    shouldShowStep2,
    reviewSummary,
    error,
    reset,
  } = useAddGameWizard();

  // Animation state
  const [isTransitioning, setIsTransitioning] = useState(false);
  const [direction, setDirection] = useState<'forward' | 'backward'>('forward');
  const prevStepRef = useRef(step);
  const stepContentRef = useRef<HTMLDivElement>(null);

  // Track step changes for animation direction
  useEffect(() => {
    if (step !== prevStepRef.current) {
      setDirection(step > prevStepRef.current ? 'forward' : 'backward');
      setIsTransitioning(true);

      // Reset transition state after animation
      const timer = setTimeout(() => {
        setIsTransitioning(false);
      }, 300);

      prevStepRef.current = step;
      return () => clearTimeout(timer);
    }
  }, [step]);

  // Focus management for accessibility
  useEffect(() => {
    if (stepContentRef.current && !isTransitioning) {
      // Focus the step content for screen readers
      stepContentRef.current.focus();
    }
  }, [step, isTransitioning]);

  // Calculate visual step index (skip Step 2 in UI if not custom game)
  const getVisualStepIndex = useCallback(() => {
    if (!shouldShowStep2) {
      // When Step 2 hidden: 1→0, 3→1, 4→2
      if (step === 1) return 0;
      if (step === 3) return 1;
      if (step === 4) return 2;
    }
    // Normal: 1→0, 2→1, 3→2, 4→3
    return step - 1;
  }, [step, shouldShowStep2]);

  const visualSteps = shouldShowStep2
    ? allSteps
    : allSteps.filter(s => s.number !== 2); // Hide Step 2 from UI

  const visualStepIndex = getVisualStepIndex();

  // Animation classes for step transitions
  const getStepAnimationClass = () => {
    if (!isTransitioning) return 'opacity-100 translate-x-0';
    return direction === 'forward'
      ? 'opacity-0 -translate-x-4'
      : 'opacity-0 translate-x-4';
  };

  return (
    <div className="min-h-dvh bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800">
      <div className="max-w-4xl mx-auto px-4 py-8">
        {/* Header */}
        <div className="flex items-center justify-between mb-8">
          <div>
            <h1
              className="text-3xl font-bold text-slate-900 dark:text-white mb-2"
              data-testid={WIZARD_TEST_IDS.title}
            >
              {t('collection.addGameTitle')}
            </h1>
            <p className="text-slate-600 dark:text-slate-400" data-testid={WIZARD_TEST_IDS.subtitle}>
              {t('collection.addGameSubtitle')}
            </p>
          </div>
          <Button variant="outline" onClick={reset} asChild>
            <Link href="/library">{t('collection.backToCollection')}</Link>
          </Button>
        </div>

        {/* Step Indicator */}
        <div className="mb-8">
          <div className="flex items-center justify-between">
            {visualSteps.map((stepInfo, visualIndex) => {
              const isActive = visualIndex === visualStepIndex;
              const isCompleted = visualIndex < visualStepIndex;

              return (
                <div
                  key={stepInfo.number}
                  className={`flex-1 ${visualIndex < visualSteps.length - 1 ? 'relative' : ''}`}
                >
                  <div className="flex flex-col items-center">
                    <div
                      className={`w-12 h-12 rounded-full flex items-center justify-center text-xl mb-2 transition-colors ${
                        isActive
                          ? 'bg-blue-600 text-white'
                          : isCompleted
                            ? 'bg-green-600 text-white'
                            : 'bg-slate-200 dark:bg-slate-700 text-slate-500 dark:text-slate-400'
                      }`}
                    >
                      {isCompleted ? '✓' : stepInfo.icon}
                    </div>
                    <span
                      className={`text-sm font-medium ${
                        isActive
                          ? 'text-blue-600 dark:text-blue-400'
                          : isCompleted
                            ? 'text-green-600 dark:text-green-400'
                            : 'text-slate-500 dark:text-slate-400'
                      }`}
                    >
                      {stepInfo.label}
                    </span>
                    <span
                      className="text-xs text-slate-500 dark:text-slate-400"
                      data-testid={WIZARD_TEST_IDS.stepDescription(stepInfo.number)}
                    >
                      {stepInfo.description}
                    </span>
                  </div>
                  {/* Connector line */}
                  {visualIndex < visualSteps.length - 1 && (
                    <div
                      className={`absolute top-6 left-1/2 w-full h-0.5 ${
                        isCompleted ? 'bg-green-600' : 'bg-slate-200 dark:bg-slate-700'
                      }`}
                      style={{ transform: 'translateX(50%)' }}
                    />
                  )}
                </div>
              );
            })}
          </div>
        </div>

        {/* Error Display */}
        {error && (
          <Card className="mb-6 p-4 bg-red-50 dark:bg-red-900/20 border-red-200 dark:border-red-800">
            <p className="text-red-800 dark:text-red-200">{error}</p>
          </Card>
        )}

        {/* Step Content with Animation */}
        <Card className="p-6 bg-white dark:bg-slate-800 shadow-lg overflow-hidden">
          <div
            ref={stepContentRef}
            tabIndex={-1}
            aria-live="polite"
            aria-atomic="true"
            className={`transition-all duration-300 ease-out ${getStepAnimationClass()}`}
          >
            {/* Screen reader announcement */}
            <span className="sr-only">
              {/* eslint-disable-next-line security/detect-object-injection */}
              Step {visualStepIndex + 1} of {visualSteps.length}: {visualSteps[visualStepIndex]?.description}
            </span>

            {step === 1 && <SearchSelectGame />}

            {step === 2 && shouldShowStep2 && <GameDetailsForm />}

            {step === 3 && <UploadPrivatePDF />}

            {step === 4 && <ReviewConfirm />}
          </div>
        </Card>

        {/* Summary Card */}
        {(reviewSummary.gameName !== 'Unknown Game' || reviewSummary.hasPdf) && (
          <Card className="mt-6 p-4 bg-slate-100 dark:bg-slate-700/50">
            <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
              Summary
            </h3>
            <div className="grid grid-cols-2 gap-4 text-sm">
              <div>
                <span className="text-slate-500">Game:</span>{' '}
                <span className="font-medium">{reviewSummary.gameName}</span>
                {reviewSummary.isCustom && (
                  <span className="ml-2 text-xs bg-blue-100 text-blue-700 px-2 py-0.5 rounded">
                    Custom
                  </span>
                )}
              </div>
              {reviewSummary.hasPdf && (
                <div>
                  <span className="text-slate-500">PDF:</span>{' '}
                  <span className="font-medium">{reviewSummary.pdfName}</span>
                </div>
              )}
              {reviewSummary.players && (
                <div>
                  <span className="text-slate-500">Players:</span>{' '}
                  <span className="font-medium">{reviewSummary.players}</span>
                </div>
              )}
              {reviewSummary.playTime && (
                <div>
                  <span className="text-slate-500">Play Time:</span>{' '}
                  <span className="font-medium">{reviewSummary.playTime}</span>
                </div>
              )}
            </div>
          </Card>
        )}
      </div>
    </div>
  );
}
