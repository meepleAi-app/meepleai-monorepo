'use client';

import { PhotosContent } from './contents/PhotosContent';
import { PlayersContent } from './contents/PlayersContent';
import { RulesAiContent } from './contents/RulesAiContent';
import { ScoresContent } from './contents/ScoresContent';
import { TimerContent } from './contents/TimerContent';

import type { SheetContext } from '../DashboardEngine';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

interface ContentProps {
  sessionId: string;
}

type ContentComponent = (props: ContentProps) => React.JSX.Element;

// ---------------------------------------------------------------------------
// Content map — routes SheetContext to the matching content module
// ---------------------------------------------------------------------------

const CONTENT_MAP: Record<SheetContext, ContentComponent> = {
  scores: ScoresContent,
  'rules-ai': RulesAiContent,
  timer: TimerContent,
  photos: PhotosContent,
  players: PlayersContent,
};

// ---------------------------------------------------------------------------
// SheetContent
// ---------------------------------------------------------------------------

interface SheetContentProps {
  context: SheetContext;
  sessionId: string;
}

export function SheetContent({ context, sessionId }: SheetContentProps) {
  const ContentModule = CONTENT_MAP[context];
  return <ContentModule sessionId={sessionId} />;
}
