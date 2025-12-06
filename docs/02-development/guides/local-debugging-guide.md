# Local Debugging Guide

**MeepleAI Monorepo** - Comprehensive guide to debugging backend (.NET), frontend (Next.js/React), and full-stack scenarios using Visual Studio Code.

**Last Updated**: 2025-11-19
**Audience**: Developers
**Prerequisites**: VSCode, .NET 9 SDK, Node.js 20+, pnpm

---

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Quick Start](#quick-start)
4. [Backend Debugging (.NET)](#backend-debugging-net)
5. [Frontend Debugging (Next.js/React)](#frontend-debugging-nextjsreact)
6. [Full Stack Debugging](#full-stack-debugging)
7. [Test Debugging](#test-debugging)
8. [Python Services Debugging](#python-services-debugging)
9. [Advanced Debugging Techniques](#advanced-debugging-techniques)
10. [Troubleshooting](#troubleshooting)

---

## Overview

MeepleAI uses Visual Studio Code with pre-configured debugging setups for:

- **.NET API Backend**: C# ASP.NET 9 with CQRS architecture
- **Next.js Frontend**: React 19 with Server/Client Components
- **Python Services**: Embedding & Unstructured services (FastAPI)
- **E2E Tests**: Playwright debugging
- **Full Stack**: Compound configurations for simultaneous debugging

**Key Files**:
- `.vscode/launch.json` - Debug configurations
- `.vscode/tasks.json` - Build and utility tasks
- `.vscode/settings.json` - Workspace settings
- `.vscode/extensions.json` - Recommended extensions

---

## Prerequisites

### Required VSCode Extensions

Install all recommended extensions when prompted, or manually:

```bash
# View recommendations
code --list-extensions

# Install key extensions
code --install-extension ms-dotnettools.csharp
code --install-extension ms-dotnettools.csdevkit
code --install-extension dbaeumer.vscode-eslint
code --install-extension esbenp.prettier-vscode
code --install-extension bradlc.vscode-tailwindcss
code --install-extension ms-python.python
code --install-extension ms-azuretools.vscode-docker
```

**Required Extensions**:
- **C# Dev Kit** (`ms-dotnettools.csdevkit`) - .NET debugging
- **ESLint** (`dbaeumer.vscode-eslint`) - TypeScript linting
- **Prettier** (`esbenp.prettier-vscode`) - Code formatting
- **Tailwind CSS IntelliSense** (`bradlc.vscode-tailwindcss`) - Styling
- **Python** (`ms-python.python`) - Python services debugging
- **Docker** (`ms-azuretools.vscode-docker`) - Container management

### System Requirements

```bash
# .NET SDK
dotnet --version  # Should be 9.0+

# Node.js & pnpm
node --version    # Should be 20+
pnpm --version    # Should be 8+

# Docker (for services)
docker --version
docker compose version
```

### Infrastructure Services

Start required services before debugging:

```bash
cd infra
docker compose up -d meepleai-postgres meepleai-qdrant meepleai-redis meepleai-seq
```

**Health Check**:
```bash
# PostgreSQL
docker exec meepleai-postgres pg_isready

# Qdrant
curl http://localhost:6333/healthz

# Redis
docker exec meepleai-redis redis-cli ping

# HyperDX (Logs)
curl http://localhost:8180/
```

---

## Quick Start

### 1. Open Workspace in VSCode

```bash
cd /path/to/meepleai-monorepo
code .
```

### 2. Select Debug Configuration

Press `F5` or click **Run and Debug** (Ctrl+Shift+D) and select:

- **`.NET API: Launch`** - Debug backend only
- **`Next.js: debug full stack`** - Debug frontend only
- **`Full Stack: API + Frontend`** - Debug both simultaneously

### 3. Set Breakpoints

- Click in the gutter (left of line numbers) to add a red dot
- Breakpoints pause execution when hit
- Use conditional breakpoints (right-click breakpoint) for specific scenarios

### 4. Start Debugging

- Press `F5` to start
- Press `F10` to step over
- Press `F11` to step into
- Press `Shift+F11` to step out
- Press `F5` to continue

---

## Backend Debugging (.NET)

### Launch Configuration

**File**: `.vscode/launch.json`

```json
{
  "name": ".NET API: Launch",
  "type": "coreclr",
  "request": "launch",
  "preLaunchTask": "build-api",
  "program": "${workspaceFolder}/apps/api/src/Api/bin/Debug/net9.0/MeepleAI.Api.dll",
  "cwd": "${workspaceFolder}/apps/api/src/Api",
  "env": {
    "ASPNETCORE_ENVIRONMENT": "Development",
    "ASPNETCORE_URLS": "http://localhost:8080"
  }
}
```

### Step-by-Step: Debug a Command Handler

**Example**: Debug `LoginCommand` handler in Authentication context.

#### 1. Locate the Handler

```bash
# File location
apps/api/src/Api/BoundedContexts/Authentication/Application/Handlers/LoginCommandHandler.cs
```

#### 2. Set Breakpoints

Open `LoginCommandHandler.cs` and click in the gutter next to:

```csharp
public async Task<Result<LoginResponse>> Handle(
    LoginCommand command,
    CancellationToken cancellationToken)
{
    // Set breakpoint here (line ~30)
    var user = await _authRepository.GetUserByEmailAsync(command.Email);

    // Set breakpoint here (line ~35)
    if (user is null || !_passwordHasher.VerifyPassword(command.Password, user.PasswordHash))
    {
        return Result<LoginResponse>.Failure("Invalid credentials");
    }

    // ... rest of handler
}
```

#### 3. Start Debugging

1. Press `F5` or select **`.NET API: Launch`**
2. Wait for "Now listening on: http://localhost:8080"
3. VSCode will auto-open browser or attach debugger

#### 4. Trigger the Endpoint

Use any HTTP client (curl, Postman, or frontend):

```bash
curl -X POST http://localhost:8080/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email": "admin-test@example.com", "password": "TestPassword123!"}'
```

#### 5. Inspect Variables

When breakpoint hits:
- Hover over variables to see values
- Use **Variables** panel (left sidebar)
- Use **Watch** panel to monitor expressions
- Use **Call Stack** to see execution path

**Variables Panel**:
```
▼ Locals
  ► command: LoginCommand
    - Email: "admin-test@example.com"
    - Password: "TestPassword123!"
  ► user: User
    - Id: 1
    - Email: "admin-test@example.com"
    - Role: "Admin"
```

#### 6. Step Through Code

- `F10` (Step Over): Execute current line, don't enter methods
- `F11` (Step Into): Enter method calls
- `Shift+F11` (Step Out): Exit current method
- `F5` (Continue): Run until next breakpoint

#### 7. Use Debug Console

At the bottom, open **Debug Console** and type:

```csharp
// Evaluate expressions
command.Email
user.PasswordHash
_passwordHasher.VerifyPassword(command.Password, user.PasswordHash)

// Check null safety
user?.Email ?? "No user"
```

### Common Debugging Scenarios

#### Debugging CQRS Pipeline

**MediatR Handlers**: Set breakpoints in:
1. **Command/Query class** (`LoginCommand.cs`)
2. **Handler** (`LoginCommandHandler.cs`)
3. **Domain Services** (e.g., `AuthenticationDomainService.cs`)
4. **Repository** (e.g., `AuthenticationRepository.cs`)

**Pipeline Behaviors**: Debug custom MediatR behaviors:
```csharp
// File: apps/api/src/Api/Infrastructure/Behaviors/ValidationBehavior.cs
public async Task<TResponse> Handle(
    TRequest request,
    RequestHandlerDelegate<TResponse> next,
    CancellationToken cancellationToken)
{
    // Set breakpoint here to debug validation
    var validationResults = await Task.WhenAll(
        _validators.Select(v => v.ValidateAsync(request, cancellationToken)));

    // ...
}
```

#### Debugging Database Queries

**Entity Framework Core**:

```csharp
// Enable sensitive data logging (appsettings.Development.json)
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Watch SQL in Debug Console**:
- Check **HyperDX** (http://localhost:8180) for real-time logs
- Use **Debug Console** to inspect `DbContext.ChangeTracker`

```csharp
// In Debug Console
_dbContext.ChangeTracker.Entries().Select(e => e.Entity)
```

#### Debugging Async Operations

**Common Issues**:
- Deadlocks: Use `.ConfigureAwait(false)` or don't block async
- Task cancellation: Monitor `CancellationToken`

```csharp
// Set conditional breakpoint
// Right-click breakpoint → Edit Breakpoint → Condition
cancellationToken.IsCancellationRequested == true
```

### Attach to Running Process

If API is already running:

1. Select **`.NET API: Attach`**
2. Choose `MeepleAI.Api` process from list
3. Set breakpoints and trigger endpoint

---

## Frontend Debugging (Next.js/React)

### Launch Configurations

**Three modes available**:

1. **Server-side debugging** (RSC, API routes, SSR)
2. **Client-side debugging** (React components in browser)
3. **Full stack debugging** (both simultaneously)

### Debug Server-Side (RSC/SSR)

**Configuration**: `Next.js: debug server-side`

```json
{
  "name": "Next.js: debug server-side",
  "type": "node-terminal",
  "request": "launch",
  "command": "pnpm dev",
  "cwd": "${workspaceFolder}/apps/web",
  "serverReadyAction": {
    "pattern": "- Local:.+(https?://.+)",
    "uriFormat": "%s",
    "action": "debugWithChrome"
  }
}
```

#### Step-by-Step: Debug Server Component

**Example**: Debug a Server Component in `apps/web/src/app/games/page.tsx`

#### 1. Open Server Component

```typescript
// apps/web/src/app/games/page.tsx
import { getGames } from '@/lib/api/games'

export default async function GamesPage() {
  // Set breakpoint here
  const games = await getGames()

  // Set breakpoint here to inspect data
  console.log('Games fetched:', games)

  return (
    <div>
      {games.map(game => (
        <GameCard key={game.id} game={game} />
      ))}
    </div>
  )
}
```

#### 2. Start Debugging

1. Select **`Next.js: debug server-side`**
2. Press `F5`
3. Browser opens automatically at http://localhost:3000

#### 3. Navigate to Page

- Navigate to http://localhost:3000/games
- Breakpoint in server component hits **before** page renders
- Inspect `games` data in Variables panel

#### 4. Debug API Client

Server Components often call API clients:

```typescript
// apps/web/src/lib/api/games.ts
export async function getGames(): Promise<Game[]> {
  // Set breakpoint here
  const response = await httpClient.get<Game[]>('/api/v1/games')

  // Set breakpoint here to inspect response
  return response.data
}
```

**Inspect**:
- Network requests (see terminal output)
- Response data structure
- Error handling

### Debug Client-Side (React Components)

**Configuration**: `Next.js: debug client-side`

```json
{
  "name": "Next.js: debug client-side",
  "type": "chrome",
  "request": "launch",
  "url": "http://localhost:3000",
  "webRoot": "${workspaceFolder}/apps/web"
}
```

#### Step-by-Step: Debug Client Component

**Example**: Debug `ChatInterface` component with hooks.

#### 1. Ensure Dev Server is Running

```bash
cd apps/web
pnpm dev
```

#### 2. Open Client Component

```typescript
// apps/web/src/components/chat/ChatInterface.tsx
'use client'

import { useState, useEffect } from 'react'
import { useChatClient } from '@/lib/api/chat'

export function ChatInterface() {
  const [messages, setMessages] = useState([])
  const { sendMessage } = useChatClient()

  const handleSend = async (text: string) => {
    // Set breakpoint here
    const response = await sendMessage(text)

    // Set breakpoint here to inspect response
    setMessages(prev => [...prev, response])
  }

  // Set breakpoint in useEffect
  useEffect(() => {
    console.log('Messages updated:', messages)
  }, [messages])

  return (
    <div>
      {/* UI */}
    </div>
  )
}
```

#### 3. Start Client Debugging

1. Select **`Next.js: debug client-side`**
2. Press `F5`
3. Chrome opens with debugger attached

#### 4. Interact with UI

- Click/type in chat interface
- Breakpoints in event handlers hit
- Use **Sources** tab in Chrome DevTools (also synced with VSCode)

#### 5. Debug React Hooks

**Common scenarios**:

```typescript
// Debug useState updates
const [state, setState] = useState(initialValue)
// Set breakpoint on setState calls

// Debug useEffect dependencies
useEffect(() => {
  // Set breakpoint here
  // Check why effect ran
}, [dep1, dep2])

// Debug custom hooks
const { data, loading, error } = useCustomHook()
// Set breakpoint inside custom hook implementation
```

### Debug Full Stack (Server + Client)

**Configuration**: `Next.js: debug full stack`

Combines server-side and client-side debugging:

1. Select **`Next.js: debug full stack`**
2. Press `F5`
3. Both server (Node) and client (Chrome) debuggers attach
4. Breakpoints work in both Server Components and Client Components

**Use Cases**:
- Debug data flow from Server Component → Client Component
- Debug API route handlers (in `apps/web/src/app/api/...`)
- Debug form actions (Server Actions)

### Debug Server Actions

**Example**: Debug form submission with Server Action.

```typescript
// apps/web/src/app/settings/actions.ts
'use server'

import { revalidatePath } from 'next/cache'

export async function updateProfile(formData: FormData) {
  // Set breakpoint here
  const name = formData.get('name')
  const email = formData.get('email')

  // Set breakpoint here
  const response = await fetch('http://localhost:8080/api/v1/users/profile', {
    method: 'PUT',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name, email }),
  })

  // Set breakpoint here to check response
  if (!response.ok) {
    throw new Error('Update failed')
  }

  revalidatePath('/settings')
  return { success: true }
}
```

**Debug Flow**:
1. User submits form in Client Component
2. Server Action executes (breakpoint in `updateProfile`)
3. API request sent to backend (breakpoint in backend handler)
4. Response processed (breakpoint in Server Action)
5. Cache revalidated and page updates

### Debug React Context & State

**Example**: Debug auth context.

```typescript
// apps/web/src/contexts/AuthContext.tsx
'use client'

import { createContext, useContext, useState, useEffect } from 'react'

export const AuthContext = createContext<AuthContextType | null>(null)

export function AuthProvider({ children }: { children: React.ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    // Set breakpoint here
    async function loadUser() {
      try {
        const response = await fetch('/api/v1/auth/me')
        // Set breakpoint here
        const userData = await response.json()
        setUser(userData)
      } catch (error) {
        // Set breakpoint here for error handling
        console.error('Auth check failed:', error)
      } finally {
        setLoading(false)
      }
    }

    loadUser()
  }, [])

  return (
    <AuthContext.Provider value={{ user, loading }}>
      {children}
    </AuthContext.Provider>
  )
}
```

**Inspect**:
- Context value propagation
- Provider re-renders
- Consumer hook calls (`useContext(AuthContext)`)

---

## Full Stack Debugging

### Debug API + Frontend Together

**Configuration**: `Full Stack: API + Frontend`

```json
{
  "name": "Full Stack: API + Frontend",
  "type": "compound",
  "configurations": [
    ".NET API: Launch",
    "Next.js: debug server-side"
  ]
}
```

#### Step-by-Step: Full Request Flow

**Scenario**: Debug complete flow from React component → API endpoint → database.

#### 1. Set Breakpoints in All Layers

**Frontend** (`apps/web/src/lib/api/games.ts`):
```typescript
export async function createGame(data: CreateGameDto): Promise<Game> {
  // Breakpoint 1: Frontend API client
  const response = await httpClient.post<Game>('/api/v1/games', data)
  return response.data
}
```

**Backend Endpoint** (`apps/api/src/Api/Routing/GameEndpoints.cs`):
```csharp
public static async Task<Results<Ok<GameDto>, BadRequest>> CreateGame(
    [FromBody] CreateGameRequest request,
    IMediator mediator)
{
    // Breakpoint 2: API endpoint
    var command = new CreateGameCommand(request.Name, request.MinPlayers, /* ... */);
    var result = await mediator.Send(command);
    // ...
}
```

**Command Handler** (`apps/api/src/Api/BoundedContexts/GameManagement/Application/Handlers/CreateGameCommandHandler.cs`):
```csharp
public async Task<Result<GameDto>> Handle(
    CreateGameCommand command,
    CancellationToken cancellationToken)
{
    // Breakpoint 3: Command handler
    var game = Game.Create(command.Name, command.MinPlayers, /* ... */);

    // Breakpoint 4: Before save
    await _gameRepository.AddAsync(game);
    await _unitOfWork.CommitAsync(cancellationToken);

    return Result<GameDto>.Success(game.ToDto());
}
```

#### 2. Start Full Stack Debugging

1. Select **`Full Stack: API + Frontend`**
2. Press `F5`
3. Both backend and frontend debuggers start
4. Two debug sessions appear in **Call Stack** panel

#### 3. Trigger the Flow

In browser at http://localhost:3000:
- Navigate to Games page
- Click "Add Game" button
- Fill form and submit

#### 4. Follow Execution

Execution pauses at each breakpoint in order:
1. **Breakpoint 1**: Frontend sends HTTP POST
2. **Breakpoint 2**: Backend receives request
3. **Breakpoint 3**: Handler processes command
4. **Breakpoint 4**: Database operation
5. Returns to frontend with response

#### 5. Switch Between Debug Sessions

**Call Stack** panel shows:
```
▼ .NET API: Launch
  CreateGameCommandHandler.Handle() Line 32
  ► MediatR Pipeline
  ► GameEndpoints.CreateGame()

▼ Next.js: debug server-side
  createGame() games.ts:45
  ► handleSubmit()
  ► GameForm component
```

Click on any stack frame to switch context.

### Debug API Calls with Network Inspection

**VSCode + Chrome DevTools Integration**:

1. Open Chrome DevTools (F12)
2. **Network** tab shows API requests
3. Click request → **Preview** to see response
4. Correlate with backend breakpoints

**Tips**:
- Enable "Preserve log" in Network tab
- Filter by "Fetch/XHR" for API calls
- Check request headers for auth tokens

---

## Test Debugging

### Backend Unit Tests (.NET)

**Configuration**: Uses built-in .NET test runner.

#### Debug xUnit Tests

**Example**: Debug `LoginCommandHandlerTests.cs`

#### 1. Open Test File

```csharp
// apps/api/tests/Api.UnitTests/BoundedContexts/Authentication/LoginCommandHandlerTests.cs
public class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_ValidCredentials_ReturnsSuccess()
    {
        // Arrange
        var user = new User { Email = "test@test.com", PasswordHash = "hash" };
        _mockRepo.Setup(r => r.GetUserByEmailAsync(It.IsAny<string>()))
                 .ReturnsAsync(user);

        // Set breakpoint here
        var command = new LoginCommand("test@test.com", "password");

        // Act
        // Set breakpoint here
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        // Set breakpoint here
        Assert.True(result.IsSuccess);
    }
}
```

#### 2. Debug Single Test

**Method 1: Test Explorer**
1. Open **Testing** panel (Ctrl+Shift+T)
2. Expand test tree: `Api.UnitTests` → `Authentication` → `LoginCommandHandlerTests`
3. Right-click test → **Debug Test**

**Method 2: CodeLens**
1. Click "Debug Test" link above test method (appears inline)
2. Breakpoints in test and handler hit

**Method 3: Terminal**
```bash
cd apps/api
dotnet test --filter "FullyQualifiedName~LoginCommandHandlerTests.Handle_ValidCredentials_ReturnsSuccess"
```
Then attach debugger via **`.NET API: Attach`**.

#### 3. Debug All Tests in Class

```bash
dotnet test --filter "FullyQualifiedName~LoginCommandHandlerTests"
```

#### 4. Inspect Mocks

In **Debug Console**:
```csharp
// Check mock setup
_mockRepo.Setups

// Verify mock calls
_mockRepo.Invocations
```

### Frontend Unit Tests (Jest)

**Configuration**: Uses Jest extension.

#### Debug Jest Tests

**Example**: Debug `ChatInterface.test.tsx`

#### 1. Open Test File

```typescript
// apps/web/src/components/chat/__tests__/ChatInterface.test.tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { ChatInterface } from '../ChatInterface'

describe('ChatInterface', () => {
  it('sends message when form is submitted', async () => {
    // Set breakpoint here
    render(<ChatInterface />)

    const input = screen.getByRole('textbox')
    const button = screen.getByRole('button', { name: /send/i })

    // Set breakpoint here
    fireEvent.change(input, { target: { value: 'Hello' } })
    fireEvent.click(button)

    // Set breakpoint here
    await waitFor(() => {
      expect(screen.getByText('Hello')).toBeInTheDocument()
    })
  })
})
```

#### 2. Debug Test

**Method 1: Jest Extension**
1. Install **Jest** extension (`Orta.vscode-jest`)
2. Tests appear with inline "Debug" button
3. Click "Debug" button above test

**Method 2: Debug Console**
1. Open **JavaScript Debug Terminal** (Ctrl+Shift+P → "Create JavaScript Debug Terminal")
2. Run test:
```bash
cd apps/web
pnpm test ChatInterface.test.tsx
```

**Method 3: Launch Configuration**

Add to `.vscode/launch.json`:
```json
{
  "name": "Jest: Debug Current Test",
  "type": "node",
  "request": "launch",
  "program": "${workspaceFolder}/apps/web/node_modules/.bin/jest",
  "args": ["${fileBasename}", "--runInBand", "--no-cache"],
  "cwd": "${workspaceFolder}/apps/web",
  "console": "integratedTerminal",
  "internalConsoleOptions": "neverOpen"
}
```

#### 3. Debug Component Implementation

Set breakpoints in both test **and** component:

```typescript
// apps/web/src/components/chat/ChatInterface.tsx
export function ChatInterface() {
  const handleSend = async (text: string) => {
    // Breakpoint here - hit during test execution
    await sendMessage(text)
  }

  return <form onSubmit={handleSend}>...</form>
}
```

When test runs, debugger pauses in test, then in component.

### E2E Tests (Playwright)

**Configuration**: `Playwright: Debug E2E Tests`

```json
{
  "name": "Playwright: Debug E2E Tests",
  "type": "node",
  "request": "launch",
  "program": "${workspaceFolder}/apps/web/node_modules/.bin/playwright",
  "args": ["test", "--debug"],
  "cwd": "${workspaceFolder}/apps/web",
  "console": "integratedTerminal"
}
```

#### Step-by-Step: Debug E2E Test

**Example**: Debug login flow E2E test.

#### 1. Open E2E Test

```typescript
// apps/web/e2e/auth/login.spec.ts
import { test, expect } from '@playwright/test'

test('user can login with valid credentials', async ({ page }) => {
  // Set breakpoint here
  await page.goto('http://localhost:3000/login')

  // Set breakpoint here
  await page.fill('input[name="email"]', 'admin-test@example.com')
  await page.fill('input[name="password"]', 'TestPassword123!')

  // Set breakpoint here
  await page.click('button[type="submit"]')

  // Set breakpoint here - wait for navigation
  await expect(page).toHaveURL('http://localhost:3000/dashboard')
  await expect(page.getByText('Welcome, Admin')).toBeVisible()
})
```

#### 2. Start E2E Debugging

1. Select **`Playwright: Debug E2E Tests`**
2. Press `F5`
3. Playwright Inspector opens with browser

#### 3. Step Through Test

- Playwright Inspector shows current step
- Browser actions execute visually
- Pause at breakpoints to inspect page state

#### 4. Inspect Page in Debug Console

```typescript
// Evaluate in Debug Console
await page.locator('input[name="email"]').inputValue()
await page.title()
await page.screenshot({ path: 'debug.png' })
```

#### 5. Debug with UI Mode

**Alternative**: Use Playwright UI for interactive debugging:

```bash
cd apps/web
pnpm playwright test --ui
```

---

## Python Services Debugging

### Embedding Service

**Configuration**: `Python: Embedding Service`

```json
{
  "name": "Python: Embedding Service",
  "type": "python",
  "request": "launch",
  "module": "uvicorn",
  "args": ["main:app", "--reload", "--port", "8000"],
  "cwd": "${workspaceFolder}/apps/embedding-service",
  "env": {
    "PYTHONPATH": "${workspaceFolder}/apps/embedding-service"
  }
}
```

#### Step-by-Step: Debug Embedding Endpoint

#### 1. Open Python File

```python
# apps/embedding-service/main.py
from fastapi import FastAPI, HTTPException
from pydantic import BaseModel

app = FastAPI()

@app.post("/embed")
async def create_embedding(request: EmbedRequest):
    # Set breakpoint here
    text = request.text

    # Set breakpoint here
    embedding = await model.encode(text)

    # Set breakpoint here - inspect result
    return {"embedding": embedding.tolist()}
```

#### 2. Start Python Debugging

1. Select **`Python: Embedding Service`**
2. Press `F5`
3. Service starts on http://localhost:8000

#### 3. Trigger Endpoint

```bash
curl -X POST http://localhost:8000/embed \
  -H "Content-Type: application/json" \
  -d '{"text": "How do I play Catan?"}'
```

#### 4. Inspect Variables

**Variables Panel**:
```
▼ Locals
  ► request: EmbedRequest
    - text: "How do I play Catan?"
  ► embedding: ndarray
    - shape: (384,)
    - dtype: float32
```

#### 5. Debug Console

```python
# Evaluate expressions
len(embedding)
embedding[:5]  # First 5 values
type(model)
```

---

## Advanced Debugging Techniques

### Conditional Breakpoints

**Use Case**: Only pause when specific condition is true.

#### Set Conditional Breakpoint

1. Right-click breakpoint (red dot)
2. Select **Edit Breakpoint** → **Expression**
3. Enter condition:

**Examples**:

```csharp
// C# - Only break for admin users
user.Role == "Admin"

// C# - Only break on errors
result.IsFailure

// C# - Only break for specific ID
command.GameId == 42
```

```typescript
// TypeScript - Only break for error responses
response.status !== 200

// TypeScript - Only break when array has items
messages.length > 0

// TypeScript - Only break for specific user
user?.id === '123'
```

### Logpoints (Non-breaking Logging)

**Use Case**: Log values without stopping execution.

#### Set Logpoint

1. Right-click in gutter (don't set breakpoint)
2. Select **Add Logpoint**
3. Enter message with expressions in curly braces:

**Examples**:

```
User {user.Email} attempted login
Game created with ID {game.Id}
API call took {stopwatch.ElapsedMilliseconds}ms
```

**Output**: Logs to **Debug Console** without pausing.

### Data Breakpoints (.NET only)

**Use Case**: Break when a specific property changes value.

#### Set Data Breakpoint

1. Hit a regular breakpoint
2. In **Variables** panel, right-click a property
3. Select **Break When Value Changes**

**Example**:
```csharp
// Break whenever user.Email changes
user.Email
```

### Hit Count Breakpoints

**Use Case**: Only break on Nth hit (e.g., skip first 10 iterations).

#### Set Hit Count Breakpoint

1. Right-click breakpoint
2. Select **Edit Breakpoint** → **Hit Count**
3. Enter condition:
   - `= 5` (break on 5th hit)
   - `> 10` (break after 10 hits)
   - `% 5 == 0` (break every 5th hit)

**Example**: Debug loop iteration #100:
```csharp
for (int i = 0; i < 1000; i++)
{
    // Breakpoint with hit count = 100
    ProcessItem(items[i]);
}
```

### Watch Expressions

**Use Case**: Monitor expressions across breakpoints.

#### Add to Watch

1. Open **Watch** panel (in debug sidebar)
2. Click **+** and enter expression:

**Examples**:

```csharp
// C# Watch expressions
user?.Email ?? "No user"
messages.Count
DateTime.Now
_dbContext.ChangeTracker.Entries().Count()
```

```typescript
// TypeScript Watch expressions
user?.email || 'Anonymous'
messages.length
new Date().toISOString()
Object.keys(state)
```

### Multi-Target Debugging

**Use Case**: Debug multiple processes simultaneously (e.g., API + Frontend + Python service).

#### Configuration

**Full Stack: Complete System** (already configured):

```json
{
  "name": "Full Stack: Complete System",
  "type": "compound",
  "configurations": [
    ".NET API: Launch",
    "Next.js: debug server-side",
    "Python: Embedding Service"
  ]
}
```

#### Usage

1. Select **`Full Stack: Complete System`**
2. Press `F5`
3. Three debug sessions start simultaneously
4. Switch between sessions in **Call Stack** panel

**Example**: Debug RAG pipeline across all services:
- Frontend: User submits question
- Backend: Processes query, calls embedding service
- Python: Generates embedding vector
- Backend: Retrieves from Qdrant, generates response
- Frontend: Displays answer

Set breakpoints in all layers and watch data flow.

### Remote Debugging (Docker Containers)

**Use Case**: Debug API running in Docker container.

#### Configuration

Add to `.vscode/launch.json`:

```json
{
  "name": ".NET API: Attach to Container",
  "type": "coreclr",
  "request": "attach",
  "processId": "${command:pickRemoteProcess}",
  "pipeTransport": {
    "pipeCwd": "${workspaceFolder}",
    "pipeProgram": "docker",
    "pipeArgs": ["exec", "-i", "meepleai-api"],
    "debuggerPath": "/vsdbg/vsdbg",
    "quoteArgs": false
  },
  "sourceFileMap": {
    "/app": "${workspaceFolder}/apps/api/src/Api"
  }
}
```

#### Setup

1. Install debugger in container (add to Dockerfile):
```dockerfile
# Development stage
RUN apt-get update && apt-get install -y unzip \
    && curl -sSL https://aka.ms/getvsdbgsh | /bin/sh /dev/stdin -v latest -l /vsdbg
```

2. Start container with debug enabled:
```bash
docker compose -f docker-compose.dev.yml up -d
```

3. Attach debugger via configuration

### Debugging Performance Issues

#### Profile CPU Usage

**C# Diagnostic Tools** (in Debug mode):
1. Start debugging (`F5`)
2. Open **Diagnostic Tools** window (Ctrl+Alt+F2)
3. View CPU usage graph
4. Identify hotspots during execution

#### Memory Profiling

**Check for Memory Leaks**:

```csharp
// Set breakpoint and inspect in Debug Console
GC.GetTotalMemory(false)
GC.CollectionCount(0)
GC.CollectionCount(1)
GC.CollectionCount(2)

// Check specific object allocations
_dbContext.ChangeTracker.Entries().Count()
```

**Frontend Memory** (Chrome DevTools):
1. Open DevTools → **Memory** tab
2. Take heap snapshot before/after action
3. Compare snapshots to find leaks

#### Trace HTTP Requests

**Backend** (HyperDX logs):
- Open http://localhost:8180
- Filter by request correlation ID
- View distributed trace

**Frontend** (Network tab):
- Open DevTools → **Network**
- Check request timing (TTFB, download time)
- Inspect request/response headers

---

## Troubleshooting

### Common Issues

#### 1. Breakpoints Not Hitting

**Symptoms**: Breakpoint shows hollow circle, never hits.

**Solutions**:

**C# (.NET)**:
```bash
# Rebuild in Debug mode
cd apps/api
dotnet clean
dotnet build --configuration Debug

# Check launch.json points to Debug build
"program": ".../bin/Debug/net9.0/MeepleAI.Api.dll"
```

**TypeScript (Next.js)**:
```bash
# Rebuild with source maps
cd apps/web
rm -rf .next
pnpm dev

# Check next.config.js enables source maps
module.exports = {
  productionBrowserSourceMaps: true,
}
```

**Verify**:
- File is saved (unsaved changes won't match)
- No syntax errors in file
- Debugger is attached (check **Call Stack** panel)

#### 2. "Cannot connect to runtime process" (.NET)

**Cause**: Process not running or wrong port.

**Solution**:
```bash
# Check API is running
curl http://localhost:8080/health

# Check no other process on port 8080
lsof -i :8080

# Kill conflicting process
kill -9 <PID>

# Restart debugging
```

#### 3. "Source map not found" (Next.js)

**Cause**: Source maps not generated or path mismatch.

**Solution**:

```bash
# Delete Next.js cache
rm -rf apps/web/.next

# Rebuild
cd apps/web
pnpm dev

# Check next.config.js
module.exports = {
  webpack: (config, { dev }) => {
    if (dev) {
      config.devtool = 'eval-source-map'
    }
    return config
  },
}
```

#### 4. Python Debugger Not Attaching

**Cause**: Python extension not installed or wrong interpreter.

**Solution**:

```bash
# Install Python extension
code --install-extension ms-python.python

# Select interpreter in VSCode
# Ctrl+Shift+P → "Python: Select Interpreter"
# Choose venv or system Python

# Verify Python version
python --version  # Should match project requirements
```

#### 5. Tests Fail Only in Debug Mode

**Cause**: Timing issues or debugger-specific behavior.

**Solution**:

**C#**:
```csharp
// Increase timeout for slow debugging
[Fact(Timeout = 60000)]  // 60 seconds
public async Task MyTest() { ... }
```

**TypeScript**:
```typescript
// Increase Jest timeout
jest.setTimeout(30000) // 30 seconds
```

#### 6. "Cannot launch program" (Permission Denied)

**Cause**: Binary not executable or locked.

**Solution**:

```bash
# Make binary executable
chmod +x apps/api/src/Api/bin/Debug/net9.0/MeepleAI.Api.dll

# Check for file locks
lsof | grep MeepleAI.Api

# Kill locking process
kill -9 <PID>
```

#### 7. Environment Variables Not Loaded

**Cause**: `.env` file not loaded or wrong location.

**Solution**:

**Backend** (check `launchSettings.json`):
```json
// apps/api/src/Api/Properties/launchSettings.json
{
  "profiles": {
    "MeepleAI.Api": {
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**Frontend** (check `.env.local`):
```bash
# apps/web/.env.local
NEXT_PUBLIC_API_BASE=http://localhost:8080
```

**Verify in Debug Console**:
```csharp
// C#
Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
```

```typescript
// TypeScript
process.env.NEXT_PUBLIC_API_BASE
```

### Debug Logging

#### Enable Verbose Logging

**Backend** (`appsettings.Development.json`):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information",
      "Microsoft.EntityFrameworkCore": "Information"
    }
  }
}
```

**Frontend** (Debug Console):
```typescript
// Enable verbose Next.js logging
DEBUG=* pnpm dev
```

#### Check HyperDX Logs

1. Open http://localhost:8180
2. Search for correlation ID or error message
3. View structured logs with request context

---

## Best Practices

### 1. Use Configuration Presets

- **Quick debugging**: `.NET API: Launch` or `Next.js: debug full stack`
- **Full investigation**: `Full Stack: Complete System`
- **Test debugging**: Use Test Explorer for focused debugging

### 2. Strategic Breakpoint Placement

**Good**:
- Entry points (handlers, controllers)
- Decision points (if/switch statements)
- Before/after external calls (DB, API)

**Avoid**:
- Inside tight loops (use conditional breakpoints)
- Getter/setter methods (clutters debugging)
- Framework internals (unless investigating bug)

### 3. Leverage Logpoints

- Use logpoints instead of `console.log` or `Debug.WriteLine`
- Cleaner code (no debug statements to remove)
- Faster iteration (no recompile needed)

### 4. Use Compound Configurations

- Save time by starting multiple services at once
- Pre-configured for common scenarios (API + Frontend)
- Customize for your workflow

### 5. Keep Source Maps Enabled

**Frontend**:
```typescript
// next.config.js - Always enable in development
module.exports = {
  webpack: (config, { dev }) => {
    if (dev) {
      config.devtool = 'eval-source-map'
    }
    return config
  },
}
```

**Backend**: Always build in Debug mode during development.

### 6. Debug Tests Frequently

- Debug test immediately when it fails
- Verify mocks and assertions
- Catch issues before committing

### 7. Learn Keyboard Shortcuts

| Action | Windows/Linux | macOS |
|--------|---------------|-------|
| Start Debugging | `F5` | `F5` |
| Step Over | `F10` | `F10` |
| Step Into | `F11` | `F11` |
| Step Out | `Shift+F11` | `Shift+F11` |
| Continue | `F5` | `F5` |
| Stop Debugging | `Shift+F5` | `Shift+F5` |
| Toggle Breakpoint | `F9` | `F9` |
| Debug Console | `Ctrl+Shift+Y` | `Cmd+Shift+Y` |

---

## Additional Resources

### Documentation

- **VSCode Debugging**: https://code.visualstudio.com/docs/editor/debugging
- **.NET Debugging**: https://learn.microsoft.com/en-us/visualstudio/debugger/
- **Next.js Debugging**: https://nextjs.org/docs/app/building-your-application/configuring/debugging
- **Playwright Debugging**: https://playwright.dev/docs/debug

### MeepleAI Docs

- [Testing Guide](../testing/testing-guide.md) - Unit, integration, E2E testing
- [Architecture Overview](../../01-architecture/overview/system-architecture.md)
- [API Specification](../../03-api/board-game-ai-api-specification.md)

### VSCode Extensions

- **Error Lens** (`usernamehw.errorlens`) - Inline error display
- **GitLens** (`eamodio.gitlens`) - Git integration
- **Thunder Client** (`rangav.vscode-thunder-client`) - API testing
- **REST Client** (`humao.rest-client`) - HTTP request testing

---

## Summary

**Quick Start**:
1. Install recommended extensions
2. Start infrastructure services (`docker compose up -d`)
3. Press `F5` and select debug configuration
4. Set breakpoints and trigger code path

**Key Points**:
- ✅ Use pre-configured launch configurations for common scenarios
- ✅ Debug full stack with compound configurations
- ✅ Use conditional breakpoints and logpoints for efficiency
- ✅ Leverage VSCode's integrated debugging for all layers
- ✅ Debug tests frequently to catch issues early

**Next Steps**:
- Explore [Testing Guide](../testing/testing-guide.md) for test-driven debugging workflows
- Read [Performance Testing Guide](../testing/performance-testing-guide.md) for profiling
- Check [Architecture Overview](../../01-architecture/overview/system-architecture.md) to understand system layers

---

**Feedback**: Found an issue or have suggestions? Open an issue on GitHub.

**Version**: 1.0
**Last Updated**: 2025-11-19
**Maintainer**: Engineering Team
