# Token Vocabulary Violations — Inventory

| Field | Value |
|---|---|
| **Date** | 2026-05-12 |
| **Generator** | `pnpm lint:tokens` (DS-2) |
| **Spec** | [`2026-05-12-token-canonicalization.md`](../docs/for-developers/specs/2026-05-12-token-canonicalization.md) |
| **Rule** | `local/no-hardcoded-color-utility` |
| **Total violations** | 1332 |
| **Files affected** | 181 |
| **Clusters affected** | 138 |

## Violations by cluster

| Cluster | Violations | Suggested stage |
|---|---|---|
| `toolkit-drawer/tabs` | 122 | manual |
| `stories/Animations.stories.tsx` | 76 | manual |
| `app/(authenticated)/admin` | 67 | manual |
| `app/(authenticated)/play-records` | 55 | manual |
| `loading/SkeletonLoader.tsx` | 54 | manual |
| `play-records/NewPlayRecordSheet.tsx` | 50 | manual |
| `layout/mobile` | 49 | manual |
| `rag-dashboard/config` | 44 | manual |
| `app/admin/database-sync` | 36 | manual |
| `app/(authenticated)/toolkit` | 32 | manual |
| `app/(auth)/welcome` | 27 | manual |
| `errors/ErrorBoundary.stories.tsx` | 27 | manual |
| `stories/DesignTokens.stories.tsx` | 27 | manual |
| `comments/CommentItem.tsx` | 22 | manual |
| `empty-state/EmptyState.stories.tsx` | 22 | manual |
| `stories/BackgroundTexture.stories.tsx` | 22 | manual |
| `app/(public)/contact` | 18 | manual |
| `modals/ErrorModal.tsx` | 18 | manual |
| `app/(authenticated)/n8n` | 17 | manual |
| `app/(authenticated)/versions` | 17 | manual |
| `app/(authenticated)/sessions` | 16 | manual |
| `app/(public)/join` | 16 | manual |
| `play-records/PlayHistory.tsx` | 16 | manual |
| `toolkit-drawer/shared` | 16 | manual |
| `upload/UploadSummary.tsx` | 16 | manual |
| `errors/ErrorBoundary.tsx` | 15 | manual |
| `app/join/[inviteToken]` | 13 | manual |
| `toolkit/Scoreboard.tsx` | 13 | manual |
| `app/(authenticated)/editor` | 12 | manual |
| `editor/ConflictResolutionModal.tsx` | 12 | manual |
| `legal/LegalMarkdown.tsx` | 12 | manual |
| `onboarding/FirstGameStep.tsx` | 12 | manual |
| `app/(public)/about` | 10 | manual |
| `onboarding/OnboardingWizard.tsx` | 10 | manual |
| `game-detail/AgentChatPanel.tsx` | 9 | manual |
| `toolkit/Counter.tsx` | 9 | manual |
| `app/(authenticated)/setup` | 8 | manual |
| `diff/DiffSummary.tsx` | 8 | manual |
| `game-detail/game-hero-section.tsx` | 8 | manual |
| `game-detail/stats-grid.tsx` | 8 | manual |
| `modals/SessionWarningModal.tsx` | 8 | manual |
| `accessible/Accessibility.stories.tsx` | 7 | manual |
| `auth/VerificationError.stories.tsx` | 7 | manual |
| `editor/RichTextEditor.tsx` | 7 | manual |
| `profile/ClaimGuestGames.tsx` | 7 | manual |
| `upload/UploadQueue.tsx` | 7 | manual |
| `versioning/ChangeItem.tsx` | 7 | manual |
| `auth/LoginForm.stories.tsx` | 6 | manual |
| `empty-state/EmptyState.tsx` | 6 | manual |
| `legal/LegalPageLayout.tsx` | 6 | manual |
| `modals/SessionSetupModal.tsx` | 6 | manual |
| `toolkit/AiToolkitGenerator.tsx` | 6 | manual |
| `toolkit/CardDeckTool.tsx` | 6 | manual |
| `toolkit/CounterTool.tsx` | 6 | manual |
| `toolkit/DiceRoller.tsx` | 6 | manual |
| `auth/VerificationPending.stories.tsx` | 5 | manual |
| `auth/VerificationSuccess.stories.tsx` | 5 | manual |
| `legal/CookieConsentBanner.tsx` | 5 | manual |
| `onboarding/InterestsStep.tsx` | 5 | manual |
| `state/BoardStateEditor.tsx` | 5 | manual |
| `toolkit/Randomizer.tsx` | 5 | manual |
| `upload/UploadQueueItem.tsx` | 5 | manual |
| `app/(auth)/login` | 4 | manual |
| `app/(auth)/register` | 4 | manual |
| `app/(auth)/reset-password` | 4 | manual |
| `app/(auth)/setup-account` | 4 | manual |
| `auth/RegisterForm.stories.tsx` | 4 | manual |
| `diff/DiffViewerEnhanced.tsx` | 4 | manual |
| `editor/EditorToolbar.tsx` | 4 | manual |
| `errors/RouteErrorBoundary.stories.tsx` | 4 | manual |
| `loading/TypingIndicator.tsx` | 4 | manual |
| `onboarding/FirstAgentStep.tsx` | 4 | manual |
| `onboarding/PasswordStep.tsx` | 4 | manual |
| `onboarding/ProfileStep.tsx` | 4 | manual |
| `pdf/PdfProcessingProgressBar.stories.tsx` | 4 | manual |
| `pwa/InstallPrompt.tsx` | 4 | manual |
| `toolkit-drawer/ToolkitDrawer.tsx` | 4 | manual |
| `auth/AuthModal.stories.tsx` | 3 | manual |
| `comments/InlineCommentIndicator.tsx` | 3 | manual |
| `game-detail/TypingIndicator.tsx` | 3 | manual |
| `layout/UserShell` | 3 | manual |
| `prompt/LazyPromptEditor.tsx` | 3 | manual |
| `prompt/PromptEditor.tsx` | 3 | manual |
| `pwa/UpdatePrompt.tsx` | 3 | manual |
| `toolkit/Timer.tsx` | 3 | manual |
| `versioning/VersionTimeline.tsx` | 3 | manual |
| `auth/RequireRole.tsx` | 2 | manual |
| `comments/CommentThread.tsx` | 2 | manual |
| `errors/RouteErrorBoundary.tsx` | 2 | manual |
| `forms/FormDescription.tsx` | 2 | manual |
| `game-state/MeeplePlayerStateCard.tsx` | 2 | manual |
| `layout/AdminSideDrawer` | 2 | manual |
| `layout/HubLayout` | 2 | manual |
| `layout/SideDrawer` | 2 | manual |
| `layout/ViewModeToggle.tsx` | 2 | manual |
| `onboarding/WelcomeChecklist.tsx` | 2 | manual |
| `pdf/PdfProgressBar.tsx` | 2 | manual |
| `pdf/PdfUploadForm.stories.tsx` | 2 | manual |
| `pdf/PdfViewerModal.tsx` | 2 | manual |
| `pdf/progress-card.tsx` | 2 | manual |
| `pdf/progress-modal.tsx` | 2 | manual |
| `rag-dashboard/builder` | 2 | manual |
| `rag-dashboard/reference` | 2 | manual |
| `showcase/stories` | 2 | manual |
| `upload/MultiFileUpload.tsx` | 2 | manual |
| `app/(authenticated)/profile` | 1 | manual |
| `app/(authenticated)/settings` | 1 | manual |
| `auth/TwoFactorSetup.tsx` | 1 | manual |
| `comments/CommentForm.tsx` | 1 | manual |
| `editor/ViewModeToggle.tsx` | 1 | manual |
| `features/chat` | 1 | manual |
| `features/common` | 1 | manual |
| `features/play` | 1 | manual |
| `game-detail/SplitViewLayout.tsx` | 1 | manual |
| `game-state/GameStateViewer.stories.tsx` | 1 | manual |
| `game-state/ResourceTracker.stories.tsx` | 1 | manual |
| `game-state/StateHistoryTimeline.stories.tsx` | 1 | manual |
| `layout/ContextualHand` | 1 | manual |
| `layout/MobileCTAPill.tsx` | 1 | manual |
| `layout/PageHeader.tsx` | 1 | manual |
| `layout/SearchOverlay.tsx` | 1 | manual |
| `onboarding/tour` | 1 | manual |
| `pdf/PdfErrorCard.stories.tsx` | 1 | manual |
| `pdf/PdfMetricsDisplay.tsx` | 1 | manual |
| `pdf/PdfProgressBar.stories.tsx` | 1 | manual |
| `pdf/PdfStatusBadge.stories.tsx` | 1 | manual |
| `pdf/PdfStatusTimeline.tsx` | 1 | manual |
| `pdf/PdfViewerModal.stories.tsx` | 1 | manual |
| `pdf/progress-toast.tsx` | 1 | manual |
| `profile/AvatarUpload.tsx` | 1 | manual |
| `pwa/OfflineIndicator.tsx` | 1 | manual |
| `rag-dashboard/DashboardNav.tsx` | 1 | manual |
| `rag-dashboard/metrics` | 1 | manual |
| `rag-dashboard/RagHero.tsx` | 1 | manual |
| `search/SearchModeToggle.tsx` | 1 | manual |
| `state/LedgerTimeline.stories.tsx` | 1 | manual |
| `toolkit/WhiteboardWidget.tsx` | 1 | manual |
| `versioning/VersionTimelineFilters.tsx` | 1 | manual |

