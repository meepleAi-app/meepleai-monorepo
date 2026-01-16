/**
 * QuickQuestionGenerator Component
 * Issue #2417 (Sub-Issue 2401.1): UI for AI-powered QuickQuestion generation
 *
 * Features:
 * - Prompt textarea input with validation (min/max length)
 * - Generate button with loading state
 * - AI progress indicator during generation
 * - Error handling UI with clear messages
 * - Accessible and responsive design
 *
 * @example
 * ```tsx
 * <QuickQuestionGenerator
 *   onGenerate={handleGenerate}
 *   onSuccess={handleSuccess}
 * />
 * ```
 */

import { useCallback, useState } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import { AlertCircle, Sparkles, XCircle } from 'lucide-react';
import { useForm } from 'react-hook-form';
import * as z from 'zod';

import { LoadingButton } from '@/components/loading/LoadingButton';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Label } from '@/components/ui/label';
import { Progress } from '@/components/ui/progress';
import { Textarea } from '@/components/ui/textarea';
import { cn } from '@/lib/utils';

// ==================== Types ====================

/**
 * Generated question result from AI
 */
export interface GeneratedQuestion {
  id: string;
  question: string;
  answer: string;
  category?: string;
  confidence: number;
}

/**
 * Generation state for tracking async operation
 */
export type GenerationState = 'idle' | 'generating' | 'success' | 'error';

/**
 * Form data for the prompt input
 */
export interface QuickQuestionFormData {
  prompt: string;
}

/**
 * Props for QuickQuestionGenerator component
 */
export interface QuickQuestionGeneratorProps {
  /**
   * Callback when generation is triggered
   * Should return the generated question or throw an error
   */
  onGenerate: (prompt: string) => Promise<GeneratedQuestion>;

  /**
   * Callback when generation succeeds
   */
  onSuccess?: (question: GeneratedQuestion) => void;

  /**
   * Callback when generation fails
   */
  onError?: (error: Error) => void;

  /**
   * Minimum prompt length
   * @default 10
   */
  minPromptLength?: number;

  /**
   * Maximum prompt length
   * @default 500
   */
  maxPromptLength?: number;

  /**
   * Initial prompt value
   */
  initialPrompt?: string;

  /**
   * Whether the component is disabled
   * @default false
   */
  disabled?: boolean;

  /**
   * Custom class name for the card container
   */
  className?: string;

  /**
   * Custom placeholder for textarea
   */
  placeholder?: string;
}

// ==================== Validation Schema ====================

const createPromptSchema = (minLength: number, maxLength: number) =>
  z.object({
    prompt: z
      .string()
      .min(minLength, `Prompt must be at least ${minLength} characters`)
      .max(maxLength, `Prompt must be at most ${maxLength} characters`)
      .refine(value => value.trim().length >= minLength, {
        message: `Prompt must have at least ${minLength} non-whitespace characters`,
      }),
  });

// ==================== Sub-Components ====================

interface ProgressIndicatorProps {
  progress: number;
  statusMessage: string;
}

function ProgressIndicator({ progress, statusMessage }: ProgressIndicatorProps) {
  return (
    <div
      className="space-y-2"
      role="status"
      aria-live="polite"
      aria-label={`Generation progress: ${progress}%`}
    >
      <div className="flex items-center justify-between text-sm">
        <span className="text-muted-foreground">{statusMessage}</span>
        <span className="font-medium">{progress}%</span>
      </div>
      <Progress value={progress} className="h-2" aria-valuemin={0} aria-valuemax={100} />
    </div>
  );
}

interface ErrorDisplayProps {
  error: string;
  onDismiss: () => void;
}

function ErrorDisplay({ error, onDismiss }: ErrorDisplayProps) {
  return (
    <Alert variant="destructive" className="relative">
      <AlertCircle className="h-4 w-4" />
      <AlertTitle>Generation Failed</AlertTitle>
      <AlertDescription>{error}</AlertDescription>
      <button
        type="button"
        onClick={onDismiss}
        className="absolute right-2 top-2 rounded-md p-1 hover:bg-destructive/10"
        aria-label="Dismiss error"
      >
        <XCircle className="h-4 w-4" />
      </button>
    </Alert>
  );
}

// ==================== Main Component ====================

