'use client';

/**
 * SessionAssistantPanel — the in-game assistant in `/sessions/[id]/live` (#4238 A4).
 *
 * Host only: the page mounts it for the host, and the server refuses everyone else. The assistant
 * reads the game and proposes; nothing is written until the host confirms a card (spec §5.3).
 *
 * The cards of a turn sit under the answer that produced them (`messageId`). A card whose message
 * is not on screen yet (the turn is still streaming) sits at the end of the conversation.
 */

import { useId, useState, type FormEvent, type JSX } from 'react';

import { RuleSourceCard } from '@/components/chat-unified/RuleSourceCard';
import { Button } from '@/components/ui/primitives/button';
import { useSessionAssistant } from '@/hooks/queries/useSessionAssistant';
import { useTranslation } from '@/hooks/useTranslation';
import type { AssistantProposalDto } from '@/lib/api/schemas/live-sessions.schemas';
import { toRuleSourceCitations } from '@/lib/session-live/assistant-citations';
import { cn } from '@/lib/utils';

import { AssistantProposalCard } from './AssistantProposalCard';

const KNOWN_UNAVAILABLE = new Set([
  'assistant_disabled',
  'assistant_model_without_tools',
  'session_state_invalid',
]);
const KNOWN_TURN_ERRORS = new Set([
  'budget_paused',
  'turn_timeout',
  'assistant_disabled',
  'assistant_model_without_tools',
  'session_state_invalid',
]);
const KNOWN_TOOLS = new Set([
  'stato_partita',
  'proponi_punteggio',
  'proponi_turno',
  'cerca_regole',
]);

/** Mirrors `AssistantTurnRunner.MaxQuestionLength`. */
const MAX_QUESTION_LENGTH = 2000;

export interface SessionAssistantPanelProps {
  readonly sessionId: string;
  /** Tighter spacing for the mobile main column. */
  readonly compact?: boolean;
  readonly className?: string;
}

