interface RatingProps {
  value: number;
  max?: number;
}

export function Rating({ value, max = 5 }: RatingProps) {
  const normalized = (value / max) * 5;
  const fullStars = Math.floor(normalized);
  const hasHalf = normalized - fullStars >= 0.25;
  const emptyStars = 5 - fullStars - (hasHalf ? 1 : 0);

  return (
    <div className="mt-0.5 flex items-center gap-0.5">
      {Array.from({ length: fullStars }, (_, i) => (
        <span key={`f${i}`} className="text-[0.78rem] text-amber-400">
          ★
        </span>
      ))}
      {hasHalf && <span className="text-[0.78rem] text-amber-400/50">★</span>}
      {Array.from({ length: emptyStars }, (_, i) => (
        <span key={`e${i}`} className="text-[0.78rem] text-[var(--mc-text-muted)]">
          ★
        </span>
      ))}
      <span className="ml-1 text-[0.7rem] font-semibold text-[var(--mc-text-secondary)]">
        {value.toFixed(1)}
      </span>
    </div>
  );
}
