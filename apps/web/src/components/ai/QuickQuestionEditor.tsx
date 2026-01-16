/**
 * QuickQuestionEditor Component
 * Issue #2419 (Sub-Issue 2401.3): Display and edit AI-generated QuickQuestion
 *
 * Features:
 * - AI preview display showing generated question
 * - Inline editing with debounced auto-save
 * - Regenerate button with confirmation when modified
 * - Save/discard actions with dirty state tracking
 * - Unsaved changes warning
 * - Accessible and responsive design
 *
 * @example
 * ```tsx
 * <QuickQuestionEditor
 *   question={generatedQuestion}
 *   onSave={handleSave}
 *   onRegenerate={handleRegenerate}
 * />
 * ```
 */

import { useCallback, useEffect, useMemo, useRef, useState } from 'react';

import { Check, Edit2, RefreshCw, RotateCcw, Save, X } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/ui/confirm-dialog';
import { Label } from '@/components/ui/label';
import { Textarea } from '@/components/ui/textarea';
import { Tooltip, TooltipContent, TooltipTrigger } from '@/components/ui/tooltip';
import { cn } from '@/lib/utils';

import type { GeneratedQuestion } from './QuickQuestionGenerator';

// ==================== Types ====================

/**
 * Save status for tracking auto-save operations
 */
export type SaveStatus = 'idle' | 'saving' | 'saved' | 'error';

/**
 * Props for QuickQuestionEditor component
 */
export interface QuickQuestionEditorProps {
  /**
   * The generated question to display/edit
   */
  question: GeneratedQuestion;

  /**
   * Callback when changes are saved
   * Can return a promise for async save operations
   */
  onSave: (updated: GeneratedQuestion) => void | Promise<void>;

  /**
   * Callback when changes are discarded
   */
  onDiscard?: () => void;

  /**
   * Callback when regenerate is requested
   * Receives the current (possibly modified) question
   */
  onRegenerate?: () => void;

  /**
   * Delay in ms before auto-save triggers
   * @default 1500
   */
  autoSaveDelay?: number;

  /**
   * Whether to enable auto-save feature
   * @default true
   */
  enableAutoSave?: boolean;

  /**
   * Whether the component is disabled
   * @default false
   */
  disabled?: boolean;

  /**
   * Whether the component is in read-only mode (preview only)
   * @default false
   */
  readOnly?: boolean;

  /**
   * Custom class name for the card container
   */
  className?: string;
}

// ==================== Helper Functions ====================

/**
 * Check if two GeneratedQuestion objects are equal
 */
function questionsEqual(a: GeneratedQuestion, b: GeneratedQuestion): boolean {
  return a.question === b.question && a.answer === b.answer;
}

/**
 * Format confidence score as percentage
 */
function formatConfidence(confidence: number): string {
  return `${Math.round(confidence * 100)}%`;
}

// ==================== Sub-Components ====================

interface PreviewDisplayProps {
  question: GeneratedQuestion;
  onEditClick: () => void;
  disabled: boolean;
  readOnly: boolean;
}

function PreviewDisplay({ question, onEditClick, disabled, readOnly }: PreviewDisplayProps) {
  return (
    <div className="space-y-4">
      {/* Question Preview */}
      <div className="space-y-2">
        <div className="flex items-center justify-between">
          <Label className="text-sm font-medium text-muted-foreground">Question</Label>
          {question.category && (
            <Badge variant="secondary" className="text-xs">
              {question.category}
            </Badge>
          )}
        </div>
        <p className="text-base font-medium leading-relaxed">{question.question}</p>
      </div>

      {/* Answer Preview */}
      <div className="space-y-2">
        <Label className="text-sm font-medium text-muted-foreground">Answer</Label>
        <p className="text-sm text-muted-foreground leading-relaxed">{question.answer}</p>
      </div>

      {/* Metadata & Edit Button */}
      <div className="flex items-center justify-between pt-2 border-t">
        <div className="flex items-center gap-2">
          <Badge
            variant="outline"
            className={cn(
              'text-xs',
              question.confidence >= 0.8
                ? 'border-green-500 text-green-700 dark:text-green-400'
                : question.confidence >= 0.6
                  ? 'border-amber-500 text-amber-700 dark:text-amber-400'
                  : 'border-red-500 text-red-700 dark:text-red-400'
            )}
          >
            Confidence: {formatConfidence(question.confidence)}
          </Badge>
        </div>

        {!readOnly && (
          <Tooltip>
            <TooltipTrigger asChild>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={onEditClick}
                disabled={disabled}
                aria-label="Edit question"
              >
                <Edit2 className="h-4 w-4 mr-2" aria-hidden="true" />
                Edit
              </Button>
            </TooltipTrigger>
            <TooltipContent>Click to edit question and answer</TooltipContent>
          </Tooltip>
        )}
      </div>
    </div>
  );
}

