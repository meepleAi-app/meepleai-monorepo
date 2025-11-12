# Frontend Architecture

**Status**: Production
**Version**: 1.0
**Owner**: Frontend Team
**Last Updated**: 2025-01-15

---

## Architecture Overview

MeepleAI frontend follows a **component-based architecture** with Next.js Pages Router, emphasizing modularity, type safety, and progressive enhancement.

### Core Principles

1. **Component Isolation**: Each component is self-contained with clear dependencies
2. **Type Safety First**: TypeScript strict mode, no `any` types
3. **Progressive Enhancement**: Server-rendered where possible, client-enhanced
4. **API-Backend Alignment**: Frontend structure mirrors backend's 7 bounded contexts

---

## System Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     User Browser                            │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐    │
│  │   Next.js    │  │    React     │  │  Shadcn/UI   │    │
│  │   (SSR/SSG)  │  │  Components  │  │  (Radix)     │    │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘    │
│         │                  │                  │             │
│         └──────────────────┴──────────────────┘             │
│                            │                                │
└────────────────────────────┼────────────────────────────────┘
                             │ HTTP/WebSocket
                             │
┌────────────────────────────┼────────────────────────────────┐
│                    API Gateway (8080)                       │
│                            │                                │
│  ┌─────────────────────────┴──────────────────────────┐   │
│  │          7 Bounded Contexts (CQRS/MediatR)         │   │
│  │  Authentication │ GameManagement │ KnowledgeBase   │   │
│  │  DocumentProcessing │ WorkflowIntegration │ etc.   │   │
│  └─────────────────────────┬──────────────────────────┘   │
│                            │                                │
└────────────────────────────┼────────────────────────────────┘
                             │
           ┌─────────────────┼─────────────────┐
           │                 │                 │
    ┌──────▼──────┐  ┌──────▼──────┐  ┌──────▼──────┐
    │  PostgreSQL │  │   Qdrant    │  │    Redis    │
    │  (Primary)  │  │  (Vectors)  │  │  (Cache)    │
    └─────────────┘  └─────────────┘  └─────────────┘
```

---

## Component Architecture

### Component Hierarchy

```
components/
├── ui/                    # Design System (Shadcn/UI primitives)
│   ├── button.tsx         # Base button with variants
│   ├── input.tsx          # Form input primitive
│   ├── dialog.tsx         # Modal dialogs
│   └── ...                # 16 Shadcn components
│
├── accessible/            # WCAG 2.1 AA wrappers
│   ├── AccessibleButton.tsx
│   ├── AccessibleModal.tsx
│   └── AccessibleFormInput.tsx
│
├── chat/                  # RAG Chat (KnowledgeBase context)
│   ├── ChatProvider.tsx   # State management
│   ├── MessageList.tsx    # Message rendering
│   ├── MessageInput.tsx   # User input
│   └── CitationDisplay.tsx # Page number + snippet
│
├── admin/                 # Admin Dashboard (Administration context)
│   ├── AdminCharts.tsx    # Analytics visualizations
│   ├── UserManagement.tsx # User CRUD
│   └── ConfigEditor.tsx   # System configuration
│
├── auth/                  # Authentication (Authentication context)
│   ├── LoginForm.tsx      # Cookie auth
│   ├── OAuth Buttons.tsx   # Google/Discord/GitHub
│   └── TwoFactorSetup.tsx # TOTP configuration
│
├── upload/                # PDF Upload (DocumentProcessing context)
│   ├── MultiFileUpload.tsx
│   ├── ProcessingProgress.tsx
│   └── GameMatcher.tsx
│
├── editor/                # Rich Text Editor
│   ├── RichTextEditor.tsx # TipTap editor
│   └── EditorToolbar.tsx  # Formatting controls
│
├── diff/                  # Diff Viewer
│   ├── DiffViewerEnhanced.tsx
│   └── SideBySideDiffView.tsx
│
├── timeline/              # Event Timeline
│   └── Timeline.tsx
│
└── loading/               # Loading States
    ├── Spinner.tsx
    ├── SkeletonLoader.tsx
    └── TypingIndicator.tsx
