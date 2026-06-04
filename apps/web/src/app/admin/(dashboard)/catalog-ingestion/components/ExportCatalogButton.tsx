import { Download } from 'lucide-react';

export function ExportCatalogButton() {
  return (
    <a
      href="/api/v1/admin/catalog-ingestion/excel-export"
      className="inline-flex items-center gap-1.5 rounded-md border border-border bg-background px-3 py-1.5 text-xs font-medium text-foreground hover:bg-muted"
    >
      <Download className="h-3.5 w-3.5" />
      Export catalog
    </a>
  );
}