interface EditFormProps {
  localQuestion: GeneratedQuestion;
  onQuestionChange: (value: string) => void;
  onAnswerChange: (value: string) => void;
  disabled: boolean;
}

function EditForm({ localQuestion, onQuestionChange, onAnswerChange, disabled }: EditFormProps) {
  return (
    <div className="space-y-4">
      {/* Question Input */}
      <div className="space-y-2">
        <Label htmlFor="edit-question" className="text-sm font-medium">
          Question
        </Label>
        <Textarea
          id="edit-question"
          value={localQuestion.question}
          onChange={e => onQuestionChange(e.target.value)}
          disabled={disabled}
          className="min-h-[80px] resize-y"
          placeholder="Enter the question..."
          aria-describedby="edit-question-description"
        />
        <p id="edit-question-description" className="sr-only">
          Edit the generated question text
        </p>
      </div>

      {/* Answer Input */}
      <div className="space-y-2">
        <Label htmlFor="edit-answer" className="text-sm font-medium">
          Answer
        </Label>
        <Textarea
          id="edit-answer"
          value={localQuestion.answer}
          onChange={e => onAnswerChange(e.target.value)}
          disabled={disabled}
          className="min-h-[100px] resize-y"
          placeholder="Enter the answer..."
          aria-describedby="edit-answer-description"
        />
        <p id="edit-answer-description" className="sr-only">
          Edit the generated answer text
        </p>
      </div>

      {/* Metadata (read-only) */}
      <div className="flex items-center gap-2 pt-2 border-t">
        {localQuestion.category && (
          <Badge variant="secondary" className="text-xs">
            {localQuestion.category}
          </Badge>
        )}
        <Badge
          variant="outline"
          className={cn(
            'text-xs',
            localQuestion.confidence >= 0.8
              ? 'border-green-500 text-green-700 dark:text-green-400'
              : localQuestion.confidence >= 0.6
                ? 'border-amber-500 text-amber-700 dark:text-amber-400'
                : 'border-red-500 text-red-700 dark:text-red-400'
          )}
        >
          Confidence: {formatConfidence(localQuestion.confidence)}
        </Badge>
      </div>
    </div>
  );
}

interface ActionButtonsProps {
  isEditing: boolean;
  isDirty: boolean;
  isSaving: boolean;
  saveStatus: SaveStatus;
  disabled: boolean;
  readOnly: boolean;
  hasRegenerate: boolean;
  onSave: () => void;
  onDiscard: () => void;
  onCancelEdit: () => void;
  onRegenerate: () => void;
}

