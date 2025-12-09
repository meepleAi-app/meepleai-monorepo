# Guida Sviluppatore Frontend - MeepleAI

## Panoramica Generale

Il frontend di MeepleAI è un'applicazione moderna costruita con **Next.js 16**, **React 19** e **TypeScript** in modalità strict. L'applicazione è composta da **33 route** con **325 file TypeScript**, raggiunge una **copertura di test del 90%+** (4.033 test) e segue un approccio component-driven development con integrazione **Storybook** e **Chromatic** per visual regression testing.

**Stack Tecnologico:**
- Next.js 16 (App Router)
- React 19
- TypeScript (strict mode)
- Tailwind CSS 4
- Shadcn/UI (Radix UI + Tailwind)
- TanStack Query v5 (server state)
- Zustand (client state)
- react-intl (i18n)
- Storybook 10 + Chromatic

---

## Architettura Applicazione

### Struttura Directory

```
apps/web/
├── src/
│   ├── app/                    # Next.js App Router (33 route)
│   │   ├── layout.tsx         # Root layout + AppProviders
│   │   ├── page.tsx           # Homepage
│   │   ├── chat/              # Chat interface
│   │   ├── games/             # Catalogo giochi
│   │   ├── settings/          # Impostazioni utente (4 tab)
│   │   ├── admin/             # Pannello amministrazione
│   │   └── ...
│   ├── components/            # Componenti React (29 directory)
│   │   ├── ui/               # Shadcn/UI components (22)
│   │   ├── auth/             # AuthProvider, LoginForm
│   │   ├── chat/             # ChatInterface, MessageList
│   │   ├── layout/           # Header, Footer, Navigation
│   │   └── ...
│   ├── lib/                   # Librerie e utilities
│   │   ├── api/              # API SDK modulare
│   │   │   ├── core/         # HttpClient base
│   │   │   ├── clients/      # Feature clients (8)
│   │   │   ├── schemas/      # Zod validation
│   │   │   └── types/        # TypeScript types
│   │   ├── store/            # Zustand store
│   │   └── schemas/          # Form validation schemas
│   ├── hooks/                 # Custom hooks (20+)
│   │   ├── queries/          # TanStack Query hooks
│   │   └── ...
│   ├── locales/              # i18n translations (it, en)
│   └── styles/               # Global CSS
├── public/                    # Asset statici
├── .storybook/               # Storybook config
└── playwright/               # E2E tests
```

---

## 1. Next.js App Router (33 Route)

### Organizzazione Route

#### **Route Core (5)**
```
/                    # Homepage (landing page)
/chat                # Interfaccia chat principale
/login               # Autenticazione
/settings            # Impostazioni utente (4 tab)
/setup               # Wizard configurazione iniziale
```

#### **Gestione Giochi (3)**
```
/games               # Catalogo giochi (filtri + paginazione)
/games/[id]          # Dettaglio singolo gioco
/board-game-ai       # Interfaccia AI legacy
```

#### **Gestione Sessioni (3)**
```
/sessions            # Lista sessioni
/sessions/[id]       # Dettagli sessione
/sessions/history    # Storico sessioni
```

#### **Route Admin (13)**
```
/admin                              # Dashboard amministrazione
/admin/analytics                    # Metriche sistema
/admin/users                        # Gestione utenti
/admin/configuration                # Configurazione dinamica
/admin/cache                        # Gestione cache
/admin/bulk-export                  # Esportazione bulk
/admin/n8n-templates                # Template workflow
/admin/prompts                      # Gestione prompt
/admin/prompts/[id]                 # Dettaglio prompt
/admin/prompts/[id]/versions        # Storico versioni
/admin/prompts/[id]/versions/[versionId]  # Versione specifica
/admin/prompts/[id]/versions/new    # Nuova versione
/admin/prompts/[id]/audit           # Audit log
/admin/prompts/[id]/compare         # Confronto versioni
```

#### **Route Utility (9)**
```
/upload              # Upload PDF
/editor              # Editor RuleSpec
/chess               # Demo/testing scacchi
/n8n                 # Integrazione workflow
/auth/callback       # OAuth callback
/reset-password      # Reset password
/versions            # Informazioni versione
/shadcn-demo         # Showcase componenti UI
```

### Pattern Server/Client Components

```tsx
// app/chat/page.tsx (Server Component - wrapper)
import { Metadata } from 'next';
import ChatPageClient from './ChatPageClient';

export const metadata: Metadata = {
  title: 'Chat - MeepleAI',
  description: 'Chatta con l\'assistente AI per le regole dei giochi da tavolo'
};

export default function ChatPage() {
  return <ChatPageClient />;
}

// app/chat/ChatPageClient.tsx (Client Component)
'use client';

import { useState } from 'react';
import { ChatInterface } from '@/components/chat/ChatInterface';

export default function ChatPageClient() {
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);

  return (
    <div className="container mx-auto p-4">
      <ChatInterface gameId={selectedGameId} />
    </div>
  );
}
```

**Strategia:**
- **Server Components**: Wrapper delle route per metadata e SEO
- **Client Components**: Logica interattiva (marcati con `'use client'`)
- **Benefici**: Riduzione bundle size, migliore performance iniziale

---

## 2. Componenti React

### Organizzazione Componenti (29 Directory)

```
components/
├── accessible/        # Conformità WCAG 2.1 AA
│   └── AccessibleSkipLink.tsx
├── admin/            # Componenti specifici admin
│   ├── AdminStats.tsx
│   └── UserManagementTable.tsx
├── auth/             # Autenticazione
│   ├── AuthProvider.tsx          # Context provider
│   ├── LoginForm.tsx
│   └── RegisterForm.tsx
├── chat/             # Interfaccia chat
│   ├── ChatInterface.tsx         # Container principale
│   ├── MessageList.tsx
│   ├── ChatInput.tsx
│   └── ChatSidebar.tsx
├── citations/        # Visualizzazione citazioni RAG
│   └── CitationCard.tsx
├── comments/         # Sistema commenti RuleSpec
│   └── CommentThread.tsx
├── diff/             # Viewer differenze versioni
│   └── DiffViewer.tsx
├── editor/           # Editor regole
│   └── RuleSpecEditor.tsx
├── errors/           # Gestione errori
│   ├── ErrorBoundary.tsx
│   └── RouteErrorBoundary.tsx
├── forms/            # Componenti form con validazione
│   └── ValidatedInput.tsx
├── game/             # Componenti specifici giochi
│   └── GameCard.tsx
├── games/            # Catalogo giochi
│   ├── GamesList.tsx
│   ├── GamesFilters.tsx
│   └── GamesPagination.tsx
├── landing/          # Homepage
│   ├── Hero.tsx
│   └── Features.tsx
├── layout/           # Layout applicazione
│   ├── Header.tsx
│   ├── Footer.tsx
│   ├── Navigation.tsx
│   └── KeyboardShortcutsHelp.tsx
├── loading/          # Stati di caricamento
│   ├── LoadingButton.tsx
│   ├── LoadingSpinner.tsx
│   └── Skeleton.tsx
├── modals/           # Modali
│   └── SessionWarningModal.tsx
├── pages/            # Componenti page-level
│   ├── HomePage.tsx
│   └── ChatPage.tsx
├── pdf/              # Gestione PDF
│   ├── PdfViewer.tsx
│   └── PdfUploadProgress.tsx
├── progress/         # Indicatori progresso
│   └── ProgressBar.tsx
├── prompt/           # Gestione prompt
│   └── PromptEditor.tsx
├── providers/        # Provider React Context
│   └── IntlProvider.tsx
├── search/           # Componenti ricerca
│   └── SearchBar.tsx
├── timeline/         # Timeline attività
│   └── ActivityTimeline.tsx
├── ui/               # Shadcn/UI (22 componenti)
│   ├── button.tsx
│   ├── input.tsx
│   ├── card.tsx
│   └── ...
├── upload/           # Upload file
│   └── UploadQueue.tsx
├── versioning/       # Controllo versione
│   └── VersionHistory.tsx
└── wizard/           # Wizard multi-step
    └── SetupWizard.tsx
```