export function QuickQuestionGenerator({
  onGenerate,
  onSuccess,
  onError,
  minPromptLength = 10,
  maxPromptLength = 500,
  initialPrompt = '',
  disabled = false,
  className,
  placeholder = 'Describe the type of question you want to generate. For example: "A strategy question about resource management in Catan"',
}: QuickQuestionGeneratorProps) {
  // ========== State ==========
  const [generationState, setGenerationState] = useState<GenerationState>('idle');
  const [progress, setProgress] = useState(0);
  const [statusMessage, setStatusMessage] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [generatedQuestion, setGeneratedQuestion] = useState<GeneratedQuestion | null>(null);

  // ========== Form Setup ==========
  const promptSchema = createPromptSchema(minPromptLength, maxPromptLength);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
    watch,
    reset,
  } = useForm<QuickQuestionFormData>({
    resolver: zodResolver(promptSchema),
    defaultValues: {
      prompt: initialPrompt,
    },
  });

  const promptValue = watch('prompt');
  const characterCount = promptValue?.length || 0;
  const isOverLimit = characterCount > maxPromptLength;
  const isUnderLimit = characterCount < minPromptLength && characterCount > 0;

  // ========== Handlers ==========
  const simulateProgress = useCallback(() => {
    const stages = [
      { progress: 20, message: 'Analyzing prompt...' },
      { progress: 40, message: 'Generating question context...' },
      { progress: 60, message: 'Formulating question...' },
      { progress: 80, message: 'Generating answer...' },
      { progress: 95, message: 'Finalizing...' },
    ];

    let currentStage = 0;
    const interval = setInterval(() => {
      if (currentStage < stages.length) {
        setProgress(stages[currentStage].progress);
        setStatusMessage(stages[currentStage].message);
        currentStage++;
      } else {
        clearInterval(interval);
      }
    }, 500);

    return () => clearInterval(interval);
  }, []);

  const handleGenerate = useCallback(
    async (data: QuickQuestionFormData) => {
      setGenerationState('generating');
      setError(null);
      setProgress(0);
      setStatusMessage('Starting generation...');
      setGeneratedQuestion(null);

      const stopProgress = simulateProgress();

      try {
        const result = await onGenerate(data.prompt.trim());
        setProgress(100);
        setStatusMessage('Complete!');
        setGeneratedQuestion(result);
        setGenerationState('success');
        onSuccess?.(result);
      } catch (err) {
        const errorMessage = err instanceof Error ? err.message : 'An unexpected error occurred';
        setError(errorMessage);
        setGenerationState('error');
        onError?.(err instanceof Error ? err : new Error(errorMessage));
      } finally {
        stopProgress();
      }
    },
    [onGenerate, onSuccess, onError, simulateProgress]
  );

  const handleDismissError = useCallback(() => {
    setError(null);
    setGenerationState('idle');
  }, []);

  const handleReset = useCallback(() => {
    reset();
    setGenerationState('idle');
    setProgress(0);
    setStatusMessage('');
    setError(null);
    setGeneratedQuestion(null);
  }, [reset]);

  // ========== Derived State ==========
  const isGenerating = generationState === 'generating' || isSubmitting;
  const isDisabled = disabled || isGenerating;
  const showProgress = generationState === 'generating';
  const showError = generationState === 'error' && error;
  const showSuccess = generationState === 'success' && generatedQuestion;

  // ========== Render ==========
  return (
    <Card className={cn('w-full max-w-2xl', className)}>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Sparkles className="h-5 w-5 text-primary" aria-hidden="true" />
          Quick Question Generator
        </CardTitle>
        <CardDescription>
          Describe what kind of question you want to generate for your game. The AI will create a
          question and answer based on your prompt.
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-4">
        <form onSubmit={handleSubmit(handleGenerate)} className="space-y-4">
          {/* Prompt Input */}
          <div className="space-y-2">
            <Label htmlFor="prompt">Your Prompt</Label>
            <Textarea
              id="prompt"
              placeholder={placeholder}
              disabled={isDisabled}
              aria-describedby="prompt-description prompt-error character-count"
              aria-invalid={!!errors.prompt}
              className={cn(
                'min-h-[120px] resize-y',
                errors.prompt && 'border-destructive focus-visible:ring-destructive'
              )}
              {...register('prompt')}
            />
            <p id="prompt-description" className="sr-only">
              Enter a description for the question you want to generate. Minimum {minPromptLength}{' '}
              characters, maximum {maxPromptLength} characters.
            </p>

            {/* Character Count & Validation */}
            <div className="flex items-center justify-between text-sm">
              <div>
                {errors.prompt && (
                  <p id="prompt-error" className="text-destructive" role="alert">
                    {errors.prompt.message}
                  </p>
                )}
              </div>
              <span
                id="character-count"
                className={cn(
                  'text-muted-foreground',
                  isOverLimit && 'text-destructive font-medium',
                  isUnderLimit && 'text-amber-600 dark:text-amber-400'
                )}
                aria-live="polite"
              >
                {characterCount}/{maxPromptLength}
              </span>
            </div>
          </div>

          {/* Progress Indicator */}
          {showProgress && <ProgressIndicator progress={progress} statusMessage={statusMessage} />}

          {/* Error Display */}
          {showError && <ErrorDisplay error={error} onDismiss={handleDismissError} />}

          {/* Success Display */}
          {showSuccess && (
            <Alert className="border-green-500/50 bg-green-50 dark:bg-green-950/20">
              <Sparkles className="h-4 w-4 text-green-600" />
              <AlertTitle className="text-green-700 dark:text-green-400">
                Question Generated!
              </AlertTitle>
              <AlertDescription className="space-y-2">
                <p className="font-medium text-green-800 dark:text-green-300">
                  {generatedQuestion.question}
                </p>
                <p className="text-sm text-green-700 dark:text-green-400">
                  Confidence: {Math.round(generatedQuestion.confidence * 100)}%
                </p>
              </AlertDescription>
            </Alert>
          )}

          {/* Actions */}
          <div className="flex items-center gap-3">
            <LoadingButton
              type="submit"
              isLoading={isGenerating}
              loadingText="Generating..."
              disabled={isDisabled || characterCount < minPromptLength || isOverLimit}
              className="flex-1"
            >
              <Sparkles className="mr-2 h-4 w-4" aria-hidden="true" />
              Generate Question
            </LoadingButton>

            {(showSuccess || showError) && (
              <LoadingButton
                type="button"
                variant="outline"
                onClick={handleReset}
                disabled={isGenerating}
              >
                Start Over
              </LoadingButton>
            )}
          </div>
        </form>
      </CardContent>
    </Card>
  );
}