function ActionButtons({
  isEditing,
  isDirty,
  isSaving,
  saveStatus,
  disabled,
  readOnly,
  hasRegenerate,
  onSave,
  onDiscard,
  onCancelEdit,
  onRegenerate,
}: ActionButtonsProps) {
  if (readOnly) return null;

  return (
    <div className="flex items-center justify-between gap-2 pt-4 border-t">
      <div className="flex items-center gap-2">
        {/* Save Status Indicator */}
        {saveStatus === 'saving' && (
          <span className="text-xs text-muted-foreground animate-pulse" aria-live="polite">
            Saving...
          </span>
        )}
        {saveStatus === 'saved' && (
          <span
            className="text-xs text-green-600 dark:text-green-400 flex items-center gap-1"
            aria-live="polite"
          >
            <Check className="h-3 w-3" aria-hidden="true" />
            Saved
          </span>
        )}
        {saveStatus === 'error' && (
          <span className="text-xs text-destructive" aria-live="polite">
            Save failed
          </span>
        )}

        {/* Dirty Indicator */}
        {isDirty && saveStatus === 'idle' && (
          <Badge variant="outline" className="text-xs border-amber-500 text-amber-600">
            Unsaved changes
          </Badge>
        )}
      </div>

      <div className="flex items-center gap-2">
        {isEditing ? (
          <>
            {/* Cancel Edit */}
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  onClick={onCancelEdit}
                  disabled={disabled || isSaving}
                  aria-label="Cancel editing"
                >
                  <X className="h-4 w-4" aria-hidden="true" />
                </Button>
              </TooltipTrigger>
              <TooltipContent>Cancel editing</TooltipContent>
            </Tooltip>

            {/* Discard Changes */}
            {isDirty && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={onDiscard}
                    disabled={disabled || isSaving}
                    aria-label="Discard changes"
                  >
                    <RotateCcw className="h-4 w-4 mr-2" aria-hidden="true" />
                    Discard
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Discard all changes</TooltipContent>
              </Tooltip>
            )}

            {/* Save Changes */}
            <Tooltip>
              <TooltipTrigger asChild>
                <Button
                  type="button"
                  variant="default"
                  size="sm"
                  onClick={onSave}
                  disabled={disabled || isSaving || !isDirty}
                  aria-label="Save changes"
                  aria-busy={isSaving}
                >
                  <Save className="h-4 w-4 mr-2" aria-hidden="true" />
                  {isSaving ? 'Saving...' : 'Save'}
                </Button>
              </TooltipTrigger>
              <TooltipContent>Save changes</TooltipContent>
            </Tooltip>
          </>
        ) : (
          <>
            {/* Regenerate Button */}
            {hasRegenerate && (
              <Tooltip>
                <TooltipTrigger asChild>
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={onRegenerate}
                    disabled={disabled}
                    aria-label="Regenerate question"
                  >
                    <RefreshCw className="h-4 w-4 mr-2" aria-hidden="true" />
                    Regenerate
                  </Button>
                </TooltipTrigger>
                <TooltipContent>Generate a new question</TooltipContent>
              </Tooltip>
            )}
          </>
        )}
      </div>
    </div>
  );
}

// ==================== Main Component ====================

