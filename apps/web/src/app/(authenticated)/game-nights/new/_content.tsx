/**
 * NewGameNightContent — client-side orchestrator for `/game-nights/new`.
 *
 * Issue #950 W3 Commit 3. Spec §11 AC-O.1 through AC-O.5:
 *   - O.1: GameNightCreateWizard composed with all 6 v2 components
 *   - O.2: page.tsx swaps the legacy form for this wizard
 *   - O.3: URL `?step=N` sync (URL → reducer on mount; reducer → URL on goToStep)
 *   - O.4: Autosave hook + restore + clear-on-success
 *   - O.5: useCreateGameNight mutation with retry [1s, 2s, 4s]
 */

'use client';

import { useEffect, useMemo, useReducer, useRef, useState, type ReactElement } from 'react';

import { useRouter, useSearchParams } from 'next/navigation';

import {
  GameNightCreateWizard,
  type GameNightCreateWizardLabels,
  type GameCandidateOption,
} from '@/components/features/game-night-create';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useCreateGameNight } from '@/hooks/queries/useGameNights';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useToast } from '@/hooks/useToast';
import { useTranslation } from '@/hooks/useTranslation';
import { useGameNightConflictCheck } from '@/lib/game-nights/hooks/useGameNightConflictCheck';
import { useGameNightDraftPersist } from '@/lib/game-nights/hooks/useGameNightDraftPersist';
import { usePlayerSearch } from '@/lib/game-nights/hooks/usePlayerSearch';
import { useRegularsForUser } from '@/lib/game-nights/hooks/useRegularsForUser';
import { initialWizardState, wizardReducer } from '@/lib/game-nights/wizard-reducer';
import type { WizardStep } from '@/lib/game-nights/wizard-types';
import { buildSubmitPayload } from '@/lib/game-nights/wizard-validators';

const TOTAL_STEPS = 4 as const;
const SUBMIT_RETRY_DELAYS_MS: readonly number[] = [1000, 2000, 4000];

function parseStep(value: string | null): WizardStep {
  const n = value ? Number.parseInt(value, 10) : NaN;
  return n === 1 || n === 2 || n === 3 || n === 4 ? n : 1;
}

/**
 * Build the deeply-nested labels object the wizard expects from the flat
 * i18n catalog. Pure derivation; useMemo'd to avoid re-render churn.
 */
function buildWizardLabels(
  t: (key: string, values?: Record<string, unknown>) => string
): GameNightCreateWizardLabels {
  return {
    title: t('gameNightCreate.title'),
    subtitle: t('gameNightCreate.subtitle'),
    nav: {
      back: t('gameNightCreate.nav.back'),
      next: t('gameNightCreate.nav.next'),
      submit: t('gameNightCreate.nav.submit'),
      cancel: t('gameNightCreate.nav.cancel'),
    },
    steps: {
      step1: t('gameNightCreate.steps.step1'),
      step2: t('gameNightCreate.steps.step2'),
      step3: t('gameNightCreate.steps.step3'),
      step4: t('gameNightCreate.steps.step4'),
      progress: (current, total) => t('gameNightCreate.steps.progress', { current, total }),
    },
    a11y: {
      wizardLabel: t('gameNightCreate.a11y.wizardLabel'),
      stepperLabel: t('gameNightCreate.a11y.stepperLabel'),
    },
    step1: {
      label: t('gameNightCreate.step1.label'),
      helper: t('gameNightCreate.step1.helper'),
      conflictWarningTitle: t('gameNightCreate.step1.conflictWarningTitle'),
      conflictWarningBody: count => t('gameNightCreate.step1.conflictWarningBody', { count }),
      conflictRoleOrganizer: t('gameNightCreate.step1.conflictRoleOrganizer'),
      conflictRoleInvitee: t('gameNightCreate.step1.conflictRoleInvitee'),
      continueAnyway: t('gameNightCreate.step1.continueAnyway'),
      // PR #1302 review fix: was reusing `continueAnyway` due to a
      // copy-paste — now wired to the dedicated `checking` key.
      checking: t('gameNightCreate.step1.checking'),
    },
    step2: {
      label: t('gameNightCreate.step2.label'),
      kindHome: t('gameNightCreate.step2.kindHome'),
      kindFriend: t('gameNightCreate.step2.kindFriend'),
      kindOnline: t('gameNightCreate.step2.kindOnline'),
      kindTbd: t('gameNightCreate.step2.kindTbd'),
      detailsLabel: t('gameNightCreate.step2.detailsLabel'),
      detailsPlaceholder: t('gameNightCreate.step2.detailsPlaceholder'),
      detailsHelper: t('gameNightCreate.step2.detailsHelper'),
    },
    step3: {
      label: t('gameNightCreate.step3.label'),
      searchPlaceholder: t('gameNightCreate.step3.searchPlaceholder'),
      searchAriaLabel: t('gameNightCreate.step3.searchAriaLabel'),
      regularsTitle: t('gameNightCreate.step3.regularsTitle'),
      regularsHelper: t('gameNightCreate.step3.regularsHelper'),
      regularsEmpty: t('gameNightCreate.step3.regularsEmpty'),
      addRegular: t('gameNightCreate.step3.addRegular'),
      remove: t('gameNightCreate.step3.remove'),
      addEmail: t('gameNightCreate.step3.addEmail'),
      emailInvalid: t('gameNightCreate.step3.emailInvalid'),
      emailDuplicate: t('gameNightCreate.step3.emailDuplicate'),
      inviteeCount: count => t('gameNightCreate.step3.inviteeCount', { count }),
      limitWarning: max => t('gameNightCreate.step3.limitWarning', { max }),
      searching: t('gameNightCreate.step3.searching'),
    },
    step4: {
      label: t('gameNightCreate.step4.label'),
      decideAtGroupLabel: t('gameNightCreate.step4.decideAtGroupLabel'),
      decideAtGroupHelper: t('gameNightCreate.step4.decideAtGroupHelper'),
      selectedCount: count => t('gameNightCreate.step4.selectedCount', { count }),
      libraryHeader: t('gameNightCreate.step4.libraryHeader'),
      libraryEmpty: t('gameNightCreate.step4.libraryEmpty'),
      selectGame: name => t('gameNightCreate.step4.selectGame', { name }),
      deselectGame: name => t('gameNightCreate.step4.deselectGame', { name }),
    },
    preview: {
      title: t('gameNightCreate.preview.title'),
      subtitle: t('gameNightCreate.preview.subtitle'),
      rsvpAccept: t('gameNightCreate.preview.rsvpAccept'),
      rsvpDecline: t('gameNightCreate.preview.rsvpDecline'),
      noDate: t('gameNightCreate.preview.noDate'),
      noLocation: t('gameNightCreate.preview.noLocation'),
      gamesTbd: t('gameNightCreate.preview.gamesTbd'),
      gamesNone: t('gameNightCreate.preview.gamesNone'),
      sectionWhen: t('gameNightCreate.preview.sectionWhen'),
      sectionWhere: t('gameNightCreate.preview.sectionWhere'),
      sectionWhat: t('gameNightCreate.preview.sectionWhat'),
      sectionWho: t('gameNightCreate.preview.sectionWho'),
      kindHome: t('gameNightCreate.preview.kindHome'),
      kindFriend: t('gameNightCreate.preview.kindFriend'),
      kindOnline: t('gameNightCreate.preview.kindOnline'),
    },
  };
}