## Top 20 files

| File | Violations |
|---|---|
| `src/stories/Animations.stories.tsx` | 76 |
| `src/components/loading/SkeletonLoader.tsx` | 54 |
| `src/components/play-records/NewPlayRecordSheet.tsx` | 50 |
| `src/components/rag-dashboard/config/RagConfigurationForm.tsx` | 44 |
| `src/app/(authenticated)/play-records/[id]/page.tsx` | 32 |
| `src/app/(auth)/welcome/_content.tsx` | 27 |
| `src/components/errors/ErrorBoundary.stories.tsx` | 27 |
| `src/stories/DesignTokens.stories.tsx` | 27 |
| `src/app/(authenticated)/admin/wizard/steps/ChatSetupStep.tsx` | 22 |
| `src/app/(authenticated)/play-records/stats/page.tsx` | 22 |
| `src/components/comments/CommentItem.tsx` | 22 |
| `src/components/empty-state/EmptyState.stories.tsx` | 22 |
| `src/components/toolkit-drawer/tabs/EventDiaryTab.tsx` | 22 |
| `src/stories/BackgroundTexture.stories.tsx` | 22 |
| `src/app/(public)/contact/page.tsx` | 18 |
| `src/components/modals/ErrorModal.tsx` | 18 |
| `src/app/(authenticated)/admin/wizard/steps/QAStep.tsx` | 17 |
| `src/app/(authenticated)/n8n/page.tsx` | 17 |
| `src/app/(authenticated)/versions/page.tsx` | 17 |
| `src/components/toolkit-drawer/tabs/DiceRollerTab.tsx` | 17 |

## Notes

- Rule is in `warn` mode during DS-3 inventory + DS-4..DS-11 cluster migrations.
- Switched to `error` in DS-12 once `pnpm lint:tokens --max-warnings 0` is green.
- Companion JSON: [`2026-05-12-token-violations.json`](./2026-05-12-token-violations.json).
