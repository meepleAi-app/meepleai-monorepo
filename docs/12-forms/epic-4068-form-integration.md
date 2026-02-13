# Epic #4068: Form Integration Patterns

**Integrating permission system with forms, validation, conditional fields**

---

## Permission-Aware Form Fields

### Pattern 1: Conditional Field Visibility

**Scenario**: Show fields only if user has permission

```tsx
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { usePermissions } from '@/contexts/PermissionContext';

// Schema varies by permission
function useGameFormSchema() {
  const { canAccess } = usePermissions();

  return z.object({
    title: z.string().min(1),
    publisher: z.string().optional(),

    // Advanced fields: Pro tier only
    ...(canAccess('game.advanced-fields') && {
      seoTitle: z.string().optional(),
      seoDescription: z.string().max(160).optional(),
      customSlug: z.string().regex(/^[a-z0-9-]+$/).optional()
    })
  });
}

function GameForm() {
  const { canAccess } = usePermissions();
  const schema = useGameFormSchema();
  const form = useForm({ resolver: zodResolver(schema) });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      {/* Basic fields: Always visible */}
      <input {...form.register('title')} />
      <input {...form.register('publisher')} />

      {/* Advanced fields: Pro tier only */}
      {canAccess('game.advanced-fields') && (
        <>
          <input {...form.register('seoTitle')} placeholder="SEO Title (Pro)" />
          <input {...form.register('seoDescription')} placeholder="SEO Description (Pro)" />
          <input {...form.register('customSlug')} placeholder="Custom URL Slug (Pro)" />
        </>
      )}

      <button type="submit">Save Game</button>
    </form>
  );
}
```

---

### Pattern 2: Tier-Locked Field with Upgrade Prompt

```tsx
function PublicationStateField() {
  const { canAccess } = usePermissions();
  const canPublish = canAccess('game.publish');

  return (
    <div>
      <label>Publication State</label>

      <select disabled={!canPublish}>
        <option value="draft">Draft</option>
        <option value="published" disabled={!canPublish}>
          Published {!canPublish && '(Pro tier required)'}
        </option>
      </select>

      {!canPublish && (
        <Alert variant="info" className="mt-2">
          <Lock className="h-4 w-4" />
          <p>Upgrade to Pro to publish games to the public catalog</p>
          <Button size="sm" onClick={() => router.push('/upgrade')}>
            Upgrade Now
          </Button>
        </Alert>
      )}
    </div>
  );
}
```

---

### Pattern 3: Dynamic Form Limits

**Scenario**: Max file uploads based on tier

```tsx
function PdfUploadForm() {
  const { tier, limits } = usePermissions();
  const [files, setFiles] = useState<File[]>([]);

  const maxFiles = {
    free: 1,
    normal: 5,
    pro: 20,
    enterprise: 100
  }[tier];

  const totalSizeMB = files.reduce((sum, f) => sum + f.size / 1024 / 1024, 0);
  const exceedsQuota = totalSizeMB > limits.storageQuotaMB;

  return (
    <form>
      <input
        type="file"
        multiple={tier !== 'free'} // Single file for Free tier
        accept=".pdf"
        onChange={(e) => {
          const selected = Array.from(e.target.files || []);

          if (selected.length > maxFiles) {
            toast.error(`Max ${maxFiles} files for ${tier} tier`);
            return;
          }

          setFiles(selected);
        }}
      />

      <p className="text-sm text-muted-foreground">
        {files.length} / {maxFiles} files selected ({tier} tier limit)
      </p>

      {totalSizeMB > 0 && (
        <p className={cn(
          'text-sm',
          exceedsQuota ? 'text-red-600' : 'text-muted-foreground'
        )}>
          Total size: {totalSizeMB.toFixed(1)}MB / {limits.storageQuotaMB}MB
          {exceedsQuota && ' ⚠️ Exceeds quota'}
        </p>
      )}

      <button
        type="submit"
        disabled={exceedsQuota}
      >
        Upload {files.length} PDF{files.length !== 1 && 's'}
      </button>
    </form>
  );
}
```

---

## Validation with Permissions

### Pattern 4: Server-Side Validation with Permission Check

