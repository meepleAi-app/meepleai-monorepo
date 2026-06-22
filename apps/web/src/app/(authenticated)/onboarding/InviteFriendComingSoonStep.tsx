'use client';

/**
 * InviteFriendComingSoonStep — placeholder for /onboarding 3-step wizard (P3).
 *
 * Asse D follow-up P3 (umbrella #1895, sub-issue #1899). Skip-only step that
 * advertises a future feature; the wizard treats this as `optional: true` so
 * the Skip + Complete buttons both close the wizard cleanly.
 *
 * Feature implementation (invite friend flow) is deferred to a sub-issue.
 */
export function InviteFriendComingSoonStep() {
  return (
    <div className="flex flex-col items-center gap-4 py-8" data-testid="invite-friend-coming-soon">
      <h2 className="font-quicksand text-2xl font-semibold text-foreground">Invita un amico</h2>
      <p className="text-center text-muted-foreground">
        La funzionalita di invito amici sara disponibile in una release futura.
      </p>
      <p className="text-center text-sm text-muted-foreground">
        Clicca <strong>Skip</strong> per saltare questo step e <strong>Complete</strong> per
        terminare l&apos;onboarding.
      </p>
    </div>
  );
}
