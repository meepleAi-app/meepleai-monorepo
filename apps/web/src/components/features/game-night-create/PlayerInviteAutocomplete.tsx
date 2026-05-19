/**
 * PlayerInviteAutocomplete — Step 3 ("Chi") component.
 * Issue #950 W3 Components. Spec §5 (C4) + §12 Scenario 3 (mixed invitees).
 *
 * Pure component: orchestrator owns wizard state + react-query hooks and
 * passes resolved data (searchResults, regulars) as props.
 */

'use client';

import { useId, useState, type ReactElement } from 'react';

import type { UserSearchResult } from '@/lib/api/schemas/auth.schemas';
import type { RegularDto } from '@/lib/api/schemas/game-nights.schemas';
import type { Invitee, UserInvitee } from '@/lib/game-nights/wizard-types';
import { inviteeKey } from '@/lib/game-nights/wizard-types';

export interface PlayerInviteAutocompleteLabels {
  readonly label: string;
  readonly searchPlaceholder: string;
  readonly searchAriaLabel: string;
  readonly regularsTitle: string;
  readonly regularsHelper: string;
  readonly regularsEmpty: string;
  readonly addRegular: string;
  readonly remove: string;
  readonly addEmail: string;
  readonly emailInvalid: string;
  readonly emailDuplicate: string;
  readonly inviteeCount: (count: number) => string;
  readonly limitWarning: (max: number) => string;
}

export interface PlayerInviteAutocompleteProps {
  readonly invitees: readonly Invitee[];
  readonly searchResults: readonly UserSearchResult[];
  readonly regulars: readonly RegularDto[];
  readonly query: string;
  readonly onQueryChange: (q: string) => void;
  readonly onAddInvitee: (invitee: Invitee) => void;
  readonly onRemoveInvitee: (key: string) => void;
  readonly isSearching?: boolean;
  readonly maxCombinedInvitees?: number;
  readonly labels: PlayerInviteAutocompleteLabels;
}

const EMAIL_REGEX = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function isEmail(value: string): boolean {
  return EMAIL_REGEX.test(value.trim());
}

