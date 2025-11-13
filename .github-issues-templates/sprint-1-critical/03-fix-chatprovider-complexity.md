# [REFACTOR] Split ChatProvider into Multiple Contexts

## 🎯 Objective

Decompose the monolithic `ChatProvider` (639 lines) into focused, maintainable context providers following single-responsibility principle.

## 📋 Current State

**Problem**: `ChatProvider.tsx` is doing too much:

- **639 lines** of code in a single file
- **35+ functions** in one context
- **Map-based state** (`chatStatesByGame`) that's not serializable
- **useRef for previous state** (anti-pattern)
- **Disabled ESLint rules** (red flag: `// eslint-disable-next-line react-hooks/exhaustive-deps`)
- **Mixed concerns**: auth, games, agents, chats, messages, UI state, editing

**Current structure**:
```tsx
ChatProvider {
  // Authentication (should be separate)
  - authUser
  - loadCurrentUser()

  // Game Management (should be separate)
  - games
  - selectedGameId
  - loadGames()
  - selectGame()

  // Agent Management (should be separate)
  - agents
  - selectedAgentId
  - loadAgents()
  - selectAgent()

  // Chat Management (legitimate ChatProvider concern)
  - chats (Map<string, GameChatState>) ❌ Complex
  - activeChatId
  - messages
  - createChat()
  - deleteChat()
  - selectChat()

  // Message Operations (should be separate)
  - sendMessage()
  - editMessage()
  - deleteMessage()
  - setMessageFeedback()

  // UI State (should be separate)
  - sidebarCollapsed
  - toggleSidebar()
  - editingMessageId
  - editContent
  - inputValue
  - searchMode

  // ... 35 total functions
}
```

## ✅ Acceptance Criteria

- [ ] Split into 4-5 focused contexts
- [ ] Each context ≤ 200 lines
- [ ] Remove Map-based state, use normalized data structure
- [ ] Remove useRef tracking pattern
- [ ] Fix all ESLint disabled rules
- [ ] State is serializable (localStorage-ready)
- [ ] Each context has unit tests
- [ ] No breaking changes to consuming components
- [ ] Performance maintained or improved
- [ ] Documentation updated

## 🏗️ Implementation Plan

### New Architecture

```
┌─────────────────────────────────────────────┐
│          Component Tree                     │
├─────────────────────────────────────────────┤
│                                             │
│  AuthProvider  (shared, global)            │
│  └─ GameProvider                            │
│     └─ ChatProvider                         │
│        └─ UIProvider                        │
│                                             │
└─────────────────────────────────────────────┘
```

### 1. Extract `AuthProvider` (Global)

**File**: `apps/web/src/components/auth/AuthProvider.tsx`

```tsx
interface AuthContextValue {
  user: AuthUser | null;
  loading: boolean;
  error: string | null;
  login: (email: string, password: string) => Promise<void>;
  register: (data: RegisterData) => Promise<void>;
  logout: () => Promise<void>;
  refreshUser: () => Promise<void>;
}

export function AuthProvider({ children }: PropsWithChildren) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Load user on mount
  useEffect(() => {
    void loadCurrentUser();
  }, []);

  const loadCurrentUser = async () => {
    setLoading(true);
    try {
      const res = await api.get<AuthResponse>('/api/v1/auth/me');
      setUser(res?.user ?? null);
    } catch (err) {
      setUser(null);
    } finally {
      setLoading(false);
    }
  };

  const login = async (email: string, password: string) => {
    // ... implementation from issue #01
  };

  const logout = async () => {
    await api.post('/api/v1/auth/logout');
    setUser(null);
  };

  const value = useMemo(() => ({
    user,
    loading,
    error,
    login,
    register,
    logout,
    refreshUser: loadCurrentUser
  }), [user, loading, error]);

  return (
    <AuthContext.Provider value={value}>
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

**Lines**: ~120 (extracted from ChatProvider)

---

### 2. Extract `GameProvider`

**File**: `apps/web/src/components/game/GameProvider.tsx`

```tsx
interface GameContextValue {
  games: Game[];
  selectedGame: Game | null;
  agents: Agent[];
  selectedAgent: Agent | null;
  loading: {
    games: boolean;
    agents: boolean;
  };
  selectGame: (gameId: string) => Promise<void>;
  selectAgent: (agentId: string) => void;
  createGame: (name: string) => Promise<Game>;
  refreshGames: () => Promise<void>;
}

