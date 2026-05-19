/**
 * GameNightCreateWizard — orchestrator skeleton for the SP7 wizard.
 * Issue #950 W3 Components (Commit 2). Spec §5 (C1) + §11 AC-O.1.
 *
 * This skeleton mounts the correct step component based on `state.step` and
 * renders a step indicator + nav buttons. The full page wiring (URL
 * `?step=` sync, autosave hook, mutation orchestration, retry policy) is
 * Commit 3 (Week 3 PR-2). This commit ships the visual scaffold so Week 4
 * E2E specs can target stable selectors.
 */

'use client';

import { useMemo, type Dispatch, type ReactElement } from 'react';

import type { UserSearchResult } from '@/lib/api/schemas/auth.schemas';
import type { RegularDto } from '@/lib/api/schemas/game-nights.schemas';
import type { WizardAction, WizardState, WizardStep } from '@/lib/game-nights/wizard-types';
import { isStepComplete } from '@/lib/game-nights/wizard-validators';

import {
  GameCandidatesPicker,
  type GameCandidatesPickerLabels,
  type GameCandidateOption,
} from './GameCandidatesPicker';
import {
  GameNightDateTimePicker,
  type GameNightDateTimePickerLabels,
} from './GameNightDateTimePicker';
import {
  GameNightLocationToggle,
  type GameNightLocationToggleLabels,
} from './GameNightLocationToggle';
import {
  PlayerInviteAutocomplete,
  type PlayerInviteAutocompleteLabels,
} from './PlayerInviteAutocomplete';
import { RSVPCardLivePreview, type RSVPCardLivePreviewLabels } from './RSVPCardLivePreview';

export interface GameNightCreateWizardLabels {
  readonly title: string;
  readonly subtitle: string;
  readonly nav: {
    readonly back: string;
    readonly next: string;
    readonly submit: string;
    readonly cancel: string;
  };
  readonly steps: {
    readonly step1: string;
    readonly step2: string;
    readonly step3: string;
    readonly step4: string;
    readonly progress: (current: number, total: number) => string;
  };
  readonly a11y: {
    readonly wizardLabel: string;
    readonly stepperLabel: string;
  };
  readonly step1: GameNightDateTimePickerLabels;
  readonly step2: GameNightLocationToggleLabels;
  readonly step3: PlayerInviteAutocompleteLabels;
  readonly step4: GameCandidatesPickerLabels;
  readonly preview: RSVPCardLivePreviewLabels;
}

export interface GameNightCreateWizardProps {
  readonly state: WizardState;
  readonly dispatch: Dispatch<WizardAction>;
  readonly title: string;
  readonly organizerName: string;
  readonly labels: GameNightCreateWizardLabels;

  // Step 3 data (orchestrator passes resolved react-query results)
  readonly playerSearchQuery: string;
  readonly onPlayerSearchQueryChange: (q: string) => void;
  readonly playerSearchResults: readonly UserSearchResult[];
  readonly isSearchingPlayers?: boolean;
  readonly regulars: readonly RegularDto[];

  // Step 4 data
  readonly libraryGames: readonly GameCandidateOption[];

  // Submit flow
  readonly onSubmit: () => void;
  readonly isSubmitting?: boolean;

  // Step 1 conflict surface
  readonly isCheckingConflict?: boolean;
}

const TOTAL_STEPS = 4 as const;