### Componente Chiave: ChatInterface

```tsx
// components/chat/ChatInterface.tsx
'use client';

import { useState, useEffect } from 'react';
import { useChatStore } from '@/lib/store/chat/hooks';
import { useCreateChat, useAddMessage } from '@/hooks/queries/chat';
import { MessageList } from './MessageList';
import { ChatInput } from './ChatInput';
import { ChatSidebar } from './ChatSidebar';

interface ChatInterfaceProps {
  gameId: string | null;
}

export function ChatInterface({ gameId }: ChatInterfaceProps) {
  const { activeChatId, messages, addOptimisticMessage } = useChatStore();
  const createChatMutation = useCreateChat();
  const addMessageMutation = useAddMessage();

  const handleSendMessage = async (content: string) => {
    if (!gameId) return;

    // 1. Crea chat se necessario
    let chatId = activeChatId;
    if (!chatId) {
      const newChat = await createChatMutation.mutateAsync({
        gameId,
        title: content.slice(0, 50)
      });
      chatId = newChat.id;
    }

    // 2. Aggiunta ottimistica messaggio utente
    const tempId = crypto.randomUUID();
    addOptimisticMessage({
      id: tempId,
      chatId,
      role: 'user',
      content,
      createdAt: new Date().toISOString()
    });

    // 3. Invio messaggio con streaming
    try {
      await addMessageMutation.mutateAsync({
        chatId,
        content,
        role: 'user'
      });
    } catch (error) {
      // Rollback ottimistico in caso di errore
      removeOptimisticMessage(tempId);
      throw error;
    }
  };

  return (
    <div className="flex h-screen">
      {/* Sidebar per selezione giochi */}
      <ChatSidebar />

      {/* Area chat principale */}
      <div className="flex-1 flex flex-col">
        {/* Lista messaggi con scrolling automatico */}
        <MessageList messages={messages} />

        {/* Input utente */}
        <ChatInput
          onSend={handleSendMessage}
          disabled={!gameId || addMessageMutation.isPending}
        />
      </div>
    </div>
  );
}
```

### Componente: Settings Page (4 Tab)

```tsx
// app/settings/page.tsx
'use client';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { ProfileTab } from './ProfileTab';
import { PreferencesTab } from './PreferencesTab';
import { PrivacyTab } from './PrivacyTab';
import { AdvancedTab } from './AdvancedTab';

export default function SettingsPage() {
  return (
    <div className="container mx-auto p-6">
      <h1 className="text-3xl font-bold mb-6">Impostazioni</h1>

      <Tabs defaultValue="profile" className="w-full">
        <TabsList className="grid w-full grid-cols-4">
          <TabsTrigger value="profile">Profilo</TabsTrigger>
          <TabsTrigger value="preferences">Preferenze</TabsTrigger>
          <TabsTrigger value="privacy">Privacy</TabsTrigger>
          <TabsTrigger value="advanced">Avanzate</TabsTrigger>
        </TabsList>

        <TabsContent value="profile">
          <ProfileTab />
        </TabsContent>

        <TabsContent value="preferences">
          <PreferencesTab />
        </TabsContent>

        <TabsContent value="privacy">
          <PrivacyTab />
        </TabsContent>

        <TabsContent value="advanced">
          <AdvancedTab />
        </TabsContent>
      </Tabs>
    </div>
  );
}

// app/settings/PrivacyTab.tsx
'use client';

import { useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { api } from '@/lib/api';
import { useCurrentUser } from '@/hooks/queries/user';
import QRCode from 'qrcode.react';

export function PrivacyTab() {
  const { data: user } = useCurrentUser();
  const [setupData, setSetupData] = useState<{
    qrCodeUri: string;
    secret: string;
    backupCodes: string[];
  } | null>(null);

  const handle2FASetup = async () => {
    const data = await api.auth.setup2FA();
    setSetupData(data);
  };

  const handleEnable2FA = async (code: string) => {
    await api.auth.enable2FA(code);
    // Refresh user data
  };

  const handleDisable2FA = async (password: string, code: string) => {
    await api.auth.disable2FA(password, code);
    // Refresh user data
  };

  const downloadBackupCodes = () => {
    const blob = new Blob([setupData!.backupCodes.join('\n')], {
      type: 'text/plain'
    });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'meepleai-backup-codes.txt';
    a.click();
  };

  return (
    <div className="space-y-6">
      {/* Autenticazione a Due Fattori */}
      <Card>
        <CardHeader>
          <CardTitle>Autenticazione a Due Fattori (2FA)</CardTitle>
          <CardDescription>
            Aggiungi un ulteriore livello di sicurezza al tuo account
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="flex items-center justify-between">
            <div>
              <p className="font-medium">Stato 2FA</p>
              <p className="text-sm text-muted-foreground">
                {user?.isTwoFactorEnabled ? 'Attiva' : 'Disattivata'}
              </p>
            </div>
            <Switch
              checked={user?.isTwoFactorEnabled}
              onCheckedChange={(enabled) => {
                if (enabled) {
                  handle2FASetup();
                } else {
                  // Mostra dialog di conferma
                }
              }}
            />
          </div>

          {/* Setup 2FA - Mostra QR Code */}
          {setupData && !user?.isTwoFactorEnabled && (
            <div className="mt-6 space-y-4">
              <div className="flex justify-center">
                <QRCode value={setupData.qrCodeUri} size={200} />
              </div>

              <div>
                <Label>Codice segreto (manuale)</Label>
                <code className="block mt-2 p-2 bg-muted rounded">
                  {setupData.secret}
                </code>
              </div>

              <div>
                <Label>Codici di backup</Label>
                <Button
                  variant="outline"
                  onClick={downloadBackupCodes}
                  className="mt-2"
                >
                  Scarica codici di backup
                </Button>
              </div>

              <div>
                <Label>Verifica codice TOTP</Label>
                <Input
                  type="text"
                  placeholder="123456"
                  maxLength={6}
                  onChange={(e) => {
                    if (e.target.value.length === 6) {
                      handleEnable2FA(e.target.value);
                    }
                  }}
                />
              </div>
            </div>
          )}
        </CardContent>
      </Card>

      {/* Account OAuth Collegati */}
      <Card>
        <CardHeader>
          <CardTitle>Account Collegati</CardTitle>
          <CardDescription>
            Collega account OAuth per login rapido
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div className="space-y-4">
            {/* Google */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <img src="/icons/google.svg" alt="Google" className="w-6 h-6" />
                <span>Google</span>
              </div>
              <Button variant="outline">Collega</Button>
            </div>

            {/* Discord */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <img src="/icons/discord.svg" alt="Discord" className="w-6 h-6" />
                <span>Discord</span>
              </div>
              <Button variant="outline">Collega</Button>
            </div>

            {/* GitHub */}
            <div className="flex items-center justify-between">
              <div className="flex items-center gap-3">
                <img src="/icons/github.svg" alt="GitHub" className="w-6 h-6" />
                <span>GitHub</span>
              </div>
              <Button variant="outline">Collega</Button>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
```

### Componente: UploadQueue