export function SessionAssistantPanel({
  sessionId,
  compact = false,
  className,
}: SessionAssistantPanelProps): JSX.Element {
  const { t, locale } = useTranslation();
  const headingId = useId();
  const inputId = useId();
  const [draft, setDraft] = useState('');
  const assistant = useSessionAssistant(sessionId, locale);

  const enabled = assistant.availability?.enabled === true;
  const canSend = enabled && !assistant.turn.running && draft.trim().length > 0;

  const messageIds = new Set(assistant.messages.map(m => m.id));
  const cardsOf = (messageId: string): AssistantProposalDto[] =>
    assistant.proposals.filter(p => p.messageId === messageId);
  const unattached = assistant.proposals.filter(p => !messageIds.has(p.messageId));

  function submit(event: FormEvent<HTMLFormElement>): void {
    event.preventDefault();
    if (!canSend) return;
    const question = draft;
    setDraft('');
    void assistant.ask(question);
  }

  function renderCard(proposal: AssistantProposalDto): JSX.Element {
    return (
      <AssistantProposalCard
        key={proposal.id}
        proposal={proposal}
        confirmBlocked={assistant.confirmingId !== null && assistant.confirmingId !== proposal.id}
        confirming={assistant.confirmingId === proposal.id}
        rejecting={assistant.rejectingId === proposal.id}
        errorCode={
          assistant.actionError?.proposalId === proposal.id
            ? (assistant.actionError.code ?? 'other')
            : null
        }
        onConfirm={points => void assistant.confirm(proposal.id, points)}
        onReject={() => void assistant.reject(proposal.id)}
      />
    );
  }

  const unavailableKey =
    assistant.availability?.code && KNOWN_UNAVAILABLE.has(assistant.availability.code)
      ? assistant.availability.code
      : 'other';
  const turnErrorKey =
    assistant.turn.errorCode && KNOWN_TURN_ERRORS.has(assistant.turn.errorCode)
      ? assistant.turn.errorCode
      : 'other';
  const toolKey =
    assistant.turn.tool === null
      ? 'start'
      : KNOWN_TOOLS.has(assistant.turn.tool)
        ? assistant.turn.tool
        : 'other';

  return (
    <section
      data-slot="session-assistant-panel"
      aria-labelledby={headingId}
      className={cn(
        'flex min-h-0 flex-col rounded-lg border border-border bg-card',
        compact ? 'gap-2 p-2' : 'gap-3 p-3',
        className
      )}
    >
      <h2 id={headingId} className="font-quicksand text-sm font-bold text-foreground">
        {t('pages.sessionLive.assistant.title')}
      </h2>

      {assistant.isLoading ? (
        <p className="text-sm text-muted-foreground">{t('pages.sessionLive.assistant.loading')}</p>
      ) : null}

      {assistant.loadError ? (
        <p role="alert" className="text-sm text-destructive">
          {t('pages.sessionLive.assistant.loadError')}
        </p>
      ) : null}

      {assistant.availability && !enabled ? (
        <p
          role="status"
          data-slot="session-assistant-unavailable"
          data-code={assistant.availability.code ?? ''}
          className="text-sm text-muted-foreground"
        >
          {t(`pages.sessionLive.assistant.unavailable.${unavailableKey}`)}
        </p>
      ) : null}

      {enabled && assistant.rulesLoaded === false ? (
        <p
          role="status"
          data-slot="session-assistant-rules-missing"
          className="rounded-md border border-border bg-muted px-2 py-1.5 text-xs text-foreground"
        >
          {t('pages.sessionLive.assistant.rulesMissing')}
        </p>
      ) : null}

      {enabled && assistant.messages.length === 0 && !assistant.turn.running ? (
        <p className="text-sm text-muted-foreground">{t('pages.sessionLive.assistant.empty')}</p>
      ) : null}

      {assistant.messages.length > 0 || unattached.length > 0 ? (
        <ol
          data-slot="session-assistant-conversation"
          className="flex min-h-0 flex-col gap-2 overflow-y-auto"
        >
          {assistant.messages.map(message => (
            <li key={message.id} data-role={message.role} className="flex flex-col gap-2">
              <p
                data-slot="session-assistant-message"
                className={cn(
                  'whitespace-pre-wrap rounded-md px-3 py-2 text-sm',
                  message.role === 'user'
                    ? 'self-end bg-muted text-foreground'
                    : 'self-start bg-background text-foreground'
                )}
              >
                {message.content}
              </p>
              {message.role === 'assistant' && message.citations && message.citations.length > 0 ? (
                <div data-slot="session-assistant-sources" className="self-start">
                  <RuleSourceCard citations={toRuleSourceCitations(message.citations)} />
                </div>
              ) : null}
              {message.role === 'assistant' ? cardsOf(message.id).map(renderCard) : null}
            </li>
          ))}
          {unattached.length > 0 ? (
            <li className="flex flex-col gap-2">{unattached.map(renderCard)}</li>
          ) : null}
        </ol>
      ) : null}

      <p role="status" aria-live="polite" className="min-h-4 text-xs text-muted-foreground">
        {assistant.turn.running ? t(`pages.sessionLive.assistant.tool.${toolKey}`) : null}
      </p>

      {assistant.turn.errorCode !== null && !assistant.turn.running ? (
        <p
          role="alert"
          data-slot="session-assistant-turn-error"
          className="text-sm text-destructive"
        >
          {t(`pages.sessionLive.assistant.turnError.${turnErrorKey}`)}
        </p>
      ) : null}

      <form onSubmit={submit} className="flex items-end gap-2">
        <label htmlFor={inputId} className="sr-only">
          {t('pages.sessionLive.assistant.inputLabel')}
        </label>
        <input
          id={inputId}
          type="text"
          data-slot="session-assistant-input"
          value={draft}
          maxLength={MAX_QUESTION_LENGTH}
          placeholder={t('pages.sessionLive.assistant.placeholder')}
          disabled={!enabled || assistant.turn.running}
          onChange={event => setDraft(event.target.value)}
          className="h-9 min-w-0 flex-1 rounded-md border border-border bg-background px-3 text-sm text-foreground"
        />
        <Button type="submit" size="sm" data-slot="session-assistant-send" disabled={!canSend}>
          {t('pages.sessionLive.assistant.send')}
        </Button>
      </form>
    </section>
  );
}
