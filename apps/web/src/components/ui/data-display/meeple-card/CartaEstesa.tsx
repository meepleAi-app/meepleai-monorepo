'use client';

import Image from 'next/image';

import { cn } from '@/lib/utils';

interface CartaEstesaStat {
  label: string;
  value: string;
  color: string; // HSL without wrapper, e.g. "25 95% 45%"
}

interface CartaEstesaProps {
  title: string;
  subtitle?: string;
  imageUrl?: string;
  rating?: number;
  entityColor: string; // HSL without wrapper
  stats?: CartaEstesaStat[];
  tags?: string[];
  description?: string;
  linkCount?: number;
  children?: React.ReactNode; // Slot for CompactIconBar
  className?: string;
}

export function CartaEstesa({
  title,
  subtitle,
  imageUrl,
  rating,
  entityColor,
  stats,
  tags,
  description,
  linkCount,
  children,
  className,
}: CartaEstesaProps) {
  return (
    <div
      data-testid="carta-estesa"
      className={cn(
        'flex flex-col overflow-hidden rounded-[20px] border border-[rgba(180,130,80,0.1)] bg-card',
        'shadow-[var(--shadow-warm-lg)]',
        className
      )}
    >
      {/* Cover Hero */}
      <div className="relative h-[200px] w-full overflow-hidden lg:h-[180px]">
        {imageUrl ? (
          <Image src={imageUrl} alt={title} fill className="object-cover" />
        ) : (
          <div
            className="flex h-full w-full items-center justify-center"
            style={{
              background: `linear-gradient(135deg, hsl(${entityColor}), hsl(${entityColor} / 0.7))`,
            }}
          >
            <span className="text-5xl opacity-30">🎲</span>
          </div>
        )}
        {/* Gradient overlay */}
        <div className="absolute bottom-0 left-0 right-0 h-[100px] bg-gradient-to-t from-black/65 to-transparent" />
        {/* Title area */}
        <div className="absolute bottom-3 left-4">
          <h1 className="font-quicksand text-2xl font-extrabold text-white [text-shadow:0_2px_8px_rgba(0,0,0,0.4)]">
            {title}
          </h1>
          {subtitle && (
            <p className="text-[11px] text-white/80 [text-shadow:0_1px_4px_rgba(0,0,0,0.3)]">
              {subtitle}
            </p>
          )}
        </div>
        {/* Rating badge */}
        {rating !== undefined && (
          <div className="absolute right-3 top-3 flex items-center gap-1 rounded-[14px] bg-black/40 px-3 py-1 backdrop-blur-[8px]">
            <span className="text-xs text-amber-300">⭐</span>
            <span className="font-quicksand text-[13px] font-bold text-white">{rating}</span>
          </div>
        )}
        {/* Link badge */}
        {linkCount !== undefined && linkCount > 0 && (
          <div className="absolute left-3 top-3 flex items-center gap-1 rounded-[14px] border border-white/50 bg-white/80 px-2.5 py-0.5 shadow-sm backdrop-blur-[8px]">
            <span className="text-[11px]">🔗</span>
            <span className="font-nunito text-[11px] font-semibold text-slate-700">
              {linkCount}
            </span>
          </div>
        )}
      </div>

      {/* Body */}
      <div className="flex flex-1 flex-col gap-2.5 p-3.5 pt-3.5 sm:p-4">
        {/* Stats row */}
        {stats && stats.length > 0 && (
          <div className="flex gap-1.5">
            {stats.map(stat => (
              <div
                key={stat.label}
                className="flex-1 rounded-xl border bg-white/70 px-1 py-2 text-center"
                style={{ borderColor: `hsl(${stat.color} / 0.15)` }}
              >
                <div
                  className="font-quicksand text-base font-bold"
                  style={{ color: `hsl(${stat.color})` }}
                >
                  {stat.value}
                </div>
                <div className="mt-0.5 text-[8px] text-muted-foreground">{stat.label}</div>
              </div>
            ))}
          </div>
        )}

        {/* Tags */}
        {tags && tags.length > 0 && (
          <div className="flex flex-wrap gap-1.5">
            {tags.map(tag => (
              <span
                key={tag}
                className="rounded-[14px] px-2.5 py-0.5 font-quicksand text-[10px] font-semibold"
                style={{
                  backgroundColor: `hsl(${entityColor} / 0.08)`,
                  color: `hsl(${entityColor})`,
                }}
              >
                {tag}
              </span>
            ))}
          </div>
        )}

        {/* Description */}
        {description && (
          <p className="text-xs leading-relaxed text-muted-foreground">{description}</p>
        )}
      </div>

      {/* Children slot (CompactIconBar) */}
      {children}
    </div>
  );
}