export function GameProvider({ children }: PropsWithChildren) {
  const [games, setGames] = useState<Game[]>([]);
  const [selectedGameId, setSelectedGameId] = useState<string | null>(null);
  const [agents, setAgents] = useState<Agent[]>([]);
  const [selectedAgentId, setSelectedAgentId] = useState<string | null>(null);
  const [loading, setLoading] = useState({ games: false, agents: false });

  // Derived values
  const selectedGame = useMemo(
    () => games.find(g => g.id === selectedGameId) ?? null,
    [games, selectedGameId]
  );

  const selectedAgent = useMemo(
    () => agents.find(a => a.id === selectedAgentId) ?? null,
    [agents, selectedAgentId]
  );

  // Load games on mount
  useEffect(() => {
    void loadGames();
  }, []);

  // Load agents when game changes
  useEffect(() => {
    if (selectedGameId) {
      void loadAgents(selectedGameId);
    }
  }, [selectedGameId]);

  const loadGames = async () => {
    setLoading(prev => ({ ...prev, games: true }));
    try {
      const fetchedGames = await api.get<Game[]>('/api/v1/games');
      setGames(fetchedGames ?? []);

      // Auto-select first game
      if (fetchedGames && fetchedGames.length > 0 && !selectedGameId) {
        setSelectedGameId(fetchedGames[0].id);
      }
    } catch (err) {
      console.error('Failed to load games:', err);
    } finally {
      setLoading(prev => ({ ...prev, games: false }));
    }
  };

  const loadAgents = async (gameId: string) => {
    setLoading(prev => ({ ...prev, agents: true }));
    try {
      const fetchedAgents = await api.get<Agent[]>(`/api/v1/games/${gameId}/agents`);
      setAgents(fetchedAgents ?? []);

      // Auto-select first agent
      if (fetchedAgents && fetchedAgents.length > 0) {
        setSelectedAgentId(fetchedAgents[0].id);
      }
    } catch (err) {
      console.error('Failed to load agents:', err);
    } finally {
      setLoading(prev => ({ ...prev, agents: false }));
    }
  };

  const selectGame = async (gameId: string) => {
    setSelectedGameId(gameId);
    // Agents will load via useEffect
  };

  const selectAgent = (agentId: string) => {
    setSelectedAgentId(agentId);
  };

  const createGame = async (name: string) => {
    const newGame = await api.post<Game>('/api/v1/games', { name });
    setGames(prev => [...prev, newGame]);
    setSelectedGameId(newGame.id);
    return newGame;
  };

  const value = useMemo(() => ({
    games,
    selectedGame,
    agents,
    selectedAgent,
    loading,
    selectGame,
    selectAgent,
    createGame,
    refreshGames: loadGames
  }), [games, selectedGame, agents, selectedAgent, loading]);

  return (
    <GameContext.Provider value={value}>
      {children}
    </GameContext.Provider>
  );
}

export function useGame() {
  const context = useContext(GameContext);
  if (!context) {
    throw new Error('useGame must be used within GameProvider');
  }
  return context;
}
```

**Lines**: ~180

---

### 3. Refactor `ChatProvider` (Simplified)

**File**: `apps/web/src/components/chat/ChatProvider.tsx` (new version)

```tsx
/**
 * Normalized state structure (no Map!)
 */
interface ChatState {
  chats: Record<string, Chat[]>;        // Normalized by gameId
  activeChatIds: Record<string, string>; // Active chat per game
  messages: Record<string, Message[]>;   // Messages by chatId
}

interface ChatContextValue {
  // For current game (derived from GameProvider)
  chats: Chat[];
  activeChat: Chat | null;
  messages: Message[];

  // Operations
  createChat: () => Promise<void>;
  deleteChat: (chatId: string) => Promise<void>;
  selectChat: (chatId: string) => Promise<void>;

