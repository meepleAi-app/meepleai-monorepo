/**
 * Tests for PublicRsvpForm (issue #1169).
 *
 * Covers:
 *   - Pending mode: renders form, captures displayName + action submit
 *   - Already-responded mode: confirmation panel + change CTA
 *   - displayName validation: max 120, trimmed, optional
 *   - Submitting state: spinners + disabled CTAs
 *   - a11y: label association, aria-invalid on validation error
 */

import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import {
  PUBLIC_RSVP_DISPLAY_NAME_MAX_LENGTH,
  PublicRsvpForm,
  type PublicRsvpFormLabels,
  type PublicRsvpFormProps,
} from '../PublicRsvpForm';

const LABELS: PublicRsvpFormLabels = {
  sectionTitle: 'La tua risposta',
  displayNameLabel: 'Il tuo nome (opzionale)',
  displayNamePlaceholder: 'Es. Marco',
  displayNameHelper: "Aiuta l'organizzatore a riconoscerti.",
  displayNameTooLong: 'Massimo 120 caratteri.',
  accept: 'Confermo',
  decline: 'Non posso',
  submitting: 'Invio…',
  alreadyRespondedHeading: 'Hai già risposto',
  alreadyRespondedBody: 'Hai confermato come "Marco".',
  changeResponse: 'Cambia risposta',
};

function renderForm(overrides: Partial<PublicRsvpFormProps> = {}) {
  const props: PublicRsvpFormProps = {
    labels: LABELS,
    currentResponse: undefined,
    submittingAction: null,
    onSubmit: vi.fn(),
    ...overrides,
  };
  const utils = render(<PublicRsvpForm {...props} />);
  return { ...utils, onSubmit: props.onSubmit };
}

describe('PublicRsvpForm', () => {
  describe('pending mode', () => {
    it('renders both Accept and Decline buttons + the display-name input', () => {
      renderForm();
      expect(screen.getByRole('button', { name: 'Confermo' })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Non posso' })).toBeInTheDocument();
      expect(screen.getByLabelText(LABELS.displayNameLabel)).toBeInTheDocument();
    });

    it('submits Accepted with null displayName when input is empty', async () => {
      const user = userEvent.setup();
      const onSubmit = vi.fn();
      renderForm({ onSubmit });

      await user.click(screen.getByRole('button', { name: 'Confermo' }));
      expect(onSubmit).toHaveBeenCalledOnce();
      expect(onSubmit).toHaveBeenCalledWith('Accepted', null);
    });

    it('submits Declined with the typed displayName trimmed', async () => {
      const user = userEvent.setup();
      const onSubmit = vi.fn();
      renderForm({ onSubmit });

      await user.type(screen.getByLabelText(LABELS.displayNameLabel), '  Marco  ');
      await user.click(screen.getByRole('button', { name: 'Non posso' }));
      expect(onSubmit).toHaveBeenCalledWith('Declined', 'Marco');
    });

    it('shows validation error when displayName exceeds 120 characters', async () => {
      const user = userEvent.setup();
      const onSubmit = vi.fn();
      renderForm({ onSubmit });

      const input = screen.getByLabelText(LABELS.displayNameLabel);
      // Type 121 chars by triggering blur after a paste-like fill — userEvent
      // honors the maxLength={121} on the input, so we get exactly 121 chars.
      const overLength = 'a'.repeat(PUBLIC_RSVP_DISPLAY_NAME_MAX_LENGTH + 1);
      await user.click(input);
      await user.paste(overLength);
      await user.tab();

      expect(screen.getByText(LABELS.displayNameTooLong)).toBeInTheDocument();
      expect(input).toHaveAttribute('aria-invalid', 'true');

      // Clicking accept does NOT submit while invalid.
      await user.click(screen.getByRole('button', { name: 'Confermo' }));
      expect(onSubmit).not.toHaveBeenCalled();
    });

    it('disables CTAs while submitting and shows the submitting label on the in-flight button', () => {
      renderForm({ submittingAction: 'Accepted' });
      const acceptBtn = screen.getByRole('button', { name: LABELS.submitting });
      expect(acceptBtn).toBeDisabled();
      expect(acceptBtn).toHaveAttribute('data-pending', 'true');

      const declineBtn = screen.getByRole('button', { name: 'Non posso' });
      expect(declineBtn).toBeDisabled();
    });

    it('respects the `disabled` prop on all CTAs and the input', () => {
      renderForm({ disabled: true });
      expect(screen.getByRole('button', { name: 'Confermo' })).toBeDisabled();
      expect(screen.getByRole('button', { name: 'Non posso' })).toBeDisabled();
      expect(screen.getByLabelText(LABELS.displayNameLabel)).toBeDisabled();
    });

    it('associates the helper text via aria-describedby for a11y', () => {
      renderForm();
      const input = screen.getByLabelText(LABELS.displayNameLabel);
      const describedById = input.getAttribute('aria-describedby');
      expect(describedById).toBeTruthy();
      const helper = describedById ? document.getElementById(describedById) : null;
      expect(helper).toHaveTextContent(LABELS.displayNameHelper);
    });
  });

  describe('already-responded mode', () => {
    it('renders the heading, body, and change CTA instead of the form', () => {
      renderForm({ currentResponse: 'Accepted' });

      expect(screen.getByText(LABELS.alreadyRespondedHeading)).toBeInTheDocument();
      expect(screen.getByText(LABELS.alreadyRespondedBody)).toBeInTheDocument();
      expect(screen.getByRole('button', { name: LABELS.changeResponse })).toBeInTheDocument();

      // The accept/decline CTAs and the input MUST not be rendered.
      expect(screen.queryByRole('button', { name: 'Confermo' })).not.toBeInTheDocument();
      expect(screen.queryByRole('button', { name: 'Non posso' })).not.toBeInTheDocument();
      expect(screen.queryByLabelText(LABELS.displayNameLabel)).not.toBeInTheDocument();
    });

    it('exposes data-mode="responded" for the outer section', () => {
      renderForm({ currentResponse: 'Declined' });
      const root = screen.getByLabelText(LABELS.sectionTitle);
      expect(root).toHaveAttribute('data-mode', 'responded');
    });
  });
});
