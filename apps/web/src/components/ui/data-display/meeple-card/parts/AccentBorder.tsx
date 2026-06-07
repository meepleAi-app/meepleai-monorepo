import { entityHsl } from '../tokens';

import type { MeepleEntityType } from '../types';

interface AccentBorderProps {
  entity: MeepleEntityType;
}

export function AccentBorder({ entity }: AccentBorderProps) {
  return (
    <div
      className="absolute left-0 right-0 top-0 z-[5] h-[3px] transition-[height] duration-200 group-hover:h-[5px]"
      style={{ background: entityHsl(entity) }}
    />
  );
}