```csharp
// FluentValidation: Permission-aware validators
public class CreateGameCommandValidator : AbstractValidator<CreateGameCommand>
{
    private readonly PermissionRegistry _permissionRegistry;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CreateGameCommandValidator(
        PermissionRegistry permissionRegistry,
        IHttpContextAccessor httpContextAccessor)
    {
        _permissionRegistry = permissionRegistry;
        _httpContextAccessor = httpContextAccessor;

        RuleFor(x => x.PublicationState)
            .Must(BeAllowedToPublish)
            .WithMessage("You must be a Creator or have Pro tier to publish games");

        RuleFor(x => x.CustomSlug)
            .Must((cmd, slug) => BeAllowedAdvancedFields(cmd))
            .When(x => !string.IsNullOrEmpty(x.CustomSlug))
            .WithMessage("Custom URL slugs require Pro tier");
    }

    private bool BeAllowedToPublish(GamePublicationState state)
    {
        if (state == GamePublicationState.Draft) return true; // Anyone can create drafts

        var userId = _httpContextAccessor.HttpContext.User.GetUserId();
        var user = /* fetch user */;
        var context = new PermissionContext(user.Tier, user.Role, user.Status);

        return _permissionRegistry.CheckAccess("game.publish", context).HasAccess;
    }

    private bool BeAllowedAdvancedFields(CreateGameCommand cmd)
    {
        var userId = _httpContextAccessor.HttpContext.User.GetUserId();
        var user = /* fetch user */;
        var context = new PermissionContext(user.Tier, user.Role, user.Status);

        return _permissionRegistry.CheckAccess("game.advanced-fields", context).HasAccess;
    }
}
```

---

### Pattern 5: Client-Side Validation with Zod + Permissions

```typescript
import { z } from 'zod';
import { usePermissions } from '@/contexts/PermissionContext';

function useGameFormSchema() {
  const { canAccess } = usePermissions();

  // Base schema (all users)
  let schema = z.object({
    title: z.string().min(1, 'Title required'),
    publisher: z.string().optional()
  });

  // Pro tier: Advanced validation
  if (canAccess('game.advanced-fields')) {
    schema = schema.extend({
      seoTitle: z.string().min(10).max(60),
      seoDescription: z.string().min(50).max(160),
      customSlug: z.string().regex(/^[a-z0-9-]+$/, 'Invalid slug format')
    });
  }

  // Creator role: Publication fields
  if (canAccess('game.publish')) {
    schema = schema.extend({
      publicationState: z.enum(['draft', 'published', 'archived']),
      publishedAt: z.string().datetime().optional()
    });
  }

  return schema;
}

// Usage
function GameForm() {
  const schema = useGameFormSchema();
  const form = useForm({
    resolver: zodResolver(schema)
  });

  // Schema automatically includes/excludes fields based on permissions
}
```

---

## Multi-Step Forms with Permission Gates

### Pattern 6: Wizard with Tier-Based Steps

```typescript
function CreateGameWizard() {
  const { hasTier, canAccess } = usePermissions();
  const [step, setStep] = useState(1);

  const steps = [
    { number: 1, title: 'Basic Info', component: BasicInfoStep, requiredTier: 'free' },
    { number: 2, title: 'Images & Media', component: MediaStep, requiredTier: 'free' },
    { number: 3, title: 'Advanced SEO', component: SeoStep, requiredTier: 'pro' },
    { number: 4, title: 'Publication', component: PublicationStep, permission: 'game.publish' }
  ];

  const accessibleSteps = steps.filter(s =>
    (s.requiredTier ? hasTier(s.requiredTier) : true) &&
    (s.permission ? canAccess(s.permission) : true)
  );

  const currentStepData = accessibleSteps.find(s => s.number === step);

  return (
    <div>
      {/* Stepper: Show only accessible steps */}
      <nav className="flex gap-2 mb-8">
        {accessibleSteps.map(s => (
          <button
            key={s.number}
            onClick={() => setStep(s.number)}
            className={cn(
              'px-4 py-2 rounded',
              step === s.number ? 'bg-primary text-primary-foreground' : 'bg-muted'
            )}
          >
            {s.number}. {s.title}
          </button>
        ))}
      </nav>

      {/* Current step */}
      {currentStepData && <currentStepData.component />}

      {/* Navigation */}
      <div className="flex justify-between mt-8">
        <button
          onClick={() => setStep(s => Math.max(1, s - 1))}
          disabled={step === accessibleSteps[0].number}
        >
          Previous
        </button>

        <button
          onClick={() => {
            const currentIndex = accessibleSteps.findIndex(s => s.number === step);
            const nextStep = accessibleSteps[currentIndex + 1];

            if (nextStep) {
              setStep(nextStep.number);
            } else {
              handleSubmit(); // Last accessible step
            }
          }}
        >
          {step === accessibleSteps[accessibleSteps.length - 1].number ? 'Submit' : 'Next'}
        </button>
      </div>

      {/* Show locked steps with upgrade prompts */}
      {steps.filter(s => !accessibleSteps.includes(s)).map(s => (
        <Alert key={s.number} variant="info">
          <Lock />
          <p>Step {s.number}: {s.title} requires {s.requiredTier} tier or {s.permission} permission</p>
          <Button size="sm" onClick={() => router.push('/upgrade')}>Unlock</Button>
        </Alert>
      ))}
    </div>
  );
}
```

