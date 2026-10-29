/**
 * #4727: the user search returns the address masked (j***n@example.com). The client schema used to require
 * a valid email, so a masked one — which is not valid — made every search result fail to parse.
 */

import { describe, it, expect } from 'vitest';

import { UserSearchResultSchema } from '../auth.schemas';

const id = '11111111-1111-4111-8111-111111111111';

describe('UserSearchResultSchema — masked email (#4727)', () => {
  it.each(['j***n@example.com', 'c***a@example.com', '***@example.com', '***@***.***'])(
    'accepts the masked address %s',
    email => {
      const parsed = UserSearchResultSchema.safeParse({ id, displayName: 'Carla', email });

      expect(parsed.success).toBe(true);
    }
  );

  it('still accepts a whole address, so the client keeps working while the server is rolled out', () => {
    const parsed = UserSearchResultSchema.safeParse({
      id,
      displayName: 'Carla',
      email: 'carla@example.com',
    });

    expect(parsed.success).toBe(true);
  });

  it('keeps requiring an id, a name and some address', () => {
    expect(
      UserSearchResultSchema.safeParse({ id: 'x', displayName: 'Carla', email: 'c***a@x.it' })
        .success
    ).toBe(false);
    expect(
      UserSearchResultSchema.safeParse({ id, displayName: '', email: 'c***a@x.it' }).success
    ).toBe(false);
    expect(UserSearchResultSchema.safeParse({ id, displayName: 'Carla', email: '' }).success).toBe(
      false
    );
  });
});