```tsx
// components/upload/UploadQueue.tsx
'use client';

import { useState, useCallback } from 'react';
import { useUploadQueue } from '@/hooks/upload/useUploadQueue';
import { Progress } from '@/components/ui/progress';
import { Button } from '@/components/ui/button';

export function UploadQueue() {
  const { queue, addFiles, cancelUpload, retryUpload, getStats } = useUploadQueue({
    onUploadComplete: (fileId, result) => {
      console.log('Upload completato:', fileId, result);
    },
    onAllComplete: () => {
      console.log('Tutti gli upload completati');
    },
    onError: (fileId, error) => {
      console.error('Errore upload:', fileId, error);
    }
  });

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    const files = Array.from(e.dataTransfer.files);
    addFiles(files);
  }, [addFiles]);

  const stats = getStats();

  return (
    <div
      onDrop={handleDrop}
      onDragOver={(e) => e.preventDefault()}
      className="border-2 border-dashed rounded-lg p-6"
    >
      <h3 className="text-lg font-semibold mb-4">Upload Queue</h3>

      {/* Statistiche */}
      <div className="grid grid-cols-4 gap-4 mb-6">
        <div>
          <p className="text-sm text-muted-foreground">Totali</p>
          <p className="text-2xl font-bold">{stats.total}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">In coda</p>
          <p className="text-2xl font-bold">{stats.pending}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">In caricamento</p>
          <p className="text-2xl font-bold">{stats.uploading}</p>
        </div>
        <div>
          <p className="text-sm text-muted-foreground">Completati</p>
          <p className="text-2xl font-bold">{stats.completed}</p>
        </div>
      </div>

      {/* Lista file in coda */}
      <div className="space-y-3">
        {queue.map((item) => (
          <div key={item.id} className="flex items-center gap-4">
            <div className="flex-1">
              <p className="font-medium">{item.file.name}</p>
              <p className="text-sm text-muted-foreground">
                {(item.file.size / 1024 / 1024).toFixed(2)} MB
              </p>
            </div>

            {/* Progress bar */}
            <div className="flex-1">
              <Progress value={item.progress} />
              <p className="text-xs text-muted-foreground mt-1">
                {item.status === 'uploading' && `${item.progress}%`}
                {item.status === 'completed' && 'Completato'}
                {item.status === 'failed' && 'Errore'}
                {item.status === 'pending' && 'In attesa'}
              </p>
            </div>

            {/* Azioni */}
            <div className="flex gap-2">
              {item.status === 'uploading' && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => cancelUpload(item.id)}
                >
                  Annulla
                </Button>
              )}
              {item.status === 'failed' && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => retryUpload(item.id)}
                >
                  Riprova
                </Button>
              )}
            </div>
          </div>
        ))}
      </div>

      {/* Area drop */}
      {queue.length === 0 && (
        <div className="text-center py-12">
          <p className="text-muted-foreground">
            Trascina i file qui o clicca per selezionare
          </p>
        </div>
      )}
    </div>
  );
}
```

---

## 3. Gestione dello Stato

### Architettura a 3 Livelli

```
┌─────────────────────────────────────────┐
│   React Context (Auth, Intl, Theme)     │  Layer 3: Provider globali
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│   Zustand (Client State)                │  Layer 2: Stato UI/locale
│   - Chat UI state                       │
│   - Selezioni utente                    │
│   - Sidebar collapsed                   │
└─────────────────────────────────────────┘
              ↓
┌─────────────────────────────────────────┐
│   TanStack Query (Server State)         │  Layer 1: Dati server
│   - User, Games, Chats                  │
│   - Cache 5min, retry 1x                │
└─────────────────────────────────────────┘
```

### Layer 1: TanStack Query (Server State)

#### **Configurazione**

```tsx
// lib/query/queryClient.ts
import { QueryClient } from '@tanstack/react-query';

// Singleton per client-side
let browserQueryClient: QueryClient | undefined = undefined;

function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 5 * 60 * 1000,  // 5 minuti
        retry: 1,
        refetchOnWindowFocus: true
      }
    }
  });
}

export function getQueryClient() {
  // Server: crea nuovo client per ogni richiesta (previene data leak)
  if (typeof window === 'undefined') {
    return makeQueryClient();
  }

  // Client: singleton riutilizzabile
  if (!browserQueryClient) {
    browserQueryClient = makeQueryClient();
  }
  return browserQueryClient;
}

// components/providers/QueryProvider.tsx
'use client';

import { QueryClientProvider } from '@tanstack/react-query';
import { ReactQueryDevtools } from '@tanstack/react-query-devtools';
import { getQueryClient } from '@/lib/query/queryClient';

export function QueryProvider({ children }: { children: React.ReactNode }) {
  const queryClient = getQueryClient();

  return (
    <QueryClientProvider client={queryClient}>
      {children}
      <ReactQueryDevtools initialIsOpen={false} />
    </QueryClientProvider>
  );
}
```

#### **Query Hooks**

```tsx
// hooks/queries/user.ts
import { useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useCurrentUser() {
  return useQuery({
    queryKey: ['user', 'current'],
    queryFn: async () => {
      const user = await api.auth.getProfile();
      return user;
    },
    staleTime: 5 * 60 * 1000,  // 5 minuti
    retry: false  // Non retry per auth (401 = non autenticato)
  });
}

// hooks/queries/games.ts
export function useGames(filters?: GamesFilters) {
  return useQuery({
    queryKey: ['games', filters],
    queryFn: async () => {
      const games = await api.games.getAll(filters);
      return games;
    }
  });
}

export function useGame(gameId: string) {
  return useQuery({
    queryKey: ['games', gameId],
    queryFn: async () => {
      const game = await api.games.getById(gameId);
      return game;
    },
    enabled: !!gameId  // Solo se gameId presente
  });
}

// hooks/queries/chat.ts
export function useChatThread(chatId: string) {
  return useQuery({
    queryKey: ['chats', chatId],
    queryFn: async () => {
      const thread = await api.chat.getThreadById(chatId);
      return thread;
    },
    refetchInterval: 10000  // Refetch ogni 10s per nuovi messaggi
  });
}
```

#### **Mutation Hooks**

```tsx
// hooks/queries/chat.ts
import { useMutation, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';

export function useCreateChat() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: CreateChatRequest) => {
      return await api.chat.createThread(request);
    },
    onSuccess: (newChat) => {
      // Invalida cache delle chat per refetch
      queryClient.invalidateQueries({ queryKey: ['chats'] });

      // Aggiungi immediatamente alla cache
      queryClient.setQueryData(['chats', newChat.id], newChat);
    }
  });
}

export function useAddMessage() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async ({ chatId, content }: AddMessageRequest) => {
      return await api.chat.addMessage(chatId, { content, role: 'user' });
    },
    onMutate: async ({ chatId, content }) => {
      // Cancella refetch in corso
      await queryClient.cancelQueries({ queryKey: ['chats', chatId] });

      // Snapshot stato precedente
      const previousThread = queryClient.getQueryData(['chats', chatId]);

      // Aggiornamento ottimistico
      queryClient.setQueryData(['chats', chatId], (old: ChatThread) => ({
        ...old,
        messages: [
          ...old.messages,
          {
            id: crypto.randomUUID(),
            role: 'user',
            content,
            createdAt: new Date().toISOString(),
            isOptimistic: true
          }
        ]
      }));

      return { previousThread };
    },
    onError: (err, variables, context) => {
      // Rollback su errore
      if (context?.previousThread) {
        queryClient.setQueryData(
          ['chats', variables.chatId],
          context.previousThread
        );
      }
    },
    onSettled: (data, error, variables) => {
      // Refetch finale
      queryClient.invalidateQueries({ queryKey: ['chats', variables.chatId] });
    }
  });
}
```

### Layer 2: Zustand (Client State)

#### **Store Architecture**

