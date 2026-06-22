import { KbCrumbs } from './KbCrumbs';
import { KbTopActions } from './KbTopActions';

export function KbTopBand() {
  return (
    <header
      aria-label="Knowledge Base section"
      className="sticky top-0 z-40 -mx-6 flex items-center gap-4 border-b border-border/60 bg-background/80 px-6 py-3 backdrop-blur-md"
    >
      <div>
        <h1 className="font-quicksand text-xl font-extrabold tracking-tight text-foreground">
          Knowledge Base
        </h1>
        <KbCrumbs />
      </div>
      <div className="flex-1" />
      <KbTopActions />
    </header>
  );
}