---

## Form Submission with Permission Verification

### Pattern 7: Double-Check Permissions Before Submit

```typescript
function GameSubmitButton() {
  const { canAccess } = usePermissions();
  const form = useFormContext<GameFormData>();

  const handleSubmit = async (data: GameFormData) => {
    // Double-check permission before API call
    const permCheck = await checkPermission('game.create');

    if (!permCheck.hasAccess) {
      toast.error(`Cannot create game: ${permCheck.reason}`);
      return;
    }

    // Proceed with submission
    try {
      await createGame(data);
      toast.success('Game created!');
    } catch (error) {
      if (error.status === 403) {
        toast.error('Permission denied. Your tier may have changed.');
        // Invalidate cache, refetch permissions
        queryClient.invalidateQueries({ queryKey: ['permissions'] });
      } else {
        toast.error('Failed to create game');
      }
    }
  };

  return (
    <button
      type="submit"
      onClick={form.handleSubmit(handleSubmit)}
      disabled={!canAccess('game.create')}
    >
      Create Game
    </button>
  );
}
```

---

## Collection Management Forms

### Pattern 8: Add to Collection with Limit Validation

```tsx
function AddToCollectionForm({ gameId }: Props) {
  const { limits } = usePermissions();
  const { data: collection } = useCollectionQuery();

  const canAdd = collection.games.length < limits.maxGames;
  const remainingSlots = limits.maxGames - collection.games.length;

  const form = useForm({
    resolver: zodResolver(z.object({
      collectionId: z.string().uuid(),
      notes: z.string().max(500).optional()
    })),

    // Pre-submit validation
    onSubmit: async (data) => {
      if (!canAdd) {
        toast.error('Collection full. Remove games or upgrade tier.');
        return;
      }

      await addToCollection({ ...data, gameId });
    }
  });

  return (
    <form onSubmit={form.handleSubmit(form.onSubmit)}>
      <input {...form.register('notes')} placeholder="Notes (optional)" />

      {/* Limit indicator */}
      <div className="mt-2 p-3 bg-muted rounded">
        <p className="text-sm">
          Collection: {collection.games.length} / {limits.maxGames} games
        </p>
        <Progress value={(collection.games.length / limits.maxGames) * 100} />

        {!canAdd && (
          <Alert variant="destructive" className="mt-2">
            <p>Collection full ({limits.maxGames} games max for {tier} tier)</p>
            <Button size="sm" onClick={() => router.push('/upgrade')}>Upgrade</Button>
          </Alert>
        )}
      </div>

      <button type="submit" disabled={!canAdd}>
        Add to Collection ({remainingSlots} slots remaining)
      </button>
    </form>
  );
}
```

---

### Pattern 9: Bulk Form Actions with Permission Filtering

```tsx
function BulkGameActionForm() {
  const { canAccess } = usePermissions();
  const [selectedGames, setSelectedGames] = useState<string[]>([]);

  const availableActions = [
    { value: 'add-to-collection', label: 'Add to Collection', permission: 'collection.manage' },
    { value: 'export', label: 'Export as CSV', permission: 'export' },
    { value: 'archive', label: 'Archive', permission: 'game.archive' },
    { value: 'delete', label: 'Delete', permission: 'quick-action.delete', destructive: true }
  ].filter(action => canAccess(action.permission));

  const form = useForm({
    resolver: zodResolver(z.object({
      action: z.enum(['add-to-collection', 'export', 'archive', 'delete']),
      gameIds: z.array(z.string()).min(1, 'Select at least one game')
    }))
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <select {...form.register('action')}>
        <option value="">Select action...</option>
        {availableActions.map(action => (
          <option key={action.value} value={action.value} className={action.destructive ? 'text-red-600' : ''}>
            {action.label}
          </option>
        ))}
      </select>

      <p className="text-sm text-muted-foreground">
        {selectedGames.length} game{selectedGames.length !== 1 && 's'} selected
      </p>

      <button type="submit" disabled={selectedGames.length === 0}>
        Apply to {selectedGames.length} game{selectedGames.length !== 1 && 's'}
      </button>
    </form>
  );
}
```