  // Message operations
  sendMessage: (content: string) => Promise<void>;
  editMessage: (messageId: string, content: string) => Promise<void>;
  deleteMessage: (messageId: string) => Promise<void>;
  setMessageFeedback: (messageId: string, feedback: 'helpful' | 'not-helpful') => Promise<void>;

  // State
  loading: {
    chats: boolean;
    messages: boolean;
    sending: boolean;
  };
  error: string | null;
}

export function ChatProvider({ children }: PropsWithChildren) {
  const { selectedGame, selectedAgent } = useGame();

  // Normalized state (serializable!)
  const [state, setState] = useState<ChatState>({
    chats: {},
    activeChatIds: {},
    messages: {}
  });

  const [loading, setLoading] = useState({
    chats: false,
    messages: false,
    sending: false
  });
  const [error, setError] = useState<string | null>(null);

  // Derived values for current game
  const currentGameId = selectedGame?.id;
  const chats = useMemo(
    () => currentGameId ? (state.chats[currentGameId] ?? []) : [],
    [state.chats, currentGameId]
  );

  const activeChatId = currentGameId ? state.activeChatIds[currentGameId] : null;
  const activeChat = useMemo(
    () => chats.find(c => c.id === activeChatId) ?? null,
    [chats, activeChatId]
  );

  const messages = useMemo(
    () => activeChatId ? (state.messages[activeChatId] ?? []) : [],
    [state.messages, activeChatId]
  );

  // Load chats when game changes
  useEffect(() => {
    if (currentGameId) {
      void loadChats(currentGameId);
    }
  }, [currentGameId]);

  // Load messages when active chat changes
  useEffect(() => {
    if (activeChatId) {
      void loadMessages(activeChatId);
    }
  }, [activeChatId]);

  const loadChats = async (gameId: string) => {
    setLoading(prev => ({ ...prev, chats: true }));
    try {
      const fetchedChats = await api.get<Chat[]>(`/api/v1/chats?gameId=${gameId}`);
      setState(prev => ({
        ...prev,
        chats: {
          ...prev.chats,
          [gameId]: fetchedChats ?? []
        }
      }));
    } catch (err) {
      console.error('Failed to load chats:', err);
      setError('Failed to load chats');
    } finally {
      setLoading(prev => ({ ...prev, chats: false }));
    }
  };

  const loadMessages = async (chatId: string) => {
    setLoading(prev => ({ ...prev, messages: true }));
    try {
      const fetchedMessages = await api.get<Message[]>(`/api/v1/chats/${chatId}/messages`);
      setState(prev => ({
        ...prev,
        messages: {
          ...prev.messages,
          [chatId]: fetchedMessages ?? []
        }
      }));
      setError(null);
    } catch (err) {
      console.error('Failed to load messages:', err);
      setError('Failed to load messages');
    } finally {
      setLoading(prev => ({ ...prev, messages: false }));
    }
  };

  const createChat = async () => {
    if (!currentGameId || !selectedAgent) return;

    try {
      const newChat = await api.post<Chat>('/api/v1/chats', {
        gameId: currentGameId,
        agentId: selectedAgent.id
      });

      // Add to chats for current game
      setState(prev => ({
        ...prev,
        chats: {
          ...prev.chats,
          [currentGameId]: [newChat, ...(prev.chats[currentGameId] ?? [])]
        },
        activeChatIds: {
          ...prev.activeChatIds,
          [currentGameId]: newChat.id
        },
        messages: {
          ...prev.messages,
          [newChat.id]: []
        }
      }));
    } catch (err) {
      console.error('Failed to create chat:', err);
      setError('Failed to create chat');
    }
  };

  const selectChat = async (chatId: string) => {
    if (!currentGameId) return;

    setState(prev => ({
      ...prev,
      activeChatIds: {
        ...prev.activeChatIds,
        [currentGameId]: chatId
      }
    }));

    await loadMessages(chatId);
  };

  const sendMessage = async (content: string) => {
    if (!activeChatId || !content.trim()) return;

    setLoading(prev => ({ ...prev, sending: true }));

    const tempMessage: Message = {
      id: `temp-${Date.now()}`,
      role: 'user',
      content: content.trim(),
      timestamp: new Date()
    };

    // Optimistic update
    setState(prev => ({
      ...prev,
      messages: {
        ...prev.messages,
        [activeChatId]: [...(prev.messages[activeChatId] ?? []), tempMessage]
      }
    }));

    try {
      // Actual API call (to be implemented with streaming)
      // For now, placeholder
    } catch (err) {
      console.error('Failed to send message:', err);
      setError('Failed to send message');

      // Rollback optimistic update
      setState(prev => ({
        ...prev,
        messages: {
          ...prev.messages,
          [activeChatId]: (prev.messages[activeChatId] ?? []).filter(
            m => m.id !== tempMessage.id
          )
        }
      }));
    } finally {
      setLoading(prev => ({ ...prev, sending: false }));
    }
  };

  // ... other message operations

  const value = useMemo(() => ({
    chats,
    activeChat,
    messages,
    createChat,
    deleteChat,
    selectChat,
    sendMessage,
    editMessage,
    deleteMessage,
    setMessageFeedback,
    loading,
    error
  }), [chats, activeChat, messages, loading, error]);

  return (
    <ChatContext.Provider value={value}>
      {children}
    </ChatContext.Provider>
  );
}
```

**Lines**: ~250 (down from 639!)

---

### 4. Extract `UIProvider`

**File**: `apps/web/src/components/ui/UIProvider.tsx`

```tsx
interface UIContextValue {
  // Sidebar
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;

