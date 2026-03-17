/**
 * Card position calculation for 3D carousel effect
 */

export interface CardPosition {
  index: number;
  offset: number; // -2, -1, 0, 1, 2 from center
  scale: number;
  opacity: number;
  zIndex: number;
  translateX: number;
  blur: number;
  visible: boolean;
}

export function calculateCardPositions(
  totalCards: number,
  currentIndex: number,
  visibleCards: number
): CardPosition[] {
  const positions: CardPosition[] = [];
  const halfVisible = Math.floor(visibleCards / 2);

  for (let i = 0; i < totalCards; i++) {
    // Calculate circular offset from center
    let offset = i - currentIndex;

    // Handle infinite loop wrapping
    if (offset > totalCards / 2) offset -= totalCards;
    if (offset < -totalCards / 2) offset += totalCards;

    const absOffset = Math.abs(offset);
    const visible = absOffset <= halfVisible;

    // Calculate visual properties based on offset
    // v2: Center card 1.1x, side cards 0.85x (Issue #4612)
    const scale = visible ? (absOffset === 0 ? 1.1 : 0.85) : 0.5;
    const opacity = visible ? (absOffset === 0 ? 1 : 0.6) : 0;
    const zIndex = visible ? 10 - absOffset : 0;
    const blur = absOffset * 2;

    // Responsive spacing - percentage based
    const baseSpacing = 35; // % offset per card
    const translateX = offset * baseSpacing;

    positions.push({
      index: i,
      offset,
      scale,
      opacity,
      zIndex,
      translateX,
      blur,
      visible,
    });
  }

  return positions;
}
