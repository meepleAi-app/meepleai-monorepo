/**
 * HeroSection Component - Game Detail Hero Image
 *
 * 16:9 aspect ratio hero image with:
 * - Lazy-loaded Next/Image with blur placeholder
 * - Responsive sizing
 * - Gradient overlay for title readability
 * - Title overlay at bottom
 *
 * Issue #1841 (PAGE-005)
 */

'use client';

import React from 'react';
import Image from 'next/image';

// ============================================================================
// Types
// ============================================================================

export interface HeroSectionProps {
  /** Game title */
  title: string;
  /** Hero image URL (optional, fallback to placeholder) */
  imageUrl?: string | null;
  /** Publisher name (optional, displayed below title) */
  publisher?: string | null;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Generate SVG placeholder for games without image
 */
function getPlaceholderImage(): string {
  return 'data:image/svg+xml,%3Csvg xmlns="http://www.w3.org/2000/svg" width="1600" height="900" viewBox="0 0 1600 900"%3E%3Crect width="1600" height="900" fill="%23334155"/%3E%3Ctext x="50%25" y="50%25" dominant-baseline="middle" text-anchor="middle" font-family="sans-serif" font-size="128" fill="%239CA3AF"%3E%F0%9F%8E%B2%3C/text%3E%3C/svg%3E';
}

// ============================================================================
// Component
// ============================================================================

export function HeroSection({ title, imageUrl, publisher }: HeroSectionProps) {
  const imageSrc = imageUrl || getPlaceholderImage();

  return (
    <div
      className="relative w-full aspect-video overflow-hidden rounded-lg mb-6 shadow-lg"
      data-testid="hero-section"
    >
      {/* Hero Image */}
      <Image
        src={imageSrc}
        alt={title}
        fill
        sizes="(max-width: 768px) 100vw, (max-width: 1200px) 80vw, 70vw"
        className="object-cover"
        loading="lazy"
        placeholder="blur"
        blurDataURL={getPlaceholderImage()}
        priority={false}
      />

      {/* Gradient Overlay (for title readability) */}
      <div
        className="absolute inset-0 bg-gradient-to-t from-black/70 via-black/20 to-transparent"
        aria-hidden="true"
      />

      {/* Title Overlay */}
      <div className="absolute bottom-0 left-0 right-0 p-6 md:p-8">
        <h1 className="font-heading text-3xl md:text-4xl lg:text-5xl font-bold text-white drop-shadow-lg mb-2">
          {title}
        </h1>
        {publisher && (
          <p className="text-white/90 text-sm md:text-base drop-shadow-md">{publisher}</p>
        )}
      </div>
    </div>
  );
}