export function PlayerInviteAutocomplete({
  invitees,
  searchResults,
  regulars,
  query,
  onQueryChange,
  onAddInvitee,
  onRemoveInvitee,
  isSearching = false,
  maxCombinedInvitees = 49,
  labels,
}: PlayerInviteAutocompleteProps): ReactElement {
  const inputId = useId();
  const [emailError, setEmailError] = useState<string | null>(null);

  const atLimit = invitees.length >= maxCombinedInvitees;
  const queryLooksLikeEmail = isEmail(query);
  const existingKeys = new Set(invitees.map(inviteeKey));

  const handleAddEmail = (): void => {
    const trimmed = query.trim().toLowerCase();
    if (!isEmail(trimmed)) {
      setEmailError(labels.emailInvalid);
      return;
    }
    if (existingKeys.has(inviteeKey({ kind: 'email', address: trimmed }))) {
      setEmailError(labels.emailDuplicate);
      return;
    }
    setEmailError(null);
    onAddInvitee({ kind: 'email', address: trimmed });
    onQueryChange('');
  };

  const handleAddUser = (user: UserSearchResult): void => {
    const invitee: UserInvitee = {
      kind: 'user',
      id: user.id,
      displayName: user.displayName,
      email: user.email,
    };
    if (existingKeys.has(inviteeKey(invitee))) return;
    onAddInvitee(invitee);
    onQueryChange('');
  };

  const handleAddRegular = (regular: RegularDto): void => {
    handleAddUser({
      id: regular.id,
      displayName: regular.displayName,
      email: regular.email,
    });
  };

  return (
    <section
      data-slot="game-night-create-step3"
      aria-labelledby={`${inputId}-label`}
      className="flex flex-col gap-4"
    >
      <label
        id={`${inputId}-label`}
        htmlFor={inputId}
        className="text-sm font-medium text-foreground"
      >
        {labels.label}
      </label>

      <div className="flex flex-col gap-2">
        <input
          id={inputId}
          type="search"
          value={query}
          onChange={e => {
            setEmailError(null);
            onQueryChange(e.target.value);
          }}
          placeholder={labels.searchPlaceholder}
          aria-label={labels.searchAriaLabel}
          disabled={atLimit}
          className="rounded-md border border-border bg-card px-3 py-2 text-sm text-foreground"
          data-slot="game-night-create-step3-search"
        />
        {queryLooksLikeEmail && (
          <button
            type="button"
            onClick={handleAddEmail}
            disabled={atLimit}
            className="self-start text-xs font-medium text-primary underline"
            data-slot="game-night-create-step3-add-email"
          >
            {labels.addEmail}
          </button>
        )}
        {emailError && (
          <p role="alert" className="text-xs text-destructive">
            {emailError}
          </p>
        )}
      </div>

      {searchResults.length > 0 && (
        <ul
          role="listbox"
          aria-label={labels.searchAriaLabel}
          className="flex flex-col gap-1 rounded-md border border-border bg-card"
          data-slot="game-night-create-step3-results"
        >
          {searchResults.map(result => {
            const alreadyAdded = existingKeys.has(
              inviteeKey({
                kind: 'user',
                id: result.id,
                displayName: result.displayName,
                email: result.email,
              })
            );
            return (
              <li key={result.id} role="option" aria-selected={false}>
                <button
                  type="button"
                  onClick={() => handleAddUser(result)}
                  disabled={alreadyAdded || atLimit}
                  className="flex w-full items-center justify-between px-3 py-2 text-left text-sm text-foreground hover:bg-muted"
                >
                  <span>{result.displayName}</span>
                  <span className="text-xs text-muted-foreground">{result.email}</span>
                </button>
              </li>
            );
          })}
        </ul>
      )}

      {isSearching && (
        <p role="status" aria-live="polite" className="text-xs text-muted-foreground">
          {labels.searchAriaLabel}
        </p>
      )}

      {!query && (
        <div data-slot="game-night-create-step3-regulars">
          <p className="text-sm font-medium text-foreground">{labels.regularsTitle}</p>
          <p className="text-xs text-muted-foreground">{labels.regularsHelper}</p>
          {regulars.length === 0 ? (
            <p className="mt-2 text-xs text-muted-foreground">{labels.regularsEmpty}</p>
          ) : (
            <ul className="mt-2 flex flex-col gap-1">
              {regulars.map(regular => {
                const alreadyAdded = existingKeys.has(
                  inviteeKey({
                    kind: 'user',
                    id: regular.id,
                    displayName: regular.displayName,
                    email: regular.email,
                  })
                );
                return (
                  <li key={regular.id} className="flex items-center justify-between">
                    <span className="text-sm text-foreground">{regular.displayName}</span>
                    <button
                      type="button"
                      onClick={() => handleAddRegular(regular)}
                      disabled={alreadyAdded || atLimit}
                      className="text-xs font-medium text-primary underline disabled:opacity-50"
                    >
                      {labels.addRegular}
                    </button>
                  </li>
                );
              })}
            </ul>
          )}
        </div>
      )}

      <div data-slot="game-night-create-step3-invitees">
        <p className="text-sm font-medium text-foreground">
          {labels.inviteeCount(invitees.length)}
        </p>
        {invitees.length > 0 && (
          <ul className="mt-2 flex flex-wrap gap-2">
            {invitees.map(invitee => {
              const key = inviteeKey(invitee);
              const display = invitee.kind === 'user' ? invitee.displayName : invitee.address;
              return (
                <li
                  key={key}
                  className="flex items-center gap-2 rounded-full border border-border bg-card px-3 py-1 text-xs"
                >
                  <span className="text-foreground">{display}</span>
                  <button
                    type="button"
                    onClick={() => onRemoveInvitee(key)}
                    aria-label={`${labels.remove} ${display}`}
                    className="text-muted-foreground hover:text-destructive"
                  >
                    ×
                  </button>
                </li>
              );
            })}
          </ul>
        )}
        {atLimit && (
          <p role="alert" className="mt-2 text-xs text-warning">
            {labels.limitWarning(maxCombinedInvitees)}
          </p>
        )}
      </div>
    </section>
  );
}
