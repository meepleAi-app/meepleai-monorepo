import { entityHsl } from '../tokens';
import type { MeepleEntityType } from '../types';

interface AccentBorderProps {
  entity: MeepleEntityType;
}

export function AccentBorder({ entity }: AccentBorderProps) {
  return (
    <div
      className="absolute bottom-0 left-0 top-0 z-[5] w-[3px] transition-[width] duration-200 group-hover:w-[5px]"
      style={{ background: entityHsl(entity) }}
    />
  );
}
