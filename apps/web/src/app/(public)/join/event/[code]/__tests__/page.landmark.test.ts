import { readFileSync } from 'node:fs';
import { join } from 'node:path';

import { describe, expect, it } from 'vitest';

// #4910. The Suspense fallback of this page opened a <main>, inside the <main> the public layout provides: for as long
// as the invitation was loading, the document had two. The fallback only renders while the page is pending, so the
// DOM cannot be asserted without suspending the server component; the source is read instead, as the architecture
// gates do (vitest runs from apps/web).

const PAGE = join(process.cwd(), 'src', 'app', '(public)', 'join', 'event', '[code]', 'page.tsx');

describe('/join/event/[code] page shell', () => {
  it('does not open a <main>: the public layout owns the landmark', () => {
    const source = readFileSync(PAGE, 'utf8');

    expect(source).not.toMatch(/<main[\s>]/);
  });
});
