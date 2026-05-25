import { Content } from './_content';

import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'Encounter Book | MeepleAI',
};

/**
 * Encounter cheatsheet route — Issue #1484.
 *
 * The parse inputs (`photoId`, target paragraph, `gameBookId`) and the optional
 * originating-story context (`from`, `excerpt`) arrive as query params from the
 * preceding runthrough step. They are resolved server-side and forwarded as
 * typed props so the client orchestrator avoids a `useSearchParams` Suspense
 * boundary.
 */
export default async function EncounterPage({
  params,
  searchParams,
}: {
  params: Promise<{ gameId: string; campaignId: string }>;
  searchParams: Promise<Record<string, string | string[] | undefined>>;
}) {
  const { gameId, campaignId } = await params;
  const sp = await searchParams;

  const str = (v: string | string[] | undefined): string => (typeof v === 'string' ? v : '');

  // `to` is the target encounter paragraph per the mockup URL contract
  // (`…/encounter?from=147&to=218`); `paragraphNumber` is an explicit fallback
  // matching the BE body field name. `from` is the originating Storybook
  // paragraph (bare integer) used only for the optional story-context card.
  const paragraphNumber = Number.parseInt(str(sp.to) || str(sp.paragraphNumber), 10) || 0;
  const fromLabel = str(sp.from) || null;
  const excerpt = str(sp.excerpt) || null;

  return (
    <Content
      gameId={gameId}
      campaignId={campaignId}
      photoId={str(sp.photoId)}
      paragraphNumber={paragraphNumber}
      gameBookId={str(sp.gameBookId)}
      fromLabel={fromLabel}
      excerpt={excerpt}
    />
  );
}