---

## Agent Configuration Forms

### Pattern 10: Model Parameter Form with Tier-Based Options

```tsx
function AgentConfigForm() {
  const { hasTier } = usePermissions();

  // Model selection varies by tier
  const availableModels = [
    { value: 'gpt-3.5-turbo', label: 'GPT-3.5 Turbo', tier: 'free' },
    { value: 'gpt-4o-mini', label: 'GPT-4o-mini', tier: 'normal' },
    { value: 'gpt-4o', label: 'GPT-4o', tier: 'pro' },
    { value: 'claude-3-opus', label: 'Claude 3 Opus', tier: 'enterprise' }
  ].filter(model => hasTier(model.tier as UserTier));

  const form = useForm({
    defaultValues: {
      modelName: availableModels[0].value,
      temperature: 0.7,
      maxTokens: 2000
    }
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <label>AI Model</label>
      <select {...form.register('modelName')}>
        {availableModels.map(model => (
          <option key={model.value} value={model.value}>
            {model.label} ({model.tier} tier)
          </option>
        ))}
      </select>

      {/* Show locked models */}
      {[
        { value: 'gpt-4o', tier: 'pro' },
        { value: 'claude-3-opus', tier: 'enterprise' }
      ].filter(m => !hasTier(m.tier as UserTier)).map(model => (
        <div key={model.value} className="p-2 bg-muted rounded opacity-50">
          <Lock className="inline mr-2" />
          {model.value} (requires {model.tier} tier)
        </div>
      ))}

      {/* Temperature slider: Pro+ get more control */}
      <label>Temperature</label>
      <input
        type="range"
        min={hasTier('pro') ? 0 : 0.5} // Pro: 0-2, Free: 0.5-1.5
        max={hasTier('pro') ? 2 : 1.5}
        step={0.1}
        {...form.register('temperature')}
      />
      <p className="text-xs text-muted-foreground">
        {hasTier('pro') ? 'Full range: 0-2' : 'Limited range: 0.5-1.5 (Pro tier unlocks 0-2)'}
      </p>

      <button type="submit">Create Agent</button>
    </form>
  );
}
```

---

## Tag Selection Forms

### Pattern 11: Tag Picker with Entity-Specific Presets

```tsx
function TagPickerForm({ entityType }: { entityType: 'game' | 'agent' | 'document' }) {
  const [selectedTags, setSelectedTags] = useState<string[]>([]);

  const availableTags = {
    game: Object.keys(GAME_TAG_PRESETS),
    agent: Object.keys(AGENT_TAG_PRESETS),
    document: Object.keys(DOCUMENT_TAG_PRESETS)
  }[entityType];

  const handleToggle = (tagKey: string) => {
    setSelectedTags(prev =>
      prev.includes(tagKey)
        ? prev.filter(k => k !== tagKey)
        : [...prev, tagKey]
    );
  };

  const tags = createTagsFromKeys(entityType, selectedTags);

  return (
    <div>
      <label>Select Tags</label>

      {/* Tag checkboxes with visual preview */}
      <div className="grid grid-cols-2 gap-2">
        {availableTags.map(tagKey => {
          const preset = getTagPreset(entityType, tagKey);
          const isSelected = selectedTags.includes(tagKey);

          return (
            <label
              key={tagKey}
              className={cn(
                'flex items-center gap-2 p-2 rounded border cursor-pointer',
                isSelected ? 'border-primary bg-primary/10' : 'border-border'
              )}
            >
              <input
                type="checkbox"
                checked={isSelected}
                onChange={() => handleToggle(tagKey)}
              />

              {/* Preview tag badge */}
              <div
                className="flex-shrink-0 px-2 py-0.5 rounded text-[10px] font-medium"
                style={{ backgroundColor: preset.bgColor, color: preset.color }}
              >
                {preset.icon && <preset.icon className="w-3 h-3 inline mr-1" />}
                {preset.label}
              </div>
            </label>
          );
        })}
      </div>

      {/* Preview: How tags will look on card */}
      <div className="mt-4 p-4 bg-muted rounded">
        <p className="text-sm font-medium mb-2">Preview:</p>
        <div className="relative w-full h-32 bg-gradient-to-br from-slate-200 to-slate-300 rounded">
          <TagStrip tags={tags} maxVisible={3} variant="desktop" />
        </div>
      </div>
    </div>
  );
}
```