```tsx
// lib/store/chat/store.ts
import { create } from 'zustand';
import { devtools, persist, subscribeWithSelector } from 'zustand/middleware';
import { immer } from 'zustand/middleware/immer';
import { temporal } from 'zundo';

import { createSessionSlice, SessionSlice } from './slices/sessionSlice';
import { createGameSlice, GameSlice } from './slices/gameSlice';
import { createChatSlice, ChatSlice } from './slices/chatSlice';
import { createMessagesSlice, MessagesSlice } from './slices/messagesSlice';
import { createUiSlice, UiSlice } from './slices/uiSlice';

export type ChatStore = SessionSlice & GameSlice & ChatSlice & MessagesSlice & UiSlice;

export const useChatStore = create<ChatStore>()(
  // Middleware stack (esterno → interno)
  devtools(
    persist(
      temporal(
        subscribeWithSelector(
          immer((set, get, store) => ({
            ...createSessionSlice(set, get, store),
            ...createGameSlice(set, get, store),
            ...createChatSlice(set, get, store),
            ...createMessagesSlice(set, get, store),
            ...createUiSlice(set, get, store)
          }))
        ),
        {
          limit: 20,  // 20 stati per undo/redo
          partialize: (state) => {
            // Solo alcuni campi per temporal
            const { selectedGameId, selectedAgentId, activeChatId } = state;
            return { selectedGameId, selectedAgentId, activeChatId };
          }
        }
      ),
      {
        name: 'meepleai-chat-storage',
        partialize: (state) => ({
          // Persistenza selettiva
          selectedGameId: state.selectedGameId,
          selectedAgentId: state.selectedAgentId,
          sidebarCollapsed: state.sidebarCollapsed,
          chatsByGame: state.chatsByGame,
          activeChatIds: state.activeChatIds,
          messagesByChat: state.messagesByChat
        })
      }
    ),
    { name: 'ChatStore' }
  )
);
```

#### **Slice Example: sessionSlice**

```tsx
// lib/store/chat/slices/sessionSlice.ts
import { StateCreator } from 'zustand';
import { ChatStore } from '../store';

export interface SessionSlice {
  // State
  selectedGameId: string | null;
  selectedAgentId: string | null;

  // Actions
  setSelectedGame: (gameId: string | null) => void;
  setSelectedAgent: (agentId: string | null) => void;
  resetSession: () => void;
}

export const createSessionSlice: StateCreator<
  ChatStore,
  [['zustand/immer', never]],
  [],
  SessionSlice
> = (set) => ({
  selectedGameId: null,
  selectedAgentId: null,

  setSelectedGame: (gameId) => {
    set((state) => {
      state.selectedGameId = gameId;
      // Reset agente quando cambia gioco
      state.selectedAgentId = null;
    });
  },

  setSelectedAgent: (agentId) => {
    set((state) => {
      state.selectedAgentId = agentId;
    });
  },

  resetSession: () => {
    set((state) => {
      state.selectedGameId = null;
      state.selectedAgentId = null;
    });
  }
});
```

#### **Slice Example: messagesSlice**

```tsx
// lib/store/chat/slices/messagesSlice.ts
export interface MessagesSlice {
  // State
  messagesByChat: Record<string, ChatMessage[]>;

  // Actions
  addOptimisticMessage: (message: ChatMessage) => void;
  removeOptimisticMessage: (messageId: string) => void;
  updateMessage: (chatId: string, messageId: string, content: string) => void;
  deleteMessage: (chatId: string, messageId: string) => void;
}

export const createMessagesSlice: StateCreator<
  ChatStore,
  [['zustand/immer', never]],
  [],
  MessagesSlice
> = (set) => ({
  messagesByChat: {},

  addOptimisticMessage: (message) => {
    set((state) => {
      if (!state.messagesByChat[message.chatId]) {
        state.messagesByChat[message.chatId] = [];
      }
      state.messagesByChat[message.chatId].push({
        ...message,
        isOptimistic: true
      });
    });
  },

  removeOptimisticMessage: (messageId) => {
    set((state) => {
      Object.keys(state.messagesByChat).forEach((chatId) => {
        state.messagesByChat[chatId] = state.messagesByChat[chatId].filter(
          (msg) => msg.id !== messageId
        );
      });
    });
  },

  updateMessage: (chatId, messageId, content) => {
    set((state) => {
      const messages = state.messagesByChat[chatId];
      if (!messages) return;

      const messageIndex = messages.findIndex((m) => m.id === messageId);
      if (messageIndex === -1) return;

      // Invalida messaggi successivi dell'assistente
      for (let i = messageIndex + 1; i < messages.length; i++) {
        if (messages[i].role === 'assistant') {
          messages[i].isInvalidated = true;
        }
      }

      messages[messageIndex].content = content;
    });
  }
});
```

#### **Selettori Type-Safe**

```tsx
// lib/store/chat/hooks.ts
import { useShallow } from 'zustand/react/shallow';
import { useChatStore } from './store';

// Hook base
export { useChatStore };

// Selettori tipizzati
export const useSelectedGame = () =>
  useChatStore((state) => state.selectedGameId);

export const useSelectedAgent = () =>
  useChatStore((state) => state.selectedAgentId);

export const useChatMessages = (chatId: string) =>
  useChatStore(
    useShallow((state) => state.messagesByChat[chatId] ?? [])
  );

// Azioni
export const useChatActions = () =>
  useChatStore(
    useShallow((state) => ({
      setSelectedGame: state.setSelectedGame,
      setSelectedAgent: state.setSelectedAgent,
      addOptimisticMessage: state.addOptimisticMessage,
      updateMessage: state.updateMessage
    }))
  );
```

#### **Undo/Redo con Zundo**

```tsx
// components/chat/UndoRedoButtons.tsx
'use client';

import { useChatStore } from '@/lib/store/chat/hooks';
import { useTemporalStore } from 'zundo';
import { Button } from '@/components/ui/button';

export function UndoRedoButtons() {
  const { undo, redo, futureStates, pastStates } = useTemporalStore(useChatStore);

  return (
    <div className="flex gap-2">
      <Button
        variant="outline"
        size="sm"
        disabled={pastStates.length === 0}
        onClick={() => undo()}
      >
        Annulla
      </Button>
      <Button
        variant="outline"
        size="sm"
        disabled={futureStates.length === 0}
        onClick={() => redo()}
      >
        Ripeti
      </Button>
    </div>
  );
}
```

### Layer 3: React Context

#### **AuthProvider**

{% raw %}
```tsx
// components/auth/AuthProvider.tsx
'use client';

import { createContext, useContext, ReactNode } from 'react';
import { useCurrentUser } from '@/hooks/queries/user';
import { useQueryClient } from '@tanstack/react-query';

interface AuthContextValue {
  user: User | null;
  isLoading: boolean;
  isAuthenticated: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | undefined>(undefined);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const { data: user, isLoading } = useCurrentUser();

  const login = async (email: string, password: string) => {
    // Server action per login
    const formData = new FormData();
    formData.append('email', email);
    formData.append('password', password);

    const result = await loginAction({ success: false }, formData);

    if (result.success) {
      // Aggiorna cache immediatamente
      queryClient.setQueryData(['user', 'current'], result.user);

      // Invalida per refetch in background
      await queryClient.invalidateQueries({ queryKey: ['user', 'current'] });
    }
  };

  const logout = async () => {
    await logoutAction();

    // Clear cache
    queryClient.setQueryData(['user', 'current'], null);
    queryClient.clear();
  };

  return (
    <AuthContext.Provider
      value={{
        user: user ?? null,
        isLoading,
        isAuthenticated: !!user,
        login,
        logout
      }}
    >
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within AuthProvider');
  }
  return context;
}
```
{% endraw %}

---

## 4. API SDK Modulare

### Architettura

```
lib/api/
├── index.ts              # Factory + default client
├── core/
│   ├── httpClient.ts     # HTTP base con Zod validation
│   ├── errors.ts         # Typed errors
│   ├── logger.ts         # API error logging
│   └── apiKeyStore.ts    # sessionStorage per API key
├── clients/              # Feature clients (allineati a bounded contexts)
│   ├── authClient.ts     # Authentication
│   ├── gamesClient.ts    # GameManagement
│   ├── chatClient.ts     # KnowledgeBase
│   ├── pdfClient.ts      # DocumentProcessing
│   ├── configClient.ts   # SystemConfiguration
│   ├── sessionsClient.ts # Session management
│   ├── bggClient.ts      # BoardGameGeek integration
│   └── agentsClient.ts   # AI agents
├── schemas/              # Zod validation schemas
│   ├── auth.schemas.ts
│   ├── games.schemas.ts
│   ├── chat.schemas.ts
│   └── ...
└── types/                # TypeScript types
    ├── auth.types.ts
    ├── games.types.ts
    └── ...
```