```

### Component Design Patterns

**1. Composition over Inheritance**
```typescript
// Base UI primitive (Shadcn)
import { Button } from '@/components/ui/button';

// Accessible wrapper
export function AccessibleButton({ children, ...props }) {
  return (
    <Button
      {...props}
      aria-label={props['aria-label'] || children}
    >
      {children}
    </Button>
  );
}

// Feature-specific component
export function ChatSendButton({ disabled, loading }) {
  const { t } = useTranslation('chat');
  return (
    <AccessibleButton
      disabled={disabled || loading}
      aria-busy={loading}
    >
      {loading ? t('sending') : t('send')}
    </AccessibleButton>
  );
}
```

**2. Container/Presentational Split**
```typescript
// Container (logic)
export function ChatContainer() {
  const { messages, sendMessage, loading } = useChatLogic();
  return (
    <ChatPresentation
      messages={messages}
      onSendMessage={sendMessage}
      loading={loading}
    />
  );
}

// Presentation (UI)
export function ChatPresentation({ messages, onSendMessage, loading }) {
  return (
    <div>
      <MessageList messages={messages} />
      <MessageInput onSend={onSendMessage} disabled={loading} />
    </div>
  );
}
```

---

## State Management Architecture

### State Tiers

```
┌─────────────────────────────────────────────────────┐
│  Tier 1: URL State (Next.js Router)                │
│  - Locale (it/en)                                   │
│  - Current page/route                               │
│  - Query parameters (search, filters)               │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│  Tier 2: Server State (Future: React Query)        │
│  - API responses (games, users, config)             │
│  - Caching & invalidation                           │
│  - Optimistic updates                               │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│  Tier 3: Component State (React Context)           │
│  - ChatProvider (chat messages, session)            │
│  - ThemeProvider (dark/light mode)                  │
│  - Feature-scoped state (upload progress, etc.)     │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│  Tier 4: Form State (React Hook Form)              │
│  - Login credentials                                 │
│  - Upload metadata                                   │
│  - Admin configuration edits                         │
└─────────────────┬───────────────────────────────────┘
                  │
┌─────────────────▼───────────────────────────────────┐
│  Tier 5: Local Component State (useState)          │
│  - Toggle states (modal open/close)                 │
│  - Temporary UI state (hover, focus)                │
│  - Input field values (uncontrolled)                │
└─────────────────────────────────────────────────────┘
```

### State Management Rules

1. **Server State → React Query** (when implemented):
   - All API responses
   - Automatic caching & invalidation
   - Background refetching

2. **Feature State → React Context**:
   - Scoped to feature (ChatProvider for chat only)
   - Avoid global contexts (performance)
   - Use reducer for complex state

3. **Form State → React Hook Form**:
   - Validation logic
   - Error handling
   - Submission state

4. **UI State → Local useState**:
   - Temporary, non-shareable
   - Component lifecycle only

---

## API Client Architecture

### Backend Integration Pattern

The frontend API client mirrors the backend's **7 Bounded Contexts** with corresponding error handling and state management:

```typescript
// lib/api.ts - Centralized API client

export class ApiClient {
  private baseUrl = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  // Authentication Context
  async login(credentials: LoginRequest): Promise<LoginResponse> {
    return this.post('/api/v1/auth/login', credentials);
  }

  async logout(): Promise<void> {
    return this.post('/api/v1/auth/logout');
  }

  // GameManagement Context
  async getGames(): Promise<Game[]> {
    return this.get('/api/v1/games');
  }

  async getGame(id: string): Promise<Game> {
    return this.get(`/api/v1/games/${id}`);
  }

  // KnowledgeBase Context (RAG)
  async askQuestion(question: AskQuestionRequest): Promise<ReadableStream> {
    return this.postStream('/api/v1/chat', question);
  }

  async searchRules(query: SearchQuery): Promise<SearchResults> {
    return this.post('/api/v1/search', query);
  }

  // DocumentProcessing Context
  async uploadPdf(file: File, gameId: string): Promise<UploadResponse> {
    const formData = new FormData();
    formData.append('file', file);
    formData.append('gameId', gameId);
    return this.postFormData('/api/v1/documents/upload', formData);
  }