---

## Permission-Aware Form Components

### Pattern 12: Combobox with Tier-Based Options

```tsx
import { Combobox } from '@/components/ui/combobox';

function TierAwareCombobox() {
  const { tier, hasTier } = usePermissions();

  const options = [
    { value: 'basic', label: 'Basic Search', tier: 'free' },
    { value: 'advanced', label: 'Advanced Filters', tier: 'normal' },
    { value: 'ai-powered', label: 'AI-Powered Search', tier: 'pro' },
    { value: 'semantic', label: 'Semantic Search', tier: 'enterprise' }
  ];

  return (
    <Combobox
      options={options.map(opt => ({
        ...opt,
        disabled: !hasTier(opt.tier as UserTier),
        label: opt.disabled ? `${opt.label} (${opt.tier} tier)` : opt.label,
        icon: opt.disabled ? Lock : undefined
      }))}
      placeholder="Select search type..."
    />
  );
}
```

---

### Pattern 13: Slider with Permission-Based Range

```tsx
function TemperatureSlider() {
  const { hasTier } = usePermissions();

  const [min, max] = hasTier('pro') ? [0, 2] : [0.5, 1.5];

  return (
    <div>
      <label>Temperature ({min} - {max})</label>

      <input
        type="range"
        min={min}
        max={max}
        step={0.1}
        defaultValue={0.7}
      />

      {!hasTier('pro') && (
        <p className="text-xs text-muted-foreground">
          Upgrade to Pro for full range (0-2)
        </p>
      )}
    </div>
  );
}
```

---

## Form Error Handling with Permissions

### Pattern 14: Permission-Specific Error Messages

```typescript
function handleFormError(error: ApiError, field: string) {
  if (error.status === 403) {
    // Permission error
    if (error.code === 'TIER_INSUFFICIENT') {
      form.setError(field, {
        message: `This field requires ${error.requiredTier} tier. Upgrade to continue.`
      });
    } else if (error.code === 'ROLE_INSUFFICIENT') {
      form.setError(field, {
        message: `Only ${error.requiredRole}s can modify this field.`
      });
    } else {
      form.setError(field, {
        message: 'You do not have permission to modify this field.'
      });
    }
  } else if (error.status === 400) {
    // Validation error
    form.setError(field, { message: error.message });
  } else {
    // Generic error
    toast.error('An error occurred. Please try again.');
  }
}

// Usage
const onSubmit = async (data: FormData) => {
  try {
    await updateGame(gameId, data);
  } catch (error) {
    handleFormError(error, 'publicationState');
  }
};
```

---

## Dynamic Form Generation

### Pattern 15: Schema-Driven Forms with Permission Filtering

```typescript
interface FormFieldConfig {
  name: string;
  type: 'text' | 'number' | 'select' | 'checkbox';
  label: string;
  requiredPermission?: string;
  requiredTier?: UserTier;
  validation: z.ZodType;
}

const gameFormFields: FormFieldConfig[] = [
  { name: 'title', type: 'text', label: 'Title', validation: z.string().min(1) },
  { name: 'publisher', type: 'text', label: 'Publisher', validation: z.string() },
  { name: 'customSlug', type: 'text', label: 'Custom URL Slug', requiredTier: 'pro', validation: z.string().regex(/^[a-z0-9-]+$/) },
  { name: 'featured', type: 'checkbox', label: 'Featured', requiredPermission: 'game.feature', validation: z.boolean() }
];

function DynamicGameForm() {
  const { canAccess, hasTier } = usePermissions();

  const accessibleFields = gameFormFields.filter(field =>
    (!field.requiredPermission || canAccess(field.requiredPermission)) &&
    (!field.requiredTier || hasTier(field.requiredTier))
  );

  const schema = z.object(
    Object.fromEntries(accessibleFields.map(f => [f.name, f.validation]))
  );

  const form = useForm({ resolver: zodResolver(schema) });

  return (
    <form>
      {accessibleFields.map(field => (
        <div key={field.name}>
          <label>{field.label}</label>
          {field.type === 'text' && <input {...form.register(field.name)} />}
          {field.type === 'checkbox' && <input type="checkbox" {...form.register(field.name)} />}
          {/* ... other field types */}
        </div>
      ))}

      {/* Show locked fields */}
      {gameFormFields.filter(f => !accessibleFields.includes(f)).map(field => (
        <div key={field.name} className="opacity-50">
          <label>{field.label} <Lock className="inline w-3 h-3" /></label>
          <input disabled placeholder={`Requires ${field.requiredTier ?? 'higher tier'}`} />
        </div>
      ))}

      <button type="submit">Save</button>
    </form>
  );
}
```