### HttpClient Core

```tsx
// lib/api/core/httpClient.ts
import { z } from 'zod';
import { getStoredApiKey } from './apiKeyStore';
import { logApiError } from './logger';
import {
  UnauthorizedError,
  ValidationError,
  RateLimitError,
  NetworkError
} from './errors';

export interface HttpClientConfig {
  baseUrl: string;
  fetchImpl?: typeof fetch;
}

export class HttpClient {
  private baseUrl: string;
  private fetchImpl: typeof fetch;

  constructor(config: HttpClientConfig) {
    this.baseUrl = config.baseUrl;
    this.fetchImpl = config.fetchImpl ?? fetch;
  }

  private getHeaders(): Record<string, string> {
    const headers: Record<string, string> = {
      'Content-Type': 'application/json',
      'X-Correlation-ID': crypto.randomUUID()
    };

    // Priorità: API Key > Cookie
    const apiKey = getStoredApiKey();
    if (apiKey) {
      headers['Authorization'] = `ApiKey ${apiKey}`;
    }

    return headers;
  }

  async get<T>(path: string, schema?: z.ZodType<T>): Promise<T> {
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method: 'GET',
      headers: this.getHeaders(),
      credentials: 'include'  // Cookie incluso
    });

    return this.handleResponse(response, schema);
  }

  async post<T>(path: string, body: unknown, schema?: z.ZodType<T>): Promise<T> {
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: this.getHeaders(),
      credentials: 'include',
      body: JSON.stringify(body)
    });

    return this.handleResponse(response, schema);
  }

  async postFile<T>(
    path: string,
    file: File,
    schema?: z.ZodType<T>
  ): Promise<T> {
    const formData = new FormData();
    formData.append('file', file);

    const headers = this.getHeaders();
    delete headers['Content-Type'];  // Browser imposterà multipart/form-data

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers,
      credentials: 'include',
      body: formData
    });

    return this.handleResponse(response, schema);
  }

  async downloadFile(path: string): Promise<{ blob: Blob; filename: string }> {
    const response = await this.fetchImpl(`${this.baseUrl}${path}`, {
      method: 'GET',
      headers: this.getHeaders(),
      credentials: 'include'
    });

    if (!response.ok) {
      throw await this.createError(response);
    }

    const blob = await response.blob();
    const contentDisposition = response.headers.get('Content-Disposition');
    const filename = contentDisposition?.match(/filename="(.+)"/)?.[1] ?? 'download';

    return { blob, filename };
  }

  private async handleResponse<T>(
    response: Response,
    schema?: z.ZodType<T>
  ): Promise<T> {
    if (!response.ok) {
      const error = await this.createError(response);
      logApiError(error, { path: response.url, status: response.status });
      throw error;
    }

    const data = await response.json();

    // Validazione con Zod se schema fornito
    if (schema) {
      try {
        return schema.parse(data);
      } catch (error) {
        if (error instanceof z.ZodError) {
          throw new ValidationError('Validation failed', error.errors);
        }
        throw error;
      }
    }

    return data as T;
  }

  private async createError(response: Response): Promise<Error> {
    const body = await response.json().catch(() => ({}));

    switch (response.status) {
      case 401:
        return new UnauthorizedError('Non autorizzato');
      case 403:
        return new Error('Accesso negato');
      case 404:
        return new Error('Risorsa non trovata');
      case 422:
        return new ValidationError('Dati non validi', body.errors);
      case 429:
        return new RateLimitError('Troppi tentativi');
      case 500:
        return new Error('Errore del server');
      default:
        return new NetworkError('Errore di rete');
    }
  }
}
```

### Feature Client Example: authClient

```tsx
// lib/api/clients/authClient.ts
import { HttpClient } from '../core/httpClient';
import {
  loginResponseSchema,
  sessionStatusSchema,
  twoFactorSetupSchema,
  userProfileSchema
} from '../schemas/auth.schemas';

export function createAuthClient(httpClient: HttpClient) {
  return {
    // API Key Authentication
    async loginWithApiKey(apiKey: string) {
      const response = await httpClient.post(
        '/api/v1/auth/apikey/login',
        { apiKey },
        loginResponseSchema
      );
      return response;
    },

    async logoutApiKey() {
      await httpClient.post('/api/v1/auth/apikey/logout', {});
    },

    // Session Management
    async getSessionStatus() {
      return await httpClient.get(
        '/api/v1/auth/session/status',
        sessionStatusSchema
      );
    },

    async extendSession() {
      await httpClient.post('/api/v1/auth/session/extend', {});
    },

    async getUserSessions() {
      return await httpClient.get('/api/v1/auth/sessions');
    },

    async revokeSession(sessionId: string) {
      await httpClient.delete(`/api/v1/auth/sessions/${sessionId}`);
    },

    // 2FA Management
    async getTwoFactorStatus() {
      return await httpClient.get('/api/v1/auth/2fa/status');
    },

    async setup2FA() {
      return await httpClient.post(
        '/api/v1/auth/2fa/setup',
        {},
        twoFactorSetupSchema
      );
    },

    async enable2FA(code: string) {
      await httpClient.post('/api/v1/auth/2fa/enable', { code });
    },

    async disable2FA(password: string, code: string) {
      await httpClient.post('/api/v1/auth/2fa/disable', { password, code });
    },

    // Profile Management
    async getProfile() {
      return await httpClient.get('/api/v1/users/profile', userProfileSchema);
    },

    async updateProfile(payload: UpdateProfilePayload) {
      return await httpClient.put('/api/v1/users/profile', payload);
    },

    async changePassword(request: ChangePasswordRequest) {
      await httpClient.put('/api/v1/users/profile/password', request);
    },

    // Preferences
    async getPreferences() {
      return await httpClient.get('/api/v1/auth/preferences');
    },

    async updatePreferences(payload: UpdatePreferencesPayload) {
      await httpClient.put('/api/v1/auth/preferences', payload);
    }
  };
}
```

### Factory e Default Client

```tsx
// lib/api/index.ts
import { HttpClient } from './core/httpClient';
import { createAuthClient } from './clients/authClient';
import { createGamesClient } from './clients/gamesClient';
import { createChatClient } from './clients/chatClient';
import { createPdfClient } from './clients/pdfClient';
import { createSessionsClient } from './clients/sessionsClient';
import { createConfigClient } from './clients/configClient';
import { createBggClient } from './clients/bggClient';
import { createAgentsClient } from './clients/agentsClient';

export interface ApiClient {
  auth: ReturnType<typeof createAuthClient>;
  games: ReturnType<typeof createGamesClient>;
  chat: ReturnType<typeof createChatClient>;
  pdf: ReturnType<typeof createPdfClient>;
  sessions: ReturnType<typeof createSessionsClient>;
  config: ReturnType<typeof createConfigClient>;
  bgg: ReturnType<typeof createBggClient>;
  agents: ReturnType<typeof createAgentsClient>;
}

export function createApiClient(config?: { fetchImpl?: typeof fetch }): ApiClient {
  const baseUrl = process.env.NEXT_PUBLIC_API_BASE ?? 'http://localhost:8080';

  const httpClient = new HttpClient({
    baseUrl,
    fetchImpl: config?.fetchImpl
  });

  return {
    auth: createAuthClient(httpClient),
    games: createGamesClient(httpClient),
    chat: createChatClient(httpClient),
    pdf: createPdfClient(httpClient),
    sessions: createSessionsClient(httpClient),
    config: createConfigClient(httpClient),
    bgg: createBggClient(httpClient),
    agents: createAgentsClient(httpClient)
  };
}

// Default singleton
export const api = createApiClient();
```

### Utilizzo