  // Administration Context
  async getUsers(): Promise<User[]> {
    return this.get('/api/v1/admin/users');
  }

  // SystemConfiguration Context
  async getConfig(): Promise<SystemConfig> {
    return this.get('/api/v1/admin/configuration');
  }

  // WorkflowIntegration Context
  async triggerWorkflow(workflow: WorkflowTrigger): Promise<WorkflowExecution> {
    return this.post('/api/v1/workflows/trigger', workflow);
  }

  // Private HTTP methods with error handling
  private async get<T>(path: string): Promise<T> {
    return this.request('GET', path);
  }

  private async post<T>(path: string, body?: any): Promise<T> {
    return this.request('POST', path, body);
  }

  private async postStream(path: string, body: any): Promise<ReadableStream> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
        ...this.getAuthHeaders(),
      },
      credentials: 'include',  // Cookie auth
      body: JSON.stringify(body),
    });

    if (!response.ok) throw new ApiError(response);
    return response.body!;
  }

  private async request<T>(method: string, path: string, body?: any): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method,
      headers: {
        'Content-Type': 'application/json',
        ...this.getAuthHeaders(),
        ...this.getCorrelationId(),  // Distributed tracing
      },
      credentials: 'include',  // Cookie auth
      body: body ? JSON.stringify(body) : undefined,
    });

    if (!response.ok) {
      throw await this.handleError(response);
    }

    return response.json();
  }

  private getAuthHeaders() {
    // API Key authentication (optional)
    const apiKey = localStorage.getItem('api_key');
    return apiKey ? { 'X-API-Key': apiKey } : {};
  }

  private getCorrelationId() {
    // Generate or retrieve correlation ID for distributed tracing
    let correlationId = sessionStorage.getItem('correlation_id');
    if (!correlationId) {
      correlationId = crypto.randomUUID();
      sessionStorage.setItem('correlation_id', correlationId);
    }
    return { 'X-Correlation-ID': correlationId };
  }

  private async handleError(response: Response): Promise<ApiError> {
    const errorBody = await response.json().catch(() => ({}));

    // Map backend error types to frontend error classes
    switch (response.status) {
      case 401:
        return new UnauthorizedError(errorBody);
      case 403:
        return new ForbiddenError(errorBody);
      case 404:
        return new NotFoundError(errorBody);
      case 422:
        return new ValidationError(errorBody);
      case 429:
        return new RateLimitError(errorBody);
      case 500:
      case 502:
      case 503:
        return new ServerError(errorBody);
      default:
        return new ApiError(errorBody);
    }
  }
}

export const apiClient = new ApiClient();
```

### Error Boundary per Bounded Context

```typescript
// components/ErrorBoundary.tsx

export function ApiErrorBoundary({ context, children }) {
  return (
    <ErrorBoundary
      fallbackRender={({ error }) => (
        <ErrorDisplay
          error={error}
          context={context}
          retryAction={error.retryable ? () => window.location.reload() : undefined}
        />
      )}
      onError={(error, errorInfo) => {
        // Send to observability platform
        logError({
          error,
          errorInfo,
          context,
          correlationId: sessionStorage.getItem('correlation_id'),
        });
      }}
    >
      {children}
    </ErrorBoundary>
  );
}

// Usage per context
<ApiErrorBoundary context="KnowledgeBase">
  <ChatInterface />