---

## Form Validation Helpers

### Reusable Validation Functions

```typescript
// lib/validation/permission-validators.ts

export function createTierValidator(requiredTier: UserTier) {
  return (value: unknown, context: ValidationContext) => {
    const { hasTier } = context.permissions;

    if (!hasTier(requiredTier)) {
      return {
        valid: false,
        error: `This field requires ${requiredTier} tier or higher`
      };
    }

    return { valid: true };
  };
}

export function createRoleValidator(requiredRole: UserRole) {
  return (value: unknown, context: ValidationContext) => {
    const { role } = context.permissions;

    if (!hasMinimumRole(role, requiredRole)) {
      return {
        valid: false,
        error: `Only ${requiredRole}s can modify this field`
      };
    }

    return { valid: true };
  };
}

// Usage with Zod
const gameSchema = z.object({
  customSlug: z.string()
    .regex(/^[a-z0-9-]+$/)
    .refine(createTierValidator('pro'), 'Custom slugs require Pro tier')
});
```

---

## Real-World Form Examples

### Example 1: User Profile Settings

```tsx
function UserProfileForm() {
  const { tier, role, canAccess } = usePermissions();

  const form = useForm({
    defaultValues: {
      displayName: user.displayName,
      email: user.email,
      theme: user.theme,

      // Pro tier only
      customDomain: user.customDomain,
      apiKey: user.apiKey,

      // Admin only
      tier: user.tier,
      role: user.role
    }
  });

  return (
    <form onSubmit={form.handleSubmit(onSubmit)}>
      <section>
        <h2>Basic Settings</h2>
        <input {...form.register('displayName')} />
        <input {...form.register('email')} />
        <select {...form.register('theme')}>
          <option value="light">Light</option>
          <option value="dark">Dark</option>
          <option value="system">System</option>
        </select>
      </section>

      <TierGate minimum="pro" fallback={<UpgradePromptCard feature="Advanced Settings" />}>
        <section>
          <h2>Advanced Settings</h2>
          <input {...form.register('customDomain')} placeholder="custom.meepleai.com" />
          <input {...form.register('apiKey')} type="password" placeholder="API Key" />
        </section>
      </TierGate>

      <PermissionGate feature="admin-settings">
        <section>
          <h2>Admin Settings</h2>
          <select {...form.register('tier')}>
            {Object.values(UserTier).map(t => <option key={t} value={t}>{t}</option>)}
          </select>
          <select {...form.register('role')}>
            {Object.values(UserRole).map(r => <option key={r} value={r}>{r}</option>)}
          </select>
        </section>
      </PermissionGate>

      <button type="submit">Save Settings</button>
    </form>
  );
}
```

---

### Example 2: Create Game Wizard (Multi-Step with Permissions)

