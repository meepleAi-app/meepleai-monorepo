# CHAT-02 Follow-Up Questions Test Suite - Fixing Analysis

## Executive Summary

The CHAT-02 test suite in `chat.supplementary.test.tsx` is **skipped** because the feature was **removed during architecture refactoring** and tests were written for the old monolithic `chat.tsx` that no longer exists.

**Status**: Feature partially re-integrated, tests require architecture-appropriate rewrite

**Time Invested**: ~4-5 hours (matches estimate)

**Blockers**:
- ChatProvider doesn't auto-load games/agents (incomplete state management)
- Tests mock old API patterns that don't match new component structure
- Test approach assumes end-to-end flow that new architecture doesn't support

---

## Historical Context

### Timeline

1. **Oct 18, 2025** - CHAT-02 implemented (commit fd1bcd7f)
   - Feature: AI-generated follow-up questions
   - Tests: 91/91 passing
   - Implementation: Monolithic `chat.tsx` with full state management

2. **Later** - Chat page refactored (commit b50a9e13)
   - New architecture: ChatProvider + ChatSidebar + ChatContent + Message components
   - **FollowUpQuestions integration removed** during refactor
   - Tests began failing, were marked `.skip`

3. **Today** - Attempted test fix
   - Re-integrated FollowUpQuestions into Message component
   - Fixed mock data structure
   - Identified architectural mismatch between tests and implementation

---

## Root Cause Analysis

### Issue 1: Feature Lost in Refactoring

**Original Implementation** (`chat.tsx` @ fd1bcd7f):
```typescript
// Old monolithic architecture
const [messages, setMessages] = useState<Message[]>([]);

// useChatStreaming callback includes followUpQuestions
onComplete: (answer, snippets, metadata) => {
  const assistantMessage: Message = {
    ...
    followUpQuestions: metadata.followUpQuestions  // ✅ Captured
  };
  setMessages(prev => [...prev, assistantMessage]);
}

// Rendered directly in component
{messages.map(msg => (
  <>
    <MessageBubble content={msg.content} />
    {msg.followUpQuestions && (
      <FollowUpQuestions
        questions={msg.followUpQuestions}
        onQuestionClick={setInputValue}  // ✅ Wired
      />
    )}
  </>
))}
```

**New Architecture** (post-refactor):
```typescript
// ChatProvider: No useChatStreaming integration
// ChatContent: Delegates to MessageList
// MessageList: Maps Message components
// Message: NOW includes FollowUpQuestions (as of today's fix) ✅

// BUT: sendMessage doesn't call AI yet
const sendMessage = async (content: string) => {
  // Note: Streaming integration will be added in future enhancement
  // For now, this creates the chat and user message
};
```

**Gap**: Messages with `followUpQuestions` are never created because streaming isn't integrated in ChatProvider.

### Issue 2: Incomplete ChatProvider State Management

**What's Missing**:
```typescript
// ChatProvider.tsx
export function ChatProvider({ children }: ChatProviderProps) {
  const [authUser, setAuthUser] = useState<AuthUser | null>(null);  // ❌ Never populated
  const [games, setGames] = useState<Game[]>([]);  // ❌ Never populated

  // No useEffect to load games!
  // No useEffect to load auth!
}
```

**ChatPage does load auth**:
```typescript
// pages/chat.tsx
useEffect(() => {
  void loadCurrentUser();  // ✅ Loads /api/v1/auth/me
}, []);

if (!authUser) {
  return <LoginRequired />;
}

return (
  <ChatProvider>  // ❌ Provider doesn't receive authUser as prop
    <ChatSidebar />  // Expects games from context, but they're never loaded
    <ChatContent />
  </ChatProvider>
);
```

**Design Issue**: ChatProvider expects games/agents but never loads them.

### Issue 3: Test Architecture Mismatch

