/**
 * Game Hero Section Component (Issue #2833)
 *
 * Displays game cover image, title, and status badge
 */

import Image from 'next/image';

interface GameHeroSectionProps {
  title: string;
  imageUrl?: string | null;
  status: 'Nuovo' | 'InPrestito' | 'Wishlist' | 'Owned';
  publisher?: string | null;
  year?: number | null;
}

const statusColors = {
  Nuovo: 'bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-200',
  InPrestito: 'bg-yellow-100 text-yellow-800 dark:bg-yellow-900 dark:text-yellow-200',
  Wishlist: 'bg-purple-100 text-purple-800 dark:bg-purple-900 dark:text-purple-200',
  Owned: 'bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-200',
} as const;

export function GameHeroSection({
  title,
  imageUrl,
  status,
  publisher,
  year,
}: GameHeroSectionProps) {
  return (
    <div className="relative w-full">
      {/* Cover Image */}
      <div className="relative h-64 md:h-80 w-full overflow-hidden rounded-lg bg-neutral-100 dark:bg-neutral-800">
        {imageUrl ? (
          <Image src={imageUrl} alt={`${title} cover`} fill className="object-cover" priority />
        ) : (
          <div className="flex items-center justify-center h-full">
            <span className="text-neutral-400 dark:text-neutral-600">No image</span>
          </div>
        )}
      </div>

      {/* Title & Status */}
      <div className="mt-4 space-y-2">
        <div className="flex items-start justify-between gap-4">
          <h1 className="text-3xl font-bold text-neutral-900 dark:text-neutral-50">{title}</h1>
          <span
            className={`px-3 py-1 rounded-full text-sm font-medium ${statusColors[status]}`}
            role="status"
            aria-label={`Game status: ${status}`}
          >
            {status}
          </span>
        </div>

        {(publisher || year) && (
          <p className="text-sm text-neutral-600 dark:text-neutral-400">
            {publisher && <span>{publisher}</span>}
            {publisher && year && <span className="mx-2">•</span>}
            {year && <span>{year}</span>}
          </p>
        )}
      </div>
    </div>
  );
}
