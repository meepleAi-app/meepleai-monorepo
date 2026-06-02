import { AiCrumbs } from './AiCrumbs';
import { AiTopActions } from './AiTopActions';

export function AiTopBand() {
  return (
    <header
      aria-label="AI & Agents section"
      className="sticky top-0 z-40 -mx-6 flex items-center gap-4 border-b border-border/60 bg-background/80 px-6 py-3 backdrop-blur-md"
    >
      <div>
        <h1 className="font-quicksand text-xl font-extrabold tracking-tight text-foreground">
          AI &amp; Agents
        </h1>
        <AiCrumbs />
      </div>
      <div className="flex-1" />
      <AiTopActions />
    </header>
  );
}