```tsx
// In qualsiasi componente
import { api } from '@/lib/api';

// 1. Autenticazione
const profile = await api.auth.getProfile();
await api.auth.enable2FA('123456');

// 2. Giochi
const games = await api.games.getAll({ search: 'Catan' });
const game = await api.games.getById('game-id');

// 3. Chat
const thread = await api.chat.createThread({ gameId: 'game-id', title: 'Nuova chat' });
await api.chat.addMessage(thread.id, { content: 'Come si gioca?', role: 'user' });

// 4. PDF
await api.pdf.upload(file, 'game-id');

// 5. Download file
const { blob, filename } = await api.chat.exportChat('chat-id', 'pdf');
const url = URL.createObjectURL(blob);
const a = document.createElement('a');
a.href = url;
a.download = filename;
a.click();
```

---

## 5. UI Component Library (Shadcn/UI)

### Inventario Componenti (22 Shadcn/UI)

Tutti i componenti includono:
- Implementazione TypeScript
- Storybook stories (150+ varianti)
- Dark mode support (next-themes)
- Accessibilità WCAG 2.1 AA
- TypeScript strict types

```
components/ui/
├── alert.tsx              # Alert messages
├── avatar.tsx             # User avatars
├── badge.tsx              # Status badges
├── button.tsx             # Buttons with variants
├── card.tsx               # Content cards
├── checkbox.tsx           # Checkboxes
├── dialog.tsx             # Modal dialogs
├── dropdown-menu.tsx      # Dropdown menus
├── form.tsx               # Form components
├── input.tsx              # Text inputs
├── label.tsx              # Form labels
├── progress.tsx           # Progress bars
├── select.tsx             # Select dropdowns
├── separator.tsx          # Visual separators
├── sheet.tsx              # Slide-in panels
├── skeleton.tsx           # Loading skeletons
├── sonner.tsx             # Toast notifications
├── switch.tsx             # Toggle switches
├── table.tsx              # Data tables
├── tabs.tsx               # Tab navigation
├── textarea.tsx           # Text areas
├── toggle.tsx             # Toggle buttons
└── toggle-group.tsx       # Toggle button groups
```

### Esempio: Button Component

```tsx
// components/ui/button.tsx
import * as React from 'react';
import { Slot } from '@radix-ui/react-slot';
import { cva, type VariantProps } from 'class-variance-authority';
import { cn } from '@/lib/utils';

const buttonVariants = cva(
  'inline-flex items-center justify-center rounded-md text-sm font-medium ring-offset-background transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2 disabled:pointer-events-none disabled:opacity-50',
  {
    variants: {
      variant: {
        default: 'bg-primary text-primary-foreground hover:bg-primary/90',
        destructive: 'bg-destructive text-destructive-foreground hover:bg-destructive/90',
        outline: 'border border-input bg-background hover:bg-accent hover:text-accent-foreground',
        secondary: 'bg-secondary text-secondary-foreground hover:bg-secondary/80',
        ghost: 'hover:bg-accent hover:text-accent-foreground',
        link: 'text-primary underline-offset-4 hover:underline'
      },
      size: {
        default: 'h-10 px-4 py-2',
        sm: 'h-9 rounded-md px-3',
        lg: 'h-11 rounded-md px-8',
        icon: 'h-10 w-10'
      }
    },
    defaultVariants: {
      variant: 'default',
      size: 'default'
    }
  }
);

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement>,
    VariantProps<typeof buttonVariants> {
  asChild?: boolean;
}

const Button = React.forwardRef<HTMLButtonElement, ButtonProps>(
  ({ className, variant, size, asChild = false, ...props }, ref) => {
    const Comp = asChild ? Slot : 'button';
    return (
      <Comp
        className={cn(buttonVariants({ variant, size, className }))}
        ref={ref}
        {...props}
      />
    );
  }
);
Button.displayName = 'Button';

export { Button, buttonVariants };
```

### Storybook Story

```tsx
// components/ui/button.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { Button } from './button';

const meta = {
  title: 'UI/Button',
  component: Button,
  parameters: {
    layout: 'centered'
  },
  tags: ['autodocs'],
  argTypes: {
    variant: {
      control: 'select',
      options: ['default', 'destructive', 'outline', 'secondary', 'ghost', 'link']
    },
    size: {
      control: 'select',
      options: ['default', 'sm', 'lg', 'icon']
    }
  }
} satisfies Meta<typeof Button>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    children: 'Button',
    variant: 'default'
  }
};

export const Destructive: Story = {
  args: {
    children: 'Delete',
    variant: 'destructive'
  }
};

export const Outline: Story = {
  args: {
    children: 'Outline',
    variant: 'outline'
  }
};

export const Loading: Story = {
  args: {
    children: (
      <>
        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
        Caricamento...
      </>
    ),
    disabled: true
  }
};

export const AllVariants: Story = {
  render: () => (
    <div className="flex gap-4 flex-wrap">
      <Button variant="default">Default</Button>
      <Button variant="destructive">Destructive</Button>
      <Button variant="outline">Outline</Button>
      <Button variant="secondary">Secondary</Button>
      <Button variant="ghost">Ghost</Button>
      <Button variant="link">Link</Button>
    </div>
  )
};
```

---

## 6. Autenticazione (Client-Side)

### Strategia Dual Auth

#### **1. Cookie-Based (Primaria)**

```tsx
// Flow:
// 1. Utente sottomette form login
// 2. loginAction() → Server Action Next.js
// 3. Backend imposta httpOnly cookie
// 4. AuthProvider aggiorna via TanStack Query
// 5. Cookie inviato automaticamente su tutte le richieste

// app/login/actions.ts
'use server';

export async function loginAction(
  prevState: any,
  formData: FormData
): Promise<{ success: boolean; user?: User; error?: string }> {
  const email = formData.get('email') as string;
  const password = formData.get('password') as string;

  try {
    const response = await fetch(`${process.env.API_BASE}/api/v1/auth/login`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ email, password }),
      credentials: 'include'
    });

    if (!response.ok) {
      return { success: false, error: 'Credenziali non valide' };
    }

    const data = await response.json();

    // Cookie già impostato dal backend (httpOnly, secure)
    return { success: true, user: data.user };
  } catch (error) {
    return { success: false, error: 'Errore di connessione' };
  }
}

// components/auth/LoginForm.tsx
'use client';

import { useFormState } from 'react-dom';
import { loginAction } from '@/app/login/actions';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';

export function LoginForm() {
  const [state, formAction] = useFormState(loginAction, { success: false });

  return (
    <form action={formAction} className="space-y-4">
      <Input name="email" type="email" placeholder="Email" required />
      <Input name="password" type="password" placeholder="Password" required />

      {state.error && (
        <p className="text-sm text-destructive">{state.error}</p>
      )}

      <Button type="submit" className="w-full">
        Accedi
      </Button>
    </form>
  );
}
```

#### **2. API Key-Based (Secondaria)**

