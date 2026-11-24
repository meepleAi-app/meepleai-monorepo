/**
 * #4763 — the one rule for «who controls a live game»: its creator.
 *
 * The server lets only the session's creator (`CreatedByUserId`) start, pause, resume, end and
 * save a live session and add or remove its players; every other participant gets 403, whatever
 * their seat. The Host *role* belongs to the first seat, which on a game planned by someone else
 * is not the creator, so no surface may decide these controls from the role.
 */
export function isSessionCreator(
  session: { readonly createdByUserId: string } | null | undefined,
  userId: string | null | undefined
): boolean {
  return session != null && userId != null && userId !== '' && session.createdByUserId === userId;
}