  // Message editing
  editingMessageId: string | null;
  editContent: string;
  startEdit: (messageId: string, content: string) => void;
  cancelEdit: () => void;
  saveEdit: (chatId: string) => Promise<void>;

  // Input
  inputValue: string;
  setInputValue: (value: string) => void;

  // Search mode
  searchMode: string;
  setSearchMode: (mode: string) => void;
}

export function UIProvider({ children }: PropsWithChildren) {
  const [sidebarCollapsed, setSidebarCollapsed] = useState(false);
  const [editingMessageId, setEditingMessageId] = useState<string | null>(null);
  const [editContent, setEditContent] = useState('');
  const [inputValue, setInputValue] = useState('');
  const [searchMode, setSearchMode] = useState('Hybrid');

  const { editMessage } = useChat();

  const toggleSidebar = useCallback(() => {
    setSidebarCollapsed(prev => !prev);
  }, []);

  const startEdit = useCallback((messageId: string, content: string) => {
    setEditingMessageId(messageId);
    setEditContent(content);
  }, []);

  const cancelEdit = useCallback(() => {
    setEditingMessageId(null);
    setEditContent('');
  }, []);

  const saveEdit = useCallback(async (chatId: string) => {
    if (!editingMessageId) return;

    await editMessage(editingMessageId, editContent);
    cancelEdit();
  }, [editingMessageId, editContent, editMessage]);

  const value = useMemo(() => ({
    sidebarCollapsed,
    toggleSidebar,
    editingMessageId,
    editContent,
    startEdit,
    cancelEdit,
    saveEdit,
    inputValue,
    setInputValue,
    searchMode,
    setSearchMode
  }), [
    sidebarCollapsed,
    editingMessageId,
    editContent,
    inputValue,
    searchMode
  ]);

  return (
    <UIContext.Provider value={value}>
      {children}
    </UIContext.Provider>
  );
}
```

**Lines**: ~90

---

### 5. Update Chat Page

**File**: `apps/web/src/pages/chat.tsx`

```tsx
export default function ChatPage() {
  const { user } = useAuth();

  if (!user) {
    return <AuthRequired />;
  }

  return (
    <AuthProvider>
      <GameProvider>
        <ChatProvider>
          <UIProvider>
            <div className="flex h-screen">
              <ChatSidebar />
              <ChatContent />
            </div>
          </UIProvider>
        </ChatProvider>
      </GameProvider>
    </AuthProvider>
  );
}
```

## 🧪 Testing

### Unit Tests

```tsx
// Each provider gets its own test file
describe('AuthProvider', () => {
  it('loads user on mount', async () => {});
  it('handles login', async () => {});
  it('handles logout', async () => {});
});