export function QuickQuestionEditor({
  question,
  onSave,
  onDiscard,
  onRegenerate,
  autoSaveDelay = 1500,
  enableAutoSave = true,
  disabled = false,
  readOnly = false,
  className,
}: QuickQuestionEditorProps) {
  // ========== State ==========
  const [isEditing, setIsEditing] = useState(false);
  const [localQuestion, setLocalQuestion] = useState<GeneratedQuestion>(question);
  const [saveStatus, setSaveStatus] = useState<SaveStatus>('idle');
  const [showRegenerateConfirm, setShowRegenerateConfirm] = useState(false);

  // ========== Refs ==========
  const autoSaveTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);
  const isMountedRef = useRef(true);
  const savingInProgressRef = useRef(false); // Prevents race conditions in concurrent saves
  const latestSaveRef = useRef<(() => Promise<void>) | undefined>(undefined); // Stores latest save function for auto-save

  // ========== Derived State ==========
  const isDirty = useMemo(() => !questionsEqual(localQuestion, question), [localQuestion, question]);
  const isSaving = saveStatus === 'saving';

  // ========== Effects ==========

  // Sync local state when question prop changes
  useEffect(() => {
    setLocalQuestion(question);
  }, [question]);

  // Cleanup on unmount
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
      if (autoSaveTimerRef.current) {
        clearTimeout(autoSaveTimerRef.current);
      }
    };
  }, []);

  // Auto-save effect - triggers debounced save when localQuestion changes
  useEffect(() => {
    // Skip auto-save if conditions aren't met
    if (!enableAutoSave || !isDirty || !isEditing || savingInProgressRef.current || readOnly) {
      return;
    }

    // Clear existing timer
    if (autoSaveTimerRef.current) {
      clearTimeout(autoSaveTimerRef.current);
    }

    // Set new auto-save timer - uses ref to always call latest save function
    autoSaveTimerRef.current = setTimeout(() => {
      if (isMountedRef.current && !savingInProgressRef.current && latestSaveRef.current) {
        latestSaveRef.current();
      }
    }, autoSaveDelay);

    return () => {
      if (autoSaveTimerRef.current) {
        clearTimeout(autoSaveTimerRef.current);
      }
    };
  }, [localQuestion, enableAutoSave, isEditing, autoSaveDelay, readOnly, isDirty]);

  // Reset save status after showing "Saved"
  useEffect(() => {
    if (saveStatus === 'saved') {
      const timer = setTimeout(() => {
        if (isMountedRef.current) {
          setSaveStatus('idle');
        }
      }, 2000);
      return () => clearTimeout(timer);
    }
  }, [saveStatus]);

  // ========== Handlers ==========

  const handleSave = useCallback(async () => {
    // Race condition guard using ref (more reliable than state check)
    if (savingInProgressRef.current || !isDirty) return;

    // Validation: Prevent saving empty question or answer
    if (!localQuestion.question.trim() || !localQuestion.answer.trim()) {
      setSaveStatus('error');
      return;
    }

    savingInProgressRef.current = true;
    setSaveStatus('saving');

    try {
      await onSave(localQuestion);
      if (isMountedRef.current) {
        setSaveStatus('saved');
      }
    } catch {
      if (isMountedRef.current) {
        setSaveStatus('error');
      }
    } finally {
      savingInProgressRef.current = false;
    }
  }, [isDirty, onSave, localQuestion]);

  // Keep ref updated with latest save function for auto-save effect
  latestSaveRef.current = handleSave;

  const handleDiscard = useCallback(() => {
    setLocalQuestion(question);
    setSaveStatus('idle');
    onDiscard?.();
  }, [question, onDiscard]);

  const handleEditClick = useCallback(() => {
    setIsEditing(true);
  }, []);

  const handleCancelEdit = useCallback(() => {
    if (isDirty) {
      // Reset to original if there are unsaved changes
      setLocalQuestion(question);
    }
    setIsEditing(false);
    setSaveStatus('idle');
  }, [isDirty, question]);

  const handleQuestionChange = useCallback((value: string) => {
    setLocalQuestion(prev => ({ ...prev, question: value }));
    setSaveStatus('idle');
  }, []);

  const handleAnswerChange = useCallback((value: string) => {
    setLocalQuestion(prev => ({ ...prev, answer: value }));
    setSaveStatus('idle');
  }, []);

  const handleRegenerateClick = useCallback(() => {
    if (isDirty) {
      setShowRegenerateConfirm(true);
    } else {
      onRegenerate?.();
    }
  }, [isDirty, onRegenerate]);

  const handleRegenerateConfirm = useCallback(() => {
    setShowRegenerateConfirm(false);
    setIsEditing(false);
    setSaveStatus('idle');
    onRegenerate?.();
  }, [onRegenerate]);

  // ========== Render ==========
  return (
    <>
      <Card className={cn('w-full max-w-2xl', className)}>
        <CardHeader className="pb-3">
          <CardTitle className="flex items-center justify-between text-lg">
            <span className="flex items-center gap-2">
              {isEditing ? 'Edit Question' : 'Generated Question'}
            </span>
            {isDirty && !readOnly && (
              <span className="text-xs font-normal text-amber-600 dark:text-amber-400">
                * Modified
              </span>
            )}
          </CardTitle>
        </CardHeader>

        <CardContent className="space-y-4">
          {isEditing ? (
            <EditForm
              localQuestion={localQuestion}
              onQuestionChange={handleQuestionChange}
              onAnswerChange={handleAnswerChange}
              disabled={disabled || isSaving}
            />
          ) : (
            <PreviewDisplay
              question={localQuestion}
              onEditClick={handleEditClick}
              disabled={disabled}
              readOnly={readOnly}
            />
          )}

          <ActionButtons
            isEditing={isEditing}
            isDirty={isDirty}
            isSaving={isSaving}
            saveStatus={saveStatus}
            disabled={disabled}
            readOnly={readOnly}
            hasRegenerate={!!onRegenerate}
            onSave={handleSave}
            onDiscard={handleDiscard}
            onCancelEdit={handleCancelEdit}
            onRegenerate={handleRegenerateClick}
          />
        </CardContent>
      </Card>

      {/* Regenerate Confirmation Dialog */}
      <ConfirmDialog
        open={showRegenerateConfirm}
        onOpenChange={setShowRegenerateConfirm}
        title="Regenerate Question?"
        message="You have unsaved changes. Regenerating will discard your modifications. Are you sure you want to continue?"
        variant="destructive"
        confirmText="Regenerate"
        cancelText="Keep Editing"
        onConfirm={handleRegenerateConfirm}
      />
    </>
  );
}
