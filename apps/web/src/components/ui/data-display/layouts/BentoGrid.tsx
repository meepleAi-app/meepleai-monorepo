interface BentoGridProps {
  children: React.ReactNode;
  className?: string;
}

export function BentoGrid({ children, className = '' }: BentoGridProps) {
  return (
    <div
      className={`grid gap-6 ${className}`}
      style={{
        gridTemplateColumns: 'repeat(4, 1fr)',
        gridTemplateAreas: `"feat feat agent session" "feat feat event kb" "coll coll player game"`,
      }}
    >
      {children}
    </div>
  );
}

interface BentoGridItemProps {
  area: string;
  children: React.ReactNode;
  className?: string;
}

export function BentoGridItem({ area, children, className = '' }: BentoGridItemProps) {
  return (
    <div style={{ gridArea: area }} className={className}>
      {children}
    </div>
  );
}