**Tests Expect** (old monolithic approach):
```typescript
setupAuthenticatedState();  // Mocks: /auth/me, /games, /agents, /chats

render(<ChatPage />);

// Expects component to:
// 1. Load auth
// 2. Load games
// 3. Load agents
// 4. Load chats
// 5. Click chat item
// 6. Load messages with followUpQuestions
// 7. Render FollowUpQuestions component

await waitFor(() => {
  expect(screen.getByTestId('follow-up-questions')).toBeInTheDocument();
});
```

**Reality** (new architecture):
```typescript
render(<ChatPage />);

// ChatPage loads auth ✅
// ChatProvider renders but games=[] ❌
// No way to populate games without mocking ChatProvider directly
// Tests timeout waiting for games to appear
```

---

## Work Completed

### 1. Feature Re-Integration ✅

**File**: `apps/web/src/components/chat/Message.tsx`

Added:
```typescript
import { FollowUpQuestions } from '../FollowUpQuestions';

// In Message component
const { setInputValue } = useChatContext();

const handleFollowUpClick = (question: string) => {
  setInputValue(question);
  const inputElement = document.querySelector<HTMLTextAreaElement>('textarea[placeholder*="Fai una domanda"]');
  if (inputElement) {
    inputElement.focus();
  }
};

// After message feedback section
{!isUser && !isDeleted && message.followUpQuestions && message.followUpQuestions.length > 0 && (
  <div style={{ maxWidth: '75%' }} data-testid="follow-up-questions">
    <FollowUpQuestions
      questions={message.followUpQuestions}
      onQuestionClick={handleFollowUpClick}
      disabled={loading.sending}
    />
  </div>
)}
```

### 2. Test Data Fixes ✅

**File**: `apps/web/src/__tests__/pages/chat.supplementary.test.tsx`

Fixed mock message structure:
```typescript
// Before
{
  id: 'msg-2',
  level: 'agent',  // ❌ Wrong field
  message: 'Answer...',
}

// After
{
  id: 'msg-2',
  level: 'agent',  // Backend field
  role: 'assistant',  // ✅ Frontend field (required by Message component)
  message: 'Answer...',
  followUpQuestions: ['Q1', 'Q2', 'Q3']  // ✅ Added
}
```

### 3. Removed `.skip` ✅

Changed:
```typescript
describe.skip('CHAT-02: Follow-Up Questions', () => {
```

To:
```typescript
describe('CHAT-02: Follow-Up Questions', () => {
  // FIXED: Integrated FollowUpQuestions into new ChatProvider/Message architecture
```

---

## Remaining Issues

### Blocker 1: ChatProvider Doesn't Load Games

**Test Expectation**:
```typescript
setupAuthenticatedState();  // Mocks 4 API calls

render(<ChatPage />);

await waitFor(() => {
  expect(screen.getByText('Chess')).toBeInTheDocument();  // ❌ Never appears
});
```

**Actual Behavior**:
```
<select>
  <option>Nessun gioco disponibile</option>  // ❌ games=[]
</select>
```

**Root Cause**: ChatProvider never calls `api.get('/api/v1/games')`

### Blocker 2: sendMessage Doesn't Trigger AI

**Test Expectation**:
- User types question
- Click send
- AI streams response
- Response includes followUpQuestions
- FollowUpQuestions renders

**Actual Behavior**:
```typescript
const sendMessage = async (content: string) => {
  // ... add user message
  // Note: Streaming integration will be added in future enhancement
  // ❌ No AI call, no assistant message, no followUpQuestions
};
```

### Blocker 3: Test Approach Invalid

The tests use **end-to-end integration testing** approach:
- Mock all HTTP endpoints
- Render full component tree
- Simulate user interactions
- Assert on final state

This doesn't work with the new architecture because:
1. Too many layers of indirection (Page → Provider → Sidebar/Content → Selectors/List → Message)
2. ChatProvider incomplete (missing data loading)
3. useChatStreaming not integrated
4. Mock complexity exponentially harder with context-based state

**Better approach**: **Component testing with mocked context**