</ApiErrorBoundary>
```

---

## Routing Architecture

### Page Structure

```
pages/
├── index.tsx              # Homepage (public, SSG)
├── chat.tsx               # RAG Chat (authenticated, CSR)
├── upload.tsx             # PDF Upload (authenticated, CSR)
├── admin/
│   ├── index.tsx          # Dashboard (SSR)
│   ├── users.tsx          # User management (SSR)
│   ├── analytics.tsx      # Analytics (SSR)
│   ├── configuration.tsx  # System config (SSR)
│   └── prompts/
│       ├── index.tsx      # Prompt list (SSR)
│       ├── [id].tsx       # Prompt detail (SSR)
│       └── [id]/
│           ├── versions/
│           │   ├── [versionId].tsx  # Version detail
│           │   └── new.tsx          # Create version
│           ├── audit.tsx            # Audit trail
│           └── compare.tsx          # Version comparison
├── auth/
│   ├── login.tsx          # Login (public, SSG)
│   ├── callback.tsx       # OAuth callback (CSR)
│   └── reset-password.tsx # Password reset (SSG)
├── profile.tsx            # User profile (authenticated, SSR)
├── settings.tsx           # User settings (authenticated, SSR)
└── _app.tsx               # App wrapper (i18n, providers)
```

### Rendering Strategies

**Static Generation (SSG)**:
- Homepage, marketing pages
- Login, reset password
- Public documentation

**Server-Side Rendering (SSR)**:
- Admin dashboard (requires auth check)
- User-specific pages (profile, settings)
- SEO-critical authenticated pages

**Client-Side Rendering (CSR)**:
- Real-time chat (WebSocket)
- PDF upload (file handling)
- Interactive editors

---

## Performance Architecture

### Code Splitting Strategy

```typescript
// Automatic route-based splitting (Next.js default)
// Each page/ file is a separate chunk

// Manual component splitting for heavy dependencies
const MonacoEditor = dynamic(() => import('@monaco-editor/react'), {
  ssr: false,
  loading: () => <EditorSkeleton />,
});

const ChartComponents = dynamic(() => import('../components/AdminCharts'), {
  ssr: false,
});

const PdfViewer = dynamic(() => import('react-pdf'), {
  ssr: false,
});
```

### Asset Loading Priorities

```typescript
// Critical CSS: Inline in <head>
// Tailwind base + Shadcn variables

// Fonts: Preload (next/font optimization)
import { Inter } from 'next/font/google';

// Images: Lazy load below fold
<Image
  src="/hero.jpg"
  priority={false}  // Lazy load
  loading="lazy"
/>

// Third-party scripts: Defer
<Script
  src="https://analytics.example.com/script.js"
  strategy="afterInteractive"
/>
```

---

## Security Architecture

### Client-Side Security Measures

1. **XSS Prevention**:
   - DOMPurify for user-generated content
   - React's built-in escaping
   - Content Security Policy headers

2. **CSRF Protection**:
   - Cookie-based auth with SameSite=Strict
   - CSRF tokens for state-changing operations

3. **Authentication**:
   - Dual auth support (cookie + API key)
   - OAuth integration (Google, Discord, GitHub)
   - 2FA support (TOTP)

4. **Data Validation**:
   - Zod schemas for all forms
   - Server-side validation (never trust client)
   - Type safety via TypeScript

---

## Testing Architecture

```
Testing Pyramid:

         /\        E2E Tests (Playwright)
        /  \       - User journeys
       /    \      - Visual regression
      /------\     - Accessibility
     /        \    Integration Tests (Jest + RTL)
    /          \   - Page-level flows
   /            \  - Component interactions
  /--------------\ Unit Tests (Jest + RTL)
 /                \- Component behavior
/                  \- Utility functions
--------------------
```

See [Testing Strategy](./testing-strategy.md) for details.

---

## Deployment Architecture

### Build Process

```bash
# Production build
pnpm build

# Output:
# .next/static/chunks/*.js     # Code-split bundles
# .next/static/css/*.css        # Optimized CSS
# .next/server/pages/*.html     # Pre-rendered pages
# .next/cache/                  # Build cache
```

### Deployment Targets

**Option 1: Vercel** (Recommended):
- Zero-config Next.js deployment
- Automatic CDN distribution
- Edge functions for API routes
- Built-in analytics

**Option 2: Docker + Kubernetes**:
- Self-hosted control
- Multi-cloud portability
- Custom scaling rules

See [Frontend Deployment](../deployment/frontend-deployment.md) for details.

---

## References

- [Next.js Documentation](https://nextjs.org/docs)
- [React 19 Documentation](https://react.dev)
- [Shadcn/UI](https://ui.shadcn.com)
- [TypeScript Handbook](https://www.typescriptlang.org/docs/handbook/intro.html)

---

**Maintained by**: Frontend Team
**Review Frequency**: Quarterly or on major architecture changes
**Last Review**: 2025-01-15
