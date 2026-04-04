'use client';

// ---------------------------------------------------------------------------
// PhotosContent
// ---------------------------------------------------------------------------

interface PhotosContentProps {
  sessionId: string;
}

export function PhotosContent({ sessionId: _sessionId }: PhotosContentProps) {
  return (
    <div className="flex flex-col gap-4 p-4">
      <h2 className="text-lg font-semibold tracking-tight">📸 Foto Sessione</h2>

      <p className="text-sm text-muted-foreground">Galleria foto disponibile a breve…</p>
    </div>
  );
}
