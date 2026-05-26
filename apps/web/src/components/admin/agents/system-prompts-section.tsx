/* eslint-disable local/no-hardcoded-color-utility -- admin CRUD chrome: text-white / button color on style-prop colored bg or admin-decorative inline gradient. DS-13c admin scope (--admin-* decision deferred to DS-15). */
'use client';

import { EyeIcon, PencilIcon } from 'lucide-react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/primitives/button';
import { useAdminPromptTemplates } from '@/hooks/queries/useAdminPromptTemplates';
import {
  AdminPromptTemplatesApiError,
  type PromptTemplateDto,
} from '@/lib/api/admin-prompt-templates';
import { logger } from '@/lib/logger';

function describeError(err: unknown): string {
  if (err instanceof AdminPromptTemplatesApiError) {
    return err.serverMessage;
  }
  if (err instanceof Error) {
    return err.message;
  }
  return 'Unknown error';
}

function formatCreatedAt(iso: string): string {
  try {
    const date = new Date(iso);
    if (Number.isNaN(date.getTime())) {
      return iso;
    }
    return date.toISOString().slice(0, 10); // YYYY-MM-DD (locale-independent for test stability)
  } catch {
    return iso;
  }
}

interface PromptCardProps {
  readonly prompt: PromptTemplateDto;
}

function PromptCard({ prompt }: PromptCardProps) {
  // tokenCount placeholder: BE doesn't expose the active version content on
  // the list query (would require a per-row round-trip via
  // `GET /admin/prompts/{id}/versions`). Phase 1b shows `versionCount` as a
  // proxy until the dedicated editor surface lands.
  const versionLabel = prompt.versionCount === 1 ? '1 version' : `${prompt.versionCount} versions`;

  return (
    <div className="bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl p-6 border border-border/50 dark:border-zinc-700/50">
      <div className="flex items-start justify-between mb-3">
        <h3 className="font-quicksand text-lg font-bold text-foreground dark:text-zinc-100">
          {prompt.name}
        </h3>
        <Badge
          variant="outline"
          className="bg-amber-100 text-amber-900 dark:bg-amber-900/30 dark:text-amber-300"
        >
          {versionLabel}
        </Badge>
      </div>
      <p className="text-sm text-muted-foreground dark:text-muted-foreground mb-4">
        {prompt.description ?? '—'}
      </p>
      <div className="flex items-center justify-between">
        <span className="text-xs text-muted-foreground dark:text-muted-foreground">
          Created {formatCreatedAt(prompt.createdAt)}
        </span>
        <div className="flex gap-2">
          <Button
            variant="ghost"
            size="sm"
            className="h-8 w-8 p-0"
            onClick={() => {
              // Phase 1b stub — dedicated editor surface tracked in #1442 Phase 2.
              logger.debug(`View prompt template ${prompt.id} (Phase 1b stub)`);
            }}
            aria-label={`View prompt ${prompt.name}`}
          >
            <EyeIcon className="h-4 w-4" />
            <span className="sr-only">View prompt</span>
          </Button>
          <Button
            variant="ghost"
            size="sm"
            className="h-8 w-8 p-0"
            onClick={() => {
              logger.debug(`Edit prompt template ${prompt.id} (Phase 1b stub)`);
            }}
            aria-label={`Edit prompt ${prompt.name}`}
          >
            <PencilIcon className="h-4 w-4" />
            <span className="sr-only">Edit prompt</span>
          </Button>
        </div>
      </div>
    </div>
  );
}

export function SystemPromptsSection() {
  const promptsQuery = useAdminPromptTemplates();

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between">
        <h2 className="font-quicksand text-xl font-bold text-foreground dark:text-zinc-100">
          System Prompts
        </h2>
      </div>

      {promptsQuery.isLoading && (
        <div className="text-sm text-muted-foreground dark:text-muted-foreground py-8 text-center">
          Loading system prompts…
        </div>
      )}

      {promptsQuery.isError && (
        <div
          role="alert"
          className="rounded-md border border-red-200 dark:border-red-800 bg-red-50/80 dark:bg-red-900/40 px-4 py-3 text-sm text-red-700 dark:text-red-200"
        >
          Failed to load prompts: {describeError(promptsQuery.error)}
        </div>
      )}

      {!promptsQuery.isLoading &&
        !promptsQuery.isError &&
        (promptsQuery.data?.length ?? 0) === 0 && (
          <div className="text-sm text-muted-foreground dark:text-muted-foreground py-8 text-center">
            No system prompts configured yet.
          </div>
        )}

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {(promptsQuery.data ?? []).map(prompt => (
          <PromptCard key={prompt.id} prompt={prompt} />
        ))}
      </div>
    </div>
  );
}