export function NewGameNightContent(): ReactElement {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { t } = useTranslation();
  const { toast } = useToast();

  const currentUserQuery = useCurrentUser();
  const userId = currentUserQuery.data?.id ?? null;
  const organizerName = currentUserQuery.data?.displayName ?? currentUserQuery.data?.email ?? '';

  // URL → initial step. The wizard reducer source-of-truths the rest;
  // we only sync `?step=` (data is kept in reducer, not URL — spec §8).
  const initialStep = parseStep(searchParams.get('step'));
  const [state, dispatch] = useReducer(wizardReducer, {
    ...initialWizardState,
    step: initialStep,
  });

  const [title, setTitle] = useState('');
  const [playerSearchQuery, setPlayerSearchQuery] = useState('');

  // ─── Autosave: load draft on mount, persist on changes, clear on success
  const draftPersist = useGameNightDraftPersist({ userId, state });
  const draftRestoredRef = useRef(false);
  useEffect(() => {
    if (draftRestoredRef.current) return;
    if (draftPersist.initialDraft != null) {
      dispatch({ type: 'restoreFromDraft', draft: draftPersist.initialDraft });
    }
    draftRestoredRef.current = true;
  }, [draftPersist.initialDraft]);

  // ─── URL `?step=` sync (reducer → URL)
  useEffect(() => {
    const current = searchParams.get('step');
    const next = String(state.step);
    if (current === next) return;
    const params = new URLSearchParams(searchParams.toString());
    params.set('step', next);
    router.replace(`?${params.toString()}`, { scroll: false });
  }, [state.step, router, searchParams]);

  // ─── Step 3 hooks
  const playerSearch = usePlayerSearch({ query: playerSearchQuery });
  const regularsQuery = useRegularsForUser({ enabled: state.step === 3 });

  // ─── Step 1 conflict check effect — dispatch when result lands
  const conflictCheck = useGameNightConflictCheck({
    at: state.date.iso,
    enabled: state.date.iso != null,
  });
  const lastDispatchedIsoRef = useRef<string | null>(null);
  useEffect(() => {
    if (!conflictCheck.data) return;
    if (state.date.iso == null) return;
    if (lastDispatchedIsoRef.current === state.date.iso) return;
    dispatch({
      type: 'recordConflict',
      result: conflictCheck.data,
      checkedAt: new Date().toISOString(),
    });
    lastDispatchedIsoRef.current = state.date.iso;
  }, [conflictCheck.data, state.date.iso]);

  // ─── Step 4 library
  const libraryQuery = useLibrary({ pageSize: 50 }, state.step === 4);
  const libraryGames = useMemo<readonly GameCandidateOption[]>(() => {
    const items = libraryQuery.data?.items ?? [];
    return items.map(entry => ({
      id: entry.gameId,
      title: entry.gameTitle,
      imageUrl: entry.gameImageUrl,
      minPlayers: entry.minPlayers ?? null,
      maxPlayers: entry.maxPlayers ?? null,
      playingTimeMinutes: entry.playingTimeMinutes ?? null,
    }));
  }, [libraryQuery.data]);

  // ─── Submit with retry [1s, 2s, 4s]
  const createMutation = useCreateGameNight();
  const [isSubmittingWithRetry, setIsSubmittingWithRetry] = useState(false);

  // PR #1302 review fix: unmount guard for the multi-second retry loop.
  // Without this, a user navigating away mid-retry would still trigger
  // setIsSubmittingWithRetry(false) on the unmounted tree (React warning)
  // and the late `router.push` would fire on a route the user already left.
  const isMountedRef = useRef(true);
  useEffect(() => {
    isMountedRef.current = true;
    return () => {
      isMountedRef.current = false;
    };
  }, []);

  const handleSubmit = async (): Promise<void> => {
    if (isSubmittingWithRetry) return;
    const payloadResult = buildSubmitPayload(state, { title });
    if (!payloadResult.ok) {
      toast({
        title: t('gameNightCreate.submit.errorTitle'),
        description: t('gameNightCreate.submit.errorBody'),
        variant: 'destructive',
      });
      return;
    }

    // The buildSubmitPayload contract returns `null` for optional fields the
    // user didn't fill; the legacy `CreateGameNightInput` shape on the
    // mutation hook uses `undefined` for the same gate. Strip nulls so the
    // type check passes without widening the shared schema (a wider change
    // would touch unrelated callers).
    const p = payloadResult.payload;
    const mutationInput = {
      title: p.title,
      scheduledAt: p.scheduledAt,
      ...(p.description != null ? { description: p.description } : {}),
      ...(p.location != null ? { location: p.location } : {}),
      ...(p.maxPlayers != null ? { maxPlayers: p.maxPlayers } : {}),
      ...(p.gameIds != null ? { gameIds: p.gameIds } : {}),
      ...(p.invitedUserIds != null ? { invitedUserIds: p.invitedUserIds } : {}),
      ...(p.invitedEmails != null ? { invitedEmails: p.invitedEmails } : {}),
    };

    setIsSubmittingWithRetry(true);
    const delays = [0, ...SUBMIT_RETRY_DELAYS_MS]; // first try has no pre-wait
    let id: string | null = null;
    let lastError: unknown = null;

    for (let attempt = 0; attempt < delays.length; attempt++) {
      if (!isMountedRef.current) return;
      if (delays[attempt] > 0) {
        await new Promise<void>(r => setTimeout(r, delays[attempt]));
      }
      if (!isMountedRef.current) return;
      try {
        id = await createMutation.mutateAsync(mutationInput);
        break;
      } catch (err) {
        lastError = err;
      }
    }

    if (!isMountedRef.current) return;
    setIsSubmittingWithRetry(false);

    if (id) {
      toast({
        title: t('gameNightCreate.submit.successToast'),
      });
      draftPersist.clear();
      router.push(`/game-nights/${id}`);
      return;
    }

    toast({
      title: t('gameNightCreate.submit.errorTitle'),
      description: t('gameNightCreate.submit.errorBody'),
      variant: 'destructive',
    });
    // PR #1302 review fix: log retry exhaustion to console so SRE has a
    // breadcrumb when investigating client-side submit failures. The toast
    // is the user-facing surface.
    if (lastError) {
      console.error('[gamenight.create] retry exhausted', lastError);
    }
  };

  const labels = useMemo(() => buildWizardLabels(t), [t]);

  return (
    <div className="container mx-auto px-4 py-6">
      <div className="mb-4">
        <label htmlFor="game-night-title" className="text-sm font-medium text-foreground">
          {t('gameNightsIndex.header.ctaNew')}
        </label>
        <input
          id="game-night-title"
          type="text"
          value={title}
          onChange={e => setTitle(e.target.value)}
          placeholder={t('gameNightCreate.title')}
          maxLength={200}
          className="mt-1 w-full rounded-md border border-border bg-card px-3 py-2 text-foreground"
          data-slot="game-night-create-title-input"
        />
      </div>

      <GameNightCreateWizard
        state={state}
        dispatch={dispatch}
        title={title}
        organizerName={organizerName}
        labels={labels}
        playerSearchQuery={playerSearchQuery}
        onPlayerSearchQueryChange={setPlayerSearchQuery}
        playerSearchResults={playerSearch.data ?? []}
        isSearchingPlayers={playerSearch.isFetching}
        regulars={regularsQuery.data ?? []}
        libraryGames={libraryGames}
        onSubmit={() => {
          void handleSubmit();
        }}
        isSubmitting={isSubmittingWithRetry || createMutation.isPending}
        isCheckingConflict={conflictCheck.isFetching}
      />

      {draftPersist.isPending && (
        <p
          role="status"
          aria-live="polite"
          className="mt-4 text-xs text-muted-foreground"
          data-slot="game-night-create-draft-status"
        >
          {t('gameNightCreate.draft.savingStatus')}
        </p>
      )}
    </div>
  );
}
