# [REFACTOR] Unify Login Flow and Remove Duplication

## 🎯 Objective

Consolidate the fragmented authentication UI scattered across `index.tsx` and `login.tsx` into a single, reusable `<AuthModal>` component.

## 📋 Current State

**Problem**: Authentication logic is duplicated in multiple places:

1. **Landing page** (`pages/index.tsx`): Lines 400-548 contain inline auth modal with login/register forms
2. **Login page** (`pages/login.tsx`): Separate login page with placeholder text and OAuth only
3. **ChatProvider**: Separate `loadCurrentUser()` logic

This creates:
- 🐛 Inconsistent UX (different flows for same action)
- 🧹 Code duplication (~200 lines)
- 🔧 Maintenance overhead (updates in 2-3 places)
- 😕 User confusion (why two login paths?)

## ✅ Acceptance Criteria

- [ ] Create reusable `<AuthModal>` component in `components/auth/`
- [ ] Modal supports both login and register tabs
- [ ] Modal includes OAuth buttons (Google, Discord, GitHub)
- [ ] Remove auth modal code from `index.tsx` (lines 400-548)
- [ ] Update `login.tsx` to use `<AuthModal>` or redirect to landing page
- [ ] Create custom `useAuth()` hook to centralize auth logic
- [ ] All auth flows use the same component
- [ ] Session expired warning preserved (from login.tsx)
- [ ] Demo credentials helper preserved
- [ ] Tests pass (auth-related E2E tests)
- [ ] Accessibility: Focus management, keyboard navigation, ARIA labels

## 🏗️ Implementation Plan

### 1. Create `useAuth()` Hook

**File**: `apps/web/src/hooks/useAuth.ts`

```tsx
export function useAuth() {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string>('');

  const login = async (email: string, password: string) => { ... };
  const register = async (data: RegisterData) => { ... };
  const logout = async () => { ... };
  const loadCurrentUser = async () => { ... };

  useEffect(() => {
    void loadCurrentUser();
  }, []);

  return { user, loading, error, login, register, logout };
}
```

### 2. Create `<AuthModal>` Component

**File**: `apps/web/src/components/auth/AuthModal.tsx`

```tsx
interface AuthModalProps {
  isOpen: boolean;
  onClose: () => void;
  defaultMode?: 'login' | 'register';
  onSuccess?: (user: AuthUser) => void;
  showDemoCredentials?: boolean;
  sessionExpiredMessage?: boolean;
}

export function AuthModal({
  isOpen,
  onClose,
  defaultMode = 'login',
  ...
}: AuthModalProps) {
  const [mode, setMode] = useState(defaultMode);
  const { login, register, error, loading } = useAuth();

  return (
    <AccessibleModal isOpen={isOpen} onClose={onClose}>
      {/* Tab navigation */}
      <Tabs value={mode} onValueChange={setMode}>
        <TabsList>
          <TabsTrigger value="login">Login</TabsTrigger>
          <TabsTrigger value="register">Register</TabsTrigger>
        </TabsList>

        <TabsContent value="login">
          <LoginForm onSubmit={login} loading={loading} error={error} />
        </TabsContent>

        <TabsContent value="register">
          <RegisterForm onSubmit={register} loading={loading} error={error} />
        </TabsContent>
      </Tabs>

      <Separator />

      <OAuthButtons />

      {showDemoCredentials && <DemoCredentialsHint />}
    </AccessibleModal>
  );
}
```

### 3. Extract Form Components

**Files**:
- `apps/web/src/components/auth/LoginForm.tsx`
- `apps/web/src/components/auth/RegisterForm.tsx`

Use React Hook Form + Zod for validation:

```tsx
const loginSchema = z.object({
  email: z.string().email('Invalid email address'),
  password: z.string().min(8, 'Password must be at least 8 characters'),
});

export function LoginForm({ onSubmit, loading, error }: LoginFormProps) {
  const form = useForm<LoginFormData>({
    resolver: zodResolver(loginSchema),
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-4">
      <FormField
        control={form.control}
        name="email"
        render={({ field }) => (
          <FormItem>
            <FormLabel>Email</FormLabel>
            <FormControl>
              <Input type="email" placeholder="you@example.com" {...field} />
            </FormControl>
            <FormMessage />
          </FormItem>
        )}
      />

      {/* Password field */}

      <LoadingButton type="submit" isLoading={loading} className="w-full">
        Login
      </LoadingButton>

      {error && <ErrorMessage>{error}</ErrorMessage>}
    </form>
  );
}
```

