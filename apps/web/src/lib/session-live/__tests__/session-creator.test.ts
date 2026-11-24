import { describe, expect, it } from 'vitest';

import { isSessionCreator } from '../session-creator';

describe('isSessionCreator (#4763)', () => {
  const session = { createdByUserId: 'user-creator' };

  it('is true for the user who created the session', () => {
    expect(isSessionCreator(session, 'user-creator')).toBe(true);
  });

  it('is false for anyone else, whatever seat they hold', () => {
    expect(isSessionCreator(session, 'user-host-seat')).toBe(false);
  });

  it.each([null, undefined, ''])('is false without a signed-in user (%s)', userId => {
    expect(isSessionCreator(session, userId)).toBe(false);
  });

  it.each([null, undefined])('is false without a session (%s)', missing => {
    expect(isSessionCreator(missing, 'user-creator')).toBe(false);
  });

  it('does not treat an empty creator id as matching an empty user id', () => {
    expect(isSessionCreator({ createdByUserId: '' }, '')).toBe(false);
  });
});
