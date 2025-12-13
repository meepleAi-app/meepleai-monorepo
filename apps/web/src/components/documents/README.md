# Document Upload Components

Frontend components for multi-document upload functionality (Issue #2051).

## Components

### DocumentBadge

Displays a color-coded badge for document types.

```tsx
import { DocumentBadge } from '@/components/documents';

<DocumentBadge type="base" />      // Blue
<DocumentBadge type="expansion" />  // Purple
<DocumentBadge type="errata" />     // Orange
<DocumentBadge type="homerule" />   // Green
```

### DocumentTypeSelector

Dropdown for selecting document type with descriptions.

```tsx
import { DocumentTypeSelector } from '@/components/documents';
import { useState } from 'react';

function MyComponent() {
  const [type, setType] = useState<DocumentType>('base');

  return (
    <DocumentTypeSelector
      value={type}
      onChange={setType}
      disabled={false}
    />
  );
}
```

### FileUploadList

Multi-file upload list with drag-drop reordering and validation.

```tsx
import { FileUploadList, type FileUploadItem } from '@/components/documents';
import { useState } from 'react';

function UploadPage() {
  const [files, setFiles] = useState<FileUploadItem[]>([]);

  return (
    <FileUploadList
      files={files}
      onChange={setFiles}
      maxFiles={5}
      maxSizeMB={50}
    />
  );
}
```

Features:
- Accept multiple PDFs (configurable max count and size)
- Drag and drop to reorder files
- Per-file document type selection
- Automatic validation (file type, size)
- Visual error display

### CollectionSourceFilter

Multi-select filter for choosing which document collections to query.

```tsx
import { CollectionSourceFilter, type DocumentCollection } from '@/components/documents';
import { useState } from 'react';

function ChatPage() {
  const collections: DocumentCollection[] = [
    {
      id: 'coll-1',
      name: 'Catan Base Rules',
      documentType: 'base',
      documentCount: 3,
    },
    {
      id: 'coll-2',
      name: 'Seafarers Expansion',
      documentType: 'expansion',
      documentCount: 2,
    },
  ];

  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  return (
    <CollectionSourceFilter
      collections={collections}
      selectedDocIds={selectedIds}
      onChange={setSelectedIds}
    />
  );
}
```

Features:
- "All documents" default when none or all selected
- Show document type badges
- Display document count per collection
- Checkbox-based multi-select
- Shows selected count in button label

## Document Types

```ts
type DocumentType = 'base' | 'expansion' | 'errata' | 'homerule';
```

- **base**: Core rulebook (blue)
- **expansion**: Expansion rules (purple)
- **errata**: Official corrections (orange)
- **homerule**: Custom variants (green)

## API Integration

These components expect the backend DocumentCollection API (Issue #2051) to be available:

- `POST /api/v1/games/{gameId}/document-collections` - Create collection
- `GET /api/v1/games/{gameId}/document-collections` - List collections
- `POST /api/v1/games/{gameId}/document-collections/{collectionId}/documents` - Add document
- `PUT /api/v1/games/{gameId}/document-collections/{collectionId}/documents/{documentId}/reorder` - Reorder

## Accessibility

All components follow WCAG 2.1 AA standards:
- Proper ARIA labels and roles
- Keyboard navigation support
- Color contrast compliance
- Screen reader friendly
- Focus management

## Testing

Components use Shadcn/ui patterns and should be tested with:
- Unit tests (Vitest + React Testing Library)
- Visual regression (Chromatic/Storybook)
- Accessibility tests (axe-core)

Example test:

```tsx
import { render, screen } from '@testing-library/react';
import { DocumentBadge } from '@/components/documents';

test('renders base document badge', () => {
  render(<DocumentBadge type="base" />);
  expect(screen.getByText('Base Rules')).toBeInTheDocument();
});
```

## File Paths

```
apps/web/src/components/documents/
├── DocumentBadge.tsx
├── DocumentTypeSelector.tsx
├── FileUploadList.tsx
├── CollectionSourceFilter.tsx
├── index.ts
└── README.md
```