describe('GameProvider', () => {
  it('loads games on mount', async () => {});
  it('loads agents when game changes', async () => {});
  it('auto-selects first game', async () => {});
});

describe('ChatProvider', () => {
  it('loads chats when game changes', async () => {});
  it('normalizes state correctly', () => {});
  it('creates new chat', async () => {});
  it('handles optimistic updates', () => {});
});

describe('UIProvider', () => {
  it('toggles sidebar', () => {});
  it('manages edit state', () => {});
  it('manages input value', () => {});
});
```

### Integration Tests

Test the entire provider tree:

```tsx
describe('Chat Page Integration', () => {
  it('loads games → chats → messages flow', async () => {
    render(
      <AuthProvider>
        <GameProvider>
          <ChatProvider>
            <ChatPage />
          </ChatProvider>
        </GameProvider>
      </AuthProvider>
    );

    // Wait for games to load
    await waitFor(() => expect(screen.getByText('Gloomhaven')).toBeInTheDocument());

    // Select game
    fireEvent.click(screen.getByText('Gloomhaven'));

    // Wait for chats to load
    await waitFor(() => expect(screen.getByText('Chat 1')).toBeInTheDocument());
  });
});
```

## 📦 Files to Create/Modify

**New Files** (4):
- `apps/web/src/components/auth/AuthProvider.tsx`
- `apps/web/src/components/game/GameProvider.tsx`
- `apps/web/src/components/ui/UIProvider.tsx`
- `apps/web/src/hooks/useAuth.ts` (re-export from AuthProvider)
- `apps/web/src/hooks/useGame.ts` (re-export from GameProvider)
- `apps/web/src/hooks/useUI.ts` (re-export from UIProvider)

**Modified Files** (3):
- `apps/web/src/components/chat/ChatProvider.tsx` (639 → 250 lines)
- `apps/web/src/pages/chat.tsx` (update provider nesting)
- `apps/web/src/pages/_app.tsx` (wrap with AuthProvider)

**Test Files** (4):
- `apps/web/src/__tests__/components/auth/AuthProvider.test.tsx`
- `apps/web/src/__tests__/components/game/GameProvider.test.tsx`
- `apps/web/src/__tests__/components/chat/ChatProvider.test.tsx`
- `apps/web/src/__tests__/components/ui/UIProvider.test.tsx`

## 📊 Impact

**Maintainability**:
- ChatProvider: 639 → 250 lines (61% reduction)
- Average context size: ~150 lines (easy to understand)
- Single responsibility: Each context has ONE job
- Testability: 4x easier (isolated contexts)

**Performance**:
- Re-renders: Fewer (more granular subscription)
- Bundle size: Negligible (+1 KB for extra contexts)
- Serialization: Possible (localStorage caching)

**Developer Experience**:
- ESLint warnings: 0 (down from 5+)
- Type safety: Improved (no Map)
- Debugging: Easier (React DevTools shows 4 contexts)

## ⏱️ Effort Estimate

**1.5 days** (12 hours)

- Day 1 AM: Extract AuthProvider + GameProvider (4h)
- Day 1 PM: Refactor ChatProvider + UIProvider (4h)
- Day 2 AM: Testing + integration (4h)

## 📚 Dependencies

- None (self-contained refactor)

## 🔗 Related Issues

- #TBD: Auth Modal (#01) - Use AuthProvider
- #TBD: Performance Optimization - Measure re-render improvements

## 📝 Notes

- Preserve backward compatibility with gradual migration
- Consider localStorage persistence for ChatState (future)
- WebSocket integration easier with normalized state (future)
- TypeScript strict mode helps catch bugs during refactor

---

**Priority**: 🔴 Critical
**Sprint**: Sprint 1
**Effort**: 1.5d (12h)
**Labels**: `frontend`, `refactor`, `architecture`, `state-management`, `sprint-1`, `priority-critical`