```tsx
// Flow:
// 1. Utente inserisce API key in /settings (tab Advanced)
// 2. api.auth.loginWithApiKey(apiKey)
// 3. Backend valida chiave
// 4. Frontend salva in sessionStorage
// 5. HttpClient aggiunge header Authorization su tutte le richieste

// lib/api/core/apiKeyStore.ts
const API_KEY_STORAGE_KEY = 'meepleai_api_key';

export function setStoredApiKey(apiKey: string): void {
  if (typeof window !== 'undefined') {
    sessionStorage.setItem(API_KEY_STORAGE_KEY, apiKey);
  }
}

export function getStoredApiKey(): string | null {
  if (typeof window !== 'undefined') {
    return sessionStorage.getItem(API_KEY_STORAGE_KEY);
  }
  return null;
}

export function clearStoredApiKey(): void {
  if (typeof window !== 'undefined') {
    sessionStorage.removeItem(API_KEY_STORAGE_KEY);
  }
}

// app/settings/AdvancedTab.tsx
'use client';

import { useState } from 'react';
import { api } from '@/lib/api';
import { setStoredApiKey, clearStoredApiKey } from '@/lib/api/core/apiKeyStore';

export function AdvancedTab() {
  const [apiKey, setApiKey] = useState('');
  const [isLoggedIn, setIsLoggedIn] = useState(false);

  const handleApiKeyLogin = async () => {
    try {
      await api.auth.loginWithApiKey(apiKey);

      // Salva in sessionStorage
      setStoredApiKey(apiKey);
      setIsLoggedIn(true);
    } catch (error) {
      alert('API key non valida');
    }
  };

  const handleApiKeyLogout = async () => {
    await api.auth.logoutApiKey();
    clearStoredApiKey();
    setIsLoggedIn(false);
    setApiKey('');
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>Autenticazione API Key</CardTitle>
      </CardHeader>
      <CardContent>
        {!isLoggedIn ? (
          <div className="space-y-4">
            <Input
              type="password"
              placeholder="mpl_dev_xxxxxxxx"
              value={apiKey}
              onChange={(e) => setApiKey(e.target.value)}
            />
            <Button onClick={handleApiKeyLogin}>Login con API Key</Button>
          </div>
        ) : (
          <div className="space-y-4">
            <p className="text-sm text-green-600">✓ Autenticato con API Key</p>
            <Button variant="destructive" onClick={handleApiKeyLogout}>
              Logout
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
```

### Gestione Sessioni

```tsx
// hooks/useSessionCheck.ts
import { useState, useEffect } from 'react';
import { api } from '@/lib/api';

export function useSessionCheck() {
  const [remainingMinutes, setRemainingMinutes] = useState<number | null>(null);
  const [isNearExpiry, setIsNearExpiry] = useState(false);

  useEffect(() => {
    const checkSession = async () => {
      try {
        const status = await api.auth.getSessionStatus();

        const now = new Date();
        const expiresAt = new Date(status.expiresAt);
        const diffMs = expiresAt.getTime() - now.getTime();
        const diffMinutes = Math.floor(diffMs / 1000 / 60);

        setRemainingMinutes(diffMinutes);
        setIsNearExpiry(diffMinutes <= 5 && diffMinutes > 0);
      } catch (error) {
        // Sessione scaduta o non autenticato
        setRemainingMinutes(0);
      }
    };

    // Check iniziale
    checkSession();

    // Check ogni minuto
    const interval = setInterval(checkSession, 60 * 1000);

    return () => clearInterval(interval);
  }, []);

  return { remainingMinutes, isNearExpiry };
}

// components/modals/SessionWarningModal.tsx
'use client';

import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';

interface SessionWarningModalProps {
  remainingMinutes: number;
  onStayLoggedIn: () => void;
  onLogOut: () => void;
}

export function SessionWarningModal({
  remainingMinutes,
  onStayLoggedIn,
  onLogOut
}: SessionWarningModalProps) {
  return (
    <Dialog open={true}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Sessione in scadenza</DialogTitle>
        </DialogHeader>
        <p>
          La tua sessione scadrà tra {remainingMinutes} minut{remainingMinutes === 1 ? 'o' : 'i'}.
          Vuoi rimanere connesso?
        </p>
        <div className="flex gap-4 justify-end">
          <Button variant="outline" onClick={onLogOut}>
            Logout
          </Button>
          <Button onClick={onStayLoggedIn}>
            Rimani connesso
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}

// Utilizzo in layout
function RootLayoutClient({ children }: { children: React.ReactNode }) {
  const { remainingMinutes, isNearExpiry } = useSessionCheck();

  const handleStayLoggedIn = async () => {
    await api.auth.extendSession();
    // Il modal si chiuderà automaticamente quando isNearExpiry diventa false
  };

  const handleLogOut = async () => {
    await api.auth.logout();
    router.push('/login');
  };

  return (
    <>
      {isNearExpiry && remainingMinutes !== null && (
        <SessionWarningModal
          remainingMinutes={remainingMinutes}
          onStayLoggedIn={handleStayLoggedIn}
          onLogOut={handleLogOut}
        />
      )}
      {children}
    </>
  );
}
```

---

## 7. Testing

### Struttura Test

```
apps/web/
├── __tests__/             # Unit tests
│   ├── components/
│   ├── hooks/
│   └── lib/
├── playwright/            # E2E tests
│   ├── tests/
│   └── fixtures/
└── .storybook/           # Component tests (Chromatic)
```

### Unit Test Example (Jest + React Testing Library)

```tsx
// __tests__/components/chat/ChatInterface.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ChatInterface } from '@/components/chat/ChatInterface';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
};

describe('ChatInterface', () => {
  it('should render chat input and message list', () => {
    render(<ChatInterface gameId="game-123" />, { wrapper: createWrapper() });

    expect(screen.getByPlaceholderText(/scrivi un messaggio/i)).toBeInTheDocument();
    expect(screen.getByRole('list', { name: /messaggi/i })).toBeInTheDocument();
  });

  it('should send message on submit', async () => {
    const user = userEvent.setup();

    render(<ChatInterface gameId="game-123" />, { wrapper: createWrapper() });

    const input = screen.getByPlaceholderText(/scrivi un messaggio/i);
    const submitButton = screen.getByRole('button', { name: /invia/i });

    await user.type(input, 'Come si gioca a Catan?');
    await user.click(submitButton);

    await waitFor(() => {
      expect(screen.getByText('Come si gioca a Catan?')).toBeInTheDocument();
    });
  });

  it('should show optimistic message immediately', async () => {
    const user = userEvent.setup();

    render(<ChatInterface gameId="game-123" />, { wrapper: createWrapper() });

    const input = screen.getByPlaceholderText(/scrivi un messaggio/i);
    await user.type(input, 'Test message');
    await user.keyboard('{Enter}');

    // Messaggio dovrebbe apparire immediatamente (ottimistico)
    expect(screen.getByText('Test message')).toBeInTheDocument();
  });
});
```

### Hook Test Example

```tsx
// __tests__/hooks/queries/useCurrentUser.test.tsx
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useCurrentUser } from '@/hooks/queries/user';
import { api } from '@/lib/api';

jest.mock('@/lib/api');

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } }
  });

  return ({ children }: { children: React.ReactNode }) => (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
};

describe('useCurrentUser', () => {
  it('should fetch user data', async () => {
    const mockUser = {
      id: 'user-123',
      email: 'test@example.com',
      displayName: 'Test User'
    };

    (api.auth.getProfile as jest.Mock).mockResolvedValue(mockUser);

    const { result } = renderHook(() => useCurrentUser(), {
      wrapper: createWrapper()
    });

    expect(result.current.isLoading).toBe(true);

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual(mockUser);
  });

  it('should handle error', async () => {
    (api.auth.getProfile as jest.Mock).mockRejectedValue(
      new Error('Unauthorized')
    );

    const { result } = renderHook(() => useCurrentUser(), {
      wrapper: createWrapper()
    });

    await waitFor(() => {
      expect(result.current.isError).toBe(true);
    });

    expect(result.current.error).toBeTruthy();
  });
});
```

### E2E Test Example (Playwright)

```tsx
// playwright/tests/chat.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Chat Interface', () => {
  test.beforeEach(async ({ page }) => {
    // Login
    await page.goto('/login');
    await page.fill('input[name="email"]', 'test@example.com');
    await page.fill('input[name="password"]', 'password123');
    await page.click('button[type="submit"]');

    // Attendi redirect
    await page.waitForURL('/chat');
  });

  test('should send and receive message', async ({ page }) => {
    // Seleziona gioco
    await page.click('[data-testid="game-selector"]');
    await page.click('[data-testid="game-option-catan"]');

    // Invia messaggio
    const input = page.locator('[data-testid="chat-input"]');
    await input.fill('Come si gioca?');
    await input.press('Enter');

    // Verifica messaggio utente
    await expect(page.locator('text=Come si gioca?')).toBeVisible();

    // Attendi risposta assistente (streaming)
    await expect(
      page.locator('[data-role="assistant"]').first()
    ).toBeVisible({ timeout: 10000 });
  });

  test('should export chat to PDF', async ({ page }) => {
    // Seleziona chat esistente
    await page.click('[data-testid="chat-list-item"]');

    // Click export
    await page.click('[data-testid="export-button"]');
    await page.click('[data-testid="export-pdf"]');

    // Verifica download
    const download = await page.waitForEvent('download');
    expect(download.suggestedFilename()).toMatch(/chat-.+\.pdf/);
  });
});
```