```tsx
function CreateGameWizard() {
  const [currentStep, setCurrentStep] = useState(0);
  const { canAccess, hasTier } = usePermissions();

  const steps = [
    {
      title: 'Basic Info',
      component: BasicInfoStep,
      schema: z.object({ title: z.string(), publisher: z.string() })
    },
    {
      title: 'Media',
      component: MediaStep,
      schema: z.object({ imageUrl: z.string().url() }),
      requiredTier: 'free'
    },
    {
      title: 'SEO',
      component: SeoStep,
      schema: z.object({ seoTitle: z.string(), seoDescription: z.string() }),
      requiredTier: 'pro'
    },
    {
      title: 'Publication',
      component: PublicationStep,
      schema: z.object({ publicationState: z.enum(['draft', 'published']) }),
      requiredPermission: 'game.publish'
    }
  ];

  const accessibleSteps = steps.filter(step =>
    (!step.requiredTier || hasTier(step.requiredTier)) &&
    (!step.requiredPermission || canAccess(step.requiredPermission))
  );

  const combinedSchema = z.object(
    Object.assign({}, ...accessibleSteps.map(s => s.schema.shape))
  );

  const form = useForm({
    resolver: zodResolver(combinedSchema)
  });

  const StepComponent = accessibleSteps[currentStep]?.component;

  return (
    <div>
      {/* Stepper */}
      <div className="flex gap-2 mb-8">
        {accessibleSteps.map((step, index) => (
          <button
            key={index}
            onClick={() => setCurrentStep(index)}
            className={cn(
              'px-4 py-2 rounded',
              currentStep === index ? 'bg-primary text-primary-foreground' : 'bg-muted'
            )}
          >
            {step.title}
          </button>
        ))}
      </div>

      {/* Current step form */}
      <FormProvider {...form}>
        {StepComponent && <StepComponent />}
      </FormProvider>

      {/* Navigation */}
      <div className="flex justify-between mt-8">
        <button
          onClick={() => setCurrentStep(i => Math.max(0, i - 1))}
          disabled={currentStep === 0}
        >
          Previous
        </button>

        {currentStep < accessibleSteps.length - 1 ? (
          <button onClick={() => setCurrentStep(i => i + 1)}>Next</button>
        ) : (
          <button onClick={form.handleSubmit(onSubmit)}>Create Game</button>
        )}
      </div>
    </div>
  );
}
```

---

## TypeScript Form Types

### Utility Types for Permission-Aware Forms

```typescript
// lib/types/form-utils.ts

/**
 * Extract fields from form data based on permissions
 */
export type PermittedFields<T, TierRequired extends UserTier> = {
  [K in keyof T]: T[K] extends { __tier: TierRequired } ? K : never;
}[keyof T];

/**
 * Form data with tier-gated fields
 */
export interface TieredFormData {
  // Free tier fields
  title: string;
  description: string;

  // Pro tier fields
  customSlug?: string & { __tier: 'pro' };
  seoMetadata?: SEOData & { __tier: 'pro' };

  // Enterprise tier fields
  customDomain?: string & { __tier: 'enterprise' };
}

/**
 * Get allowed fields for user's tier
 */
export function getAllowedFields<T extends TieredFormData>(
  tier: UserTier
): Array<keyof T> {
  const tierLevel = TIER_HIERARCHY[tier];

  return Object.keys(defaultFormData).filter(key => {
    const field = defaultFormData[key as keyof T];
    const fieldTier = field?.__tier;

    if (!fieldTier) return true; // No tier requirement

    return TIER_HIERARCHY[fieldTier] <= tierLevel;
  }) as Array<keyof T>;
}

// Usage
function TieredForm() {
  const { tier } = usePermissions();
  const allowedFields = getAllowedFields(tier);

  return (
    <form>
      {allowedFields.includes('title') && <input name="title" />}
      {allowedFields.includes('customSlug') && <input name="customSlug" placeholder="Pro tier" />}
      {allowedFields.includes('customDomain') && <input name="customDomain" placeholder="Enterprise" />}
    </form>
  );
}
```

---

## Summary: Form Integration Checklist

**Permission-Aware Forms**:
- [ ] Schema adapts to user tier/role
- [ ] Locked fields show upgrade prompts
- [ ] Conditional validation (tier-specific rules)
- [ ] Double-check permissions before submit
- [ ] Graceful error handling (403 → upgrade path)

**Tag Selection**:
- [ ] Entity-specific tag presets
- [ ] Visual preview before selection
- [ ] Max tags enforced (client + server)
- [ ] Tag priority sorting

**Agent Config**:
- [ ] Model selection by tier (Free: GPT-3.5, Pro: GPT-4, Enterprise: Claude)
- [ ] Parameter ranges by tier (Free: limited, Pro: full range)
- [ ] Capability selection (RAG/Vision Pro tier)

**Collection Management**:
- [ ] Real-time limit indicators
- [ ] Pre-submit quota validation
- [ ] Optimistic updates with rollback
- [ ] Clear warning when approaching limit

**Validation**:
- [ ] Client-side: Zod schemas with permission checks
- [ ] Server-side: FluentValidation with PermissionRegistry
- [ ] Error messages include upgrade path
- [ ] Field-level permission feedback

---

## Resources

- React Hook Form: https://react-hook-form.com/
- Zod Validation: https://zod.dev/
- FluentValidation: https://docs.fluentvalidation.net/
- Form Accessibility: https://www.w3.org/WAI/tutorials/forms/
