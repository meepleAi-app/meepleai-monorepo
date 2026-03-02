'use client';

import { BookOpen, ExternalLink, Tag, Zap } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';

export interface CatalogDetailsSectionProps {
  description?: string | null;
  categories?: Array<{ id: string; name: string; slug?: string }>;
  mechanics?: Array<{ id: string; name: string; slug?: string }>;
  designers?: Array<{ id: string; name: string }>;
  bggId?: number | null;
  gameTitle: string;
}

/**
 * CatalogDetailsSection — shows description, categories, mechanics and BGG link
 * for catalog games in the library game detail page.
 * Only renders when catalog data (description, categories, bggId) is available.
 */
export function CatalogDetailsSection({
  description,
  categories,
  mechanics,
  designers,
  bggId,
  gameTitle,
}: CatalogDetailsSectionProps) {
  const hasDescription = !!description?.trim();
  const hasCategories = (categories?.length ?? 0) > 0;
  const hasMechanics = (mechanics?.length ?? 0) > 0;
  const hasDesigners = (designers?.length ?? 0) > 0;

  // Don't render section if no catalog data is available (private games)
  if (!hasDescription && !hasCategories && !hasMechanics && !hasDesigners && !bggId) return null;

  return (
    <section className="space-y-4">
      {/* Description */}
      {hasDescription && (
        <Card className="rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] shadow-[0_4px_20px_rgba(45,42,38,0.08)]">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-base font-semibold text-[hsl(25,95%,38%)]">
              <BookOpen className="h-4 w-4" />
              Descrizione
            </CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-sm leading-relaxed text-foreground/80">{description}</p>
          </CardContent>
        </Card>
      )}

      {/* Categories, Mechanics & Designers */}
      {(hasCategories || hasMechanics || hasDesigners) && (
        <Card className="rounded-3xl border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9] shadow-[0_4px_20px_rgba(45,42,38,0.08)]">
          <CardContent className="pt-5 space-y-4">
            {hasCategories && (
              <div className="space-y-2">
                <div className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  <Tag className="h-3.5 w-3.5" />
                  Categorie
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {categories!.map(cat => (
                    <Badge key={cat.id} variant="secondary" className="rounded-full text-xs">
                      {cat.name}
                    </Badge>
                  ))}
                </div>
              </div>
            )}

            {hasMechanics && (
              <div className="space-y-2">
                <div className="flex items-center gap-1.5 text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  <Zap className="h-3.5 w-3.5" />
                  Meccaniche
                </div>
                <div className="flex flex-wrap gap-1.5">
                  {mechanics!.map(mec => (
                    <Badge
                      key={mec.id}
                      variant="outline"
                      className="rounded-full text-xs border-orange-200 text-orange-700 dark:border-orange-800 dark:text-orange-400"
                    >
                      {mec.name}
                    </Badge>
                  ))}
                </div>
              </div>
            )}

            {hasDesigners && (
              <div className="space-y-1">
                <div className="text-xs font-semibold uppercase tracking-wider text-muted-foreground">
                  Autori
                </div>
                <p className="text-sm text-foreground/80">
                  {designers!.map(d => d.name).join(', ')}
                </p>
              </div>
            )}
          </CardContent>
        </Card>
      )}

      {/* BGG Link */}
      {bggId && (
        <div className="flex items-center justify-end">
          <a
            href={`https://boardgamegeek.com/boardgame/${bggId}`}
            target="_blank"
            rel="noopener noreferrer"
            className="inline-flex items-center gap-1.5 text-sm text-muted-foreground hover:text-foreground transition-colors"
          >
            <ExternalLink className="h-3.5 w-3.5" />
            Vedi {gameTitle} su BoardGameGeek
          </a>
        </div>
      )}
    </section>
  );
}