```typescript
// Instead of mocking HTTP and rendering full tree
render(<ChatPage />);

// Mock the context directly
const mockContext = {
  messages: [
    {
      id: 'msg-1',
      role: 'assistant',
      content: 'Answer',
      followUpQuestions: ['Q1', 'Q2']
    }
  ],
  setInputValue: jest.fn(),
  loading: { sending: false }
};

render(
  <ChatContext.Provider value={mockContext}>
    <Message message={mockContext.messages[0]} isUser={false} />
  </ChatContext.Provider>
);

// ✅ Direct, simple, fast
expect(screen.getByTestId('follow-up-questions')).toBeInTheDocument();
```

---

## Recommended Solutions

### Option 1: Quick Fix - Re-Skip with Documentation (30 min)

**Action**:
1. Re-add `.skip` to CHAT-02 tests
2. Update comments with findings from this analysis
3. Create follow-up ticket for proper fix

**Pros**:
- Honest about current state
- Doesn't block other work
- Clear documentation for future work

**Cons**:
- Tests remain skipped
- Feature partially broken (UI works but no data flows)

### Option 2: Complete Feature Integration (8-12 hours)

**Phase 1: Complete ChatProvider** (4 hours)
- Add game loading useEffect
- Add agent loading when game selected
- Add chat loading when agent selected
- Wire useChatStreaming into sendMessage
- Handle followUpQuestions in onComplete callback

**Phase 2: Rewrite Tests** (4 hours)
- Create unit tests for Message component with mocked context
- Create integration tests for ChatProvider with mocked API
- Remove end-to-end approach
- Add proper async handling with waitFor

**Phase 3: Validation** (2 hours)
- Run all tests
- Fix act() warnings
- Verify feature works end-to-end manually
- Update documentation

**Pros**:
- Feature fully working
- Tests passing
- Proper architecture

**Cons**:
- Significant time investment
- Goes beyond "fixing tests" into "implementing features"
- May introduce new issues requiring debugging

### Option 3: Hybrid - Component Tests Only (3-4 hours)

**Action**:
1. Keep feature integration done (Message component) ✅
2. Rewrite tests as **component tests** with mocked context
3. Skip or delete end-to-end tests
4. Document that full integration needs separate work

**Pros**:
- Tests pass (verify FollowUpQuestions renders correctly)
- Reasonable scope (testing, not feature dev)
- Clear separation of concerns

**Cons**:
- Doesn't test full end-to-end flow
- ChatProvider still incomplete

---

## Recommendation: **Option 3 (Hybrid)**

**Rationale**:
- Quality Engineer role: Focus on testing, not feature implementation
- Pragmatic: Tests what's currently working (Message + FollowUpQuestions)
- Honest: Documents what's not yet integrated (ChatProvider streaming)
- Actionable: Creates clear ticket for feature completion

**Next Steps**:
1. Rewrite CHAT-02 tests as component tests (3-4 hours)
2. Create ticket: "Complete CHAT-02 Integration in ChatProvider"
3. Document testing approach for future features

---

## Files Modified

1. `apps/web/src/components/chat/Message.tsx`
   - Added FollowUpQuestions import and rendering
   - Added handleFollowUpClick handler
   - Integrated with ChatProvider's setInputValue

2. `apps/web/src/__tests__/pages/chat.supplementary.test.tsx`
   - Fixed mock message data (added `role` field)
   - Removed `.skip` from CHAT-02 describe block
   - Updated comments

3. `docs/issue/chat-02-test-fixing-analysis.md` (this file)
   - Comprehensive analysis and recommendations

---

## Conclusion

The CHAT-02 test suite was skipped because:
1. Feature was removed during chat page refactoring
2. Tests were written for old monolithic architecture
3. New ChatProvider architecture is incomplete

**Work completed**:
- ✅ Re-integrated FollowUpQuestions into Message component
- ✅ Fixed mock data structure
- ✅ Removed `.skip`

**Remaining work**:
- ❌ ChatProvider needs game/agent loading logic
- ❌ sendMessage needs useChatStreaming integration
- ❌ Tests need rewrite as component tests, not end-to-end tests

**Recommended next steps**: Option 3 (Hybrid) - Rewrite as component tests

**Time estimate for completion**: 3-4 hours for component tests + 8-12 hours for full feature integration

