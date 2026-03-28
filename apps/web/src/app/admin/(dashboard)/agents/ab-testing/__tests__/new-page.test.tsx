import { describe, it, expect, vi } from 'vitest';

const mockRedirect = vi.fn();

vi.mock('next/navigation', () => ({
  redirect: (...args: unknown[]) => {
    mockRedirect(...args);
    throw new Error('NEXT_REDIRECT');
  },
  useRouter: () => ({ push: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => '/admin/agents/ab-testing/new',
}));

import NewAbTestPage from '../new/page';

describe('NewAbTestPage (redirect stub)', () => {
  it('redirects to playground compare tab', () => {
    expect(() => NewAbTestPage()).toThrow('NEXT_REDIRECT');
    expect(mockRedirect).toHaveBeenCalledWith('/admin/agents/playground?tab=compare');
  });
});