export function GameNightCreateWizard({
  state,
  dispatch,
  title,
  organizerName,
  labels,
  playerSearchQuery,
  onPlayerSearchQueryChange,
  playerSearchResults,
  isSearchingPlayers = false,
  regulars,
  libraryGames,
  onSubmit,
  isSubmitting = false,
  isCheckingConflict = false,
}: GameNightCreateWizardProps): ReactElement {
  const stepLabels = useMemo(
    () => [labels.steps.step1, labels.steps.step2, labels.steps.step3, labels.steps.step4],
    [labels.steps]
  );

  const canAdvance = isStepComplete(state, state.step);
  const isFinalStep = state.step === TOTAL_STEPS;

  const goPrev = (): void => {
    if (state.step > 1) {
      dispatch({ type: 'goToStep', step: (state.step - 1) as WizardStep });
    }
  };

  const goNext = (): void => {
    if (isFinalStep) {
      onSubmit();
      return;
    }
    dispatch({ type: 'goToStep', step: (state.step + 1) as WizardStep });
  };

  return (
    <div
      data-slot="game-night-create-wizard"
      aria-label={labels.a11y.wizardLabel}
      className="grid grid-cols-1 gap-6 lg:grid-cols-[2fr_1fr]"
    >
      <div className="flex flex-col gap-6">
        <header className="flex flex-col gap-1">
          <h1 className="text-xl font-semibold text-foreground">{labels.title}</h1>
          <p className="text-sm text-muted-foreground">{labels.subtitle}</p>
        </header>

        <nav
          aria-label={labels.a11y.stepperLabel}
          className="flex items-center gap-2"
          data-slot="game-night-create-stepper"
        >
          {stepLabels.map((label, idx) => {
            const step = (idx + 1) as WizardStep;
            const isCurrent = step === state.step;
            const isComplete = step < state.step;
            return (
              <button
                key={label}
                type="button"
                onClick={() => dispatch({ type: 'goToStep', step })}
                aria-current={isCurrent ? 'step' : undefined}
                data-slot={`game-night-create-stepper-step${step}`}
                className={
                  isCurrent
                    ? 'rounded-full border border-primary bg-primary/10 px-3 py-1 text-xs font-medium text-foreground'
                    : isComplete
                      ? 'rounded-full border border-primary px-3 py-1 text-xs text-primary'
                      : 'rounded-full border border-border px-3 py-1 text-xs text-muted-foreground'
                }
              >
                {step}. {label}
              </button>
            );
          })}
        </nav>

        <p className="text-xs text-muted-foreground">
          {labels.steps.progress(state.step, TOTAL_STEPS)}
        </p>

        <div data-slot={`game-night-create-step-${state.step}-container`}>
          {state.step === 1 && (
            <GameNightDateTimePicker
              iso={state.date.iso}
              onSetDate={iso => dispatch({ type: 'setDate', iso })}
              conflictResult={state.date.conflictResult}
              isCheckingConflict={isCheckingConflict}
              onContinueAnyway={() =>
                dispatch({
                  type: 'recordConflict',
                  // Mark as checked but blank so the warning surface
                  // dismisses; the date itself stays put.
                  checkedAt: new Date().toISOString(),
                  result: { hasConflict: false, conflicts: [] },
                })
              }
              labels={labels.step1}
            />
          )}

          {state.step === 2 && (
            <GameNightLocationToggle
              kind={state.location.kind}
              details={state.location.details}
              onSetLocation={(kind, details) => dispatch({ type: 'setLocation', kind, details })}
              labels={labels.step2}
            />
          )}

          {state.step === 3 && (
            <PlayerInviteAutocomplete
              invitees={state.invitees}
              searchResults={playerSearchResults}
              regulars={regulars}
              query={playerSearchQuery}
              onQueryChange={onPlayerSearchQueryChange}
              onAddInvitee={invitee => dispatch({ type: 'addInvitee', invitee })}
              onRemoveInvitee={key => dispatch({ type: 'removeInvitee', key })}
              isSearching={isSearchingPlayers}
              labels={labels.step3}
            />
          )}

          {state.step === 4 && (
            <GameCandidatesPicker
              games={libraryGames}
              selected={state.games.selected}
              decideAtGroup={state.games.decideAtGroup}
              onToggleGame={gameId => dispatch({ type: 'toggleGame', gameId })}
              onToggleDecideAtGroup={() => dispatch({ type: 'toggleDecideAtGroup' })}
              labels={labels.step4}
            />
          )}
        </div>

        <footer
          className="flex items-center justify-between border-t border-border pt-4"
          data-slot="game-night-create-nav"
        >
          <button
            type="button"
            onClick={goPrev}
            disabled={state.step === 1}
            className="rounded-md border border-border bg-card px-3 py-2 text-sm text-muted-foreground hover:text-foreground disabled:opacity-50"
            data-slot="game-night-create-nav-back"
          >
            {labels.nav.back}
          </button>
          <button
            type="button"
            onClick={goNext}
            disabled={!canAdvance || isSubmitting}
            className="rounded-md bg-primary px-4 py-2 text-sm font-medium text-primary-foreground disabled:opacity-50"
            data-slot="game-night-create-nav-next"
          >
            {isFinalStep ? labels.nav.submit : labels.nav.next}
          </button>
        </footer>
      </div>

      <RSVPCardLivePreview
        state={state}
        title={title}
        organizerName={organizerName}
        games={libraryGames}
        labels={labels.preview}
      />
    </div>
  );
}
