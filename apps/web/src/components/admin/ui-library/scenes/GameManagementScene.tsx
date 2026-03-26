'use client';

import { GameStatusBadge } from '@/components/admin/shared-games/GameStatusBadge';

// GameForm, PdfUploadSection, and CategoriesTable require API clients and
// React Hook Form contexts that are not available in isolation.
// We showcase GameStatusBadge fully and use descriptive placeholders for the rest.

function PlaceholderCard({
  title,
  description,
  note,
}: {
  title: string;
  description: string;
  note?: string;
}) {
  return (
    <div className="flex flex-col gap-2 rounded-xl border border-dashed border-border/50 bg-muted/20 p-5">
      <p className="font-quicksand text-sm font-semibold text-foreground">{title}</p>
      <p className="font-nunito text-xs text-muted-foreground">{description}</p>
      {note && (
        <span className="inline-flex w-fit items-center rounded-full border border-border/50 px-2.5 py-0.5 font-nunito text-[10px] text-muted-foreground">
          {note}
        </span>
      )}
    </div>
  );
}

export default function GameManagementScene() {
  return (
    <div className="space-y-8">
      {/* GameStatusBadge — fully rendered */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Game Status Badges
        </h3>
        <div className="flex flex-wrap gap-3">
          <GameStatusBadge status="Draft" />
          <GameStatusBadge status="PendingApproval" />
          <GameStatusBadge status="Published" />
          <GameStatusBadge status="Archived" />
        </div>
        <div className="flex flex-wrap gap-3">
          <GameStatusBadge status="Draft" size="sm" />
          <GameStatusBadge status="PendingApproval" size="sm" />
          <GameStatusBadge status="Published" size="sm" />
          <GameStatusBadge status="Archived" size="sm" />
        </div>
      </div>

      {/* Complex form components — require API/form context */}
      <div className="space-y-3">
        <h3 className="font-quicksand text-base font-semibold text-foreground">
          Form Components (require context)
        </h3>
        <div className="grid gap-3 sm:grid-cols-2">
          <PlaceholderCard
            title="GameForm"
            description="Full game creation/editing form with search integration, image upload, metadata tags, and mechanics."
            note="requires useApiClient + react-hook-form"
          />
          <PlaceholderCard
            title="PdfUploadSection"
            description="Drag-and-drop PDF upload with XHR progress tracking, processing status, and document list."
            note="requires useApiClient + admin session"
          />
          <PlaceholderCard
            title="CategoriesTable"
            description="Sortable table of game categories with inline editing, reordering, and delete confirmation."
            note="requires useApiClient"
          />
          <PlaceholderCard
            title="BggSearchPanel"
            description="External catalog integration for importing game metadata, images, and expansion data."
            note="requires useApiClient"
          />
        </div>
      </div>
    </div>
  );
}