### 4. Update Landing Page

**File**: `apps/web/src/pages/index.tsx`

```tsx
// Before: ~550 lines with inline auth modal
// After: ~400 lines using <AuthModal>

export default function Home() {
  const [showAuthModal, setShowAuthModal] = useState(false);
  const { user } = useAuth();

  return (
    <>
      {/* Page content */}

      <AuthModal
        isOpen={showAuthModal}
        onClose={() => setShowAuthModal(false)}
        onSuccess={(user) => {
          setShowAuthModal(false);
          router.push('/chat');
        }}
        showDemoCredentials={!user}
      />
    </>
  );
}
```

### 5. Update Login Page

**File**: `apps/web/src/pages/login.tsx`

Option A (Recommended): Use `<AuthModal>` as page content
Option B: Redirect to landing page with `?auth=login` param

```tsx
// Option A
export default function LoginPage() {
  const router = useRouter();
  const { reason } = router.query;

  return (
    <div className="min-h-screen flex items-center justify-center">
      <Card className="w-full max-w-md">
        {reason === 'session_expired' && (
          <Alert variant="warning" className="mb-4">
            <AlertTitle>Session Expired</AlertTitle>
            <AlertDescription>
              Your session has expired. Please log in again.
            </AlertDescription>
          </Alert>
        )}

        <AuthModal
          isOpen={true}
          onClose={() => router.push('/')}
          onSuccess={() => router.push('/chat')}
          sessionExpiredMessage={reason === 'session_expired'}
        />
      </Card>
    </div>
  );
}
```

### 6. Update ChatProvider

Remove duplicate auth logic, use `useAuth()` hook instead.

## 🧪 Testing

### Unit Tests

```tsx
// apps/web/src/__tests__/components/auth/AuthModal.test.tsx
describe('AuthModal', () => {
  it('renders login form by default', () => {});
  it('switches between login and register tabs', () => {});
  it('shows OAuth buttons', () => {});
  it('calls onSuccess after successful login', () => {});
  it('displays error message on failed auth', () => {});
  it('shows demo credentials when enabled', () => {});
});
```

### E2E Tests

Update existing auth tests:
- `e2e/demo-user-login.spec.ts` - should still pass
- `e2e/auth-oauth-buttons.spec.ts` - should still pass
- `e2e/auth-2fa-complete.spec.ts` - should still pass

## 📦 Files to Create/Modify

**New Files** (7):
- `apps/web/src/hooks/useAuth.ts`
- `apps/web/src/components/auth/AuthModal.tsx`
- `apps/web/src/components/auth/LoginForm.tsx`
- `apps/web/src/components/auth/RegisterForm.tsx`
- `apps/web/src/components/auth/DemoCredentialsHint.tsx`
- `apps/web/src/__tests__/hooks/useAuth.test.ts`
- `apps/web/src/__tests__/components/auth/AuthModal.test.tsx`

**Modified Files** (3):
- `apps/web/src/pages/index.tsx` (remove 148 lines, add 10 lines)
- `apps/web/src/pages/login.tsx` (simplify, use AuthModal)
- `apps/web/src/components/chat/ChatProvider.tsx` (use useAuth hook)

## 📊 Impact

**Lines of Code**:
- Removed: ~200 lines (duplicated logic)
- Added: ~300 lines (reusable components)
- Net change: +100 lines (but much better organized)

**Bundle Size**:
- Estimated reduction: ~2-3 KB (tree-shaking duplicate code)

**Maintainability**:
- Auth logic in ONE place instead of 3
- Form validation centralized
- Easier to add OAuth providers

## ⏱️ Effort Estimate

**4 hours** (half day)

- Setup: 30min
- useAuth hook: 1h
- AuthModal + Forms: 1.5h
- Integration + cleanup: 45min
- Testing: 45min

## 📚 Dependencies

- React Hook Form (already installed)
- Zod (already installed)
- Shadcn/UI Tabs component (may need to install)

## 🔗 Related Issues

- Design System (#TBD) - Uses new form components
- Accessibility Audit (#TBD) - Focus management

## 📝 Notes

- Keep OAuth buttons as separate component (already exists)
- Preserve session timeout detection from current login.tsx
- Consider adding "Remember me" option (future enhancement)
- Add rate limiting feedback (future enhancement)

---

**Priority**: 🔴 Critical
**Sprint**: Sprint 1
**Effort**: 4h
**Labels**: `frontend`, `refactor`, `auth`, `ux`, `sprint-1`