### Copertura Test

```bash
# Unit tests (Jest)
pnpm test
pnpm test:coverage

# E2E tests (Playwright)
pnpm test:e2e
pnpm test:e2e:ui  # UI mode

# Coverage report
pnpm test:coverage --coverage --coverageReporters=html
# Apre report in coverage/index.html

# Target: 90%+ coverage
# Attuale: 90.03% (4,033 test)
```

---

## 8. Performance & Optimization

### Code Splitting

```tsx
// Lazy loading componenti pesanti
import dynamic from 'next/dynamic';

const PdfViewer = dynamic(() => import('@/components/pdf/PdfViewer'), {
  loading: () => <Skeleton className="h-[600px]" />,
  ssr: false  // Solo client-side
});

const DiffViewer = dynamic(() => import('@/components/diff/DiffViewer'), {
  loading: () => <LoadingSpinner />
});
```

### Image Optimization

```tsx
import Image from 'next/image';

// Ottimizzazione automatica
<Image
  src="/games/catan.jpg"
  alt="Catan"
  width={300}
  height={200}
  placeholder="blur"
  blurDataURL="data:image/..."
  priority  // Per above-the-fold images
/>
```

### Web Workers (Upload Queue)

```tsx
// hooks/upload/uploadWorker.ts
const worker = new Worker(new URL('./upload.worker.ts', import.meta.url));

worker.postMessage({
  type: 'upload',
  file: file,
  uploadUrl: '/api/v1/pdf/upload'
});

worker.onmessage = (e) => {
  if (e.data.type === 'progress') {
    setProgress(e.data.progress);
  }
  if (e.data.type === 'complete') {
    onComplete(e.data.result);
  }
};

// upload.worker.ts (Web Worker)
self.onmessage = async (e) => {
  const { file, uploadUrl } = e.data;

  const xhr = new XMLHttpRequest();

  xhr.upload.addEventListener('progress', (event) => {
    if (event.lengthComputable) {
      const progress = (event.loaded / event.total) * 100;
      self.postMessage({ type: 'progress', progress });
    }
  });

  xhr.addEventListener('load', () => {
    self.postMessage({ type: 'complete', result: JSON.parse(xhr.responseText) });
  });

  const formData = new FormData();
  formData.append('file', file);

  xhr.open('POST', uploadUrl);
  xhr.send(formData);
};
```

---

## 9. Internazionalizzazione (i18n)

```tsx
// locales/it/common.json
{
  "nav": {
    "home": "Home",
    "chat": "Chat",
    "games": "Giochi",
    "settings": "Impostazioni"
  },
  "chat": {
    "inputPlaceholder": "Scrivi un messaggio...",
    "send": "Invia",
    "noMessages": "Nessun messaggio"
  }
}

// components/providers/IntlProvider.tsx
'use client';

import { IntlProvider as ReactIntlProvider } from 'react-intl';
import { useEffect, useState } from 'react';

export function IntlProvider({ children, locale }: {
  children: React.ReactNode;
  locale: string;
}) {
  const [messages, setMessages] = useState({});

  useEffect(() => {
    import(`@/locales/${locale}/common.json`).then((module) => {
      setMessages(module.default);
    });
  }, [locale]);

  return (
    <ReactIntlProvider locale={locale} messages={messages}>
      {children}
    </ReactIntlProvider>
  );
}

// Utilizzo
import { useIntl } from 'react-intl';

function ChatInput() {
  const intl = useIntl();

  return (
    <input
      placeholder={intl.formatMessage({ id: 'chat.inputPlaceholder' })}
    />
  );
}
```

---

## 10. Comandi Utili

```bash
# Sviluppo
pnpm dev                    # Dev server (localhost:3000)
pnpm build                  # Production build
pnpm start                  # Production server
pnpm lint                   # ESLint
pnpm typecheck              # TypeScript check

# Testing
pnpm test                   # Unit tests
pnpm test:watch             # Watch mode
pnpm test:coverage          # Coverage report
pnpm test:e2e               # E2E tests (Playwright)
pnpm test:e2e:ui            # Playwright UI mode

# Storybook
pnpm storybook              # Dev (localhost:6006)
pnpm build-storybook        # Build statico
pnpm chromatic              # Visual regression testing

# Performance
pnpm test:performance       # Playwright performance tests
pnpm lighthouse:ci          # Lighthouse CI audits

# Coverage
./tools/run-frontend-coverage.sh --open   # Coverage con HTML report
```

---

## 11. Best Practices

### 1. Component Composition

```tsx
// ✅ Buono: Composizione
<Card>
  <CardHeader>
    <CardTitle>Titolo</CardTitle>
  </CardHeader>
  <CardContent>
    Contenuto
  </CardContent>
</Card>

// ❌ Cattivo: Props pesanti
<Card
  title="Titolo"
  content="Contenuto"
  headerClass="..."
  contentClass="..."
/>
```

### 2. Custom Hooks

```tsx
// ✅ Buono: Logica riutilizzabile
function usePagination(totalItems: number, itemsPerPage: number) {
  const [page, setPage] = useState(1);
  const totalPages = Math.ceil(totalItems / itemsPerPage);

  const nextPage = () => setPage((p) => Math.min(p + 1, totalPages));
  const prevPage = () => setPage((p) => Math.max(p - 1, 1));

  return { page, totalPages, nextPage, prevPage, setPage };
}

// ❌ Cattivo: Logica duplicata in componenti
```

### 3. Error Boundaries

```tsx
// components/errors/ErrorBoundary.tsx
'use client';

import { Component, ReactNode } from 'react';

interface Props {
  children: ReactNode;
  fallback?: ReactNode;
}

interface State {
  hasError: boolean;
  error?: Error;
}

export class ErrorBoundary extends Component<Props, State> {
  constructor(props: Props) {
    super(props);
    this.state = { hasError: false };
  }

  static getDerivedStateFromError(error: Error): State {
    return { hasError: true, error };
  }

  componentDidCatch(error: Error, errorInfo: any) {
    console.error('ErrorBoundary caught:', error, errorInfo);
  }

  render() {
    if (this.state.hasError) {
      return this.props.fallback || (
        <div className="p-4 text-center">
          <h2 className="text-xl font-bold text-destructive">
            Qualcosa è andato storto
          </h2>
          <p className="text-muted-foreground mt-2">
            {this.state.error?.message}
          </p>
        </div>
      );
    }

    return this.props.children;
  }
}
```

### 4. TypeScript Strict

```tsx
// ✅ Buono: Type safety
interface User {
  id: string;
  email: string;
  displayName: string;
  role: 'admin' | 'editor' | 'user';
}

function updateUser(userId: string, updates: Partial<User>): Promise<User> {
  // ...
}

// ❌ Cattivo: any
function updateUser(userId: any, updates: any): any {
  // ...
}
```

---

## 12. Risorse

| Documento | Path |
|-----------|------|
| **Storybook Guide** | `apps/web/STORYBOOK.md` |
| **Chromatic Guide** | `apps/web/.storybook/CHROMATIC.md` |
| **Shadcn/UI Installation** | `docs/04-frontend/shadcn-ui-installation.md` |
| **Frontend Coverage** | `docs/02-development/testing/frontend/code-coverage.md` |
| **Performance Testing** | `docs/02-development/testing/frontend/performance-testing.md` |

---

**Versione:** 1.0
**Ultimo Aggiornamento:** 2025-11-19
**Autore:** Engineering Lead
