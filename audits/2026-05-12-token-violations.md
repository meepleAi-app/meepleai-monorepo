# Token Vocabulary Violations — Inventory

| Field | Value |
|---|---|
| **Date** | 2026-05-12 |
| **Generator** | `pnpm lint:tokens` (DS-2) |
| **Spec** | [`2026-05-12-token-canonicalization.md`](../docs/for-developers/specs/2026-05-12-token-canonicalization.md) |
| **Rule** | `local/no-hardcoded-color-utility` |
| **Total violations** | 4141 |
| **Files affected** | 483 |
| **Clusters affected** | 191 |

## Violations by cluster

| Cluster | Violations | Suggested stage |
|---|---|---|
| `app/admin/(dashboard)` | 1241 | manual |
| `ui/data-display` | 450 | manual |
| `admin/knowledge-base` | 313 | manual |
| `admin/shared-games` | 128 | manual |
| `toolkit-drawer/tabs` | 122 | manual |
| `features/gamebook` | 101 | DS-10 |
| `admin/agents` | 83 | manual |
| `stories/Animations.stories.tsx` | 76 | manual |
| `admin/users` | 75 | manual |
| `app/(authenticated)/admin` | 67 | manual |
| `admin/games` | 64 | manual |
| `app/(authenticated)/play-records` | 55 | manual |
| `admin/command-center` | 54 | manual |
| `loading/SkeletonLoader.tsx` | 54 | manual |
| `admin/mechanic-extractor` | 52 | manual |
| `play-records/NewPlayRecordSheet.tsx` | 50 | manual |
| `layout/mobile` | 49 | manual |
| `rag-dashboard/config` | 44 | manual |
| `app/admin/database-sync` | 36 | manual |
| `app/(authenticated)/toolkit` | 32 | manual |
| `admin/sandbox` | 31 | manual |
| `app/(auth)/welcome` | 27 | manual |
| `errors/ErrorBoundary.stories.tsx` | 27 | manual |
| `stories/DesignTokens.stories.tsx` | 27 | manual |
| `comments/CommentItem.tsx` | 22 | manual |
| `empty-state/EmptyState.stories.tsx` | 22 | manual |
| `stories/BackgroundTexture.stories.tsx` | 22 | manual |
| `admin/infrastructure` | 20 | manual |
| `app/(public)/contact` | 18 | manual |
| `modals/ErrorModal.tsx` | 18 | manual |
| `app/(authenticated)/n8n` | 17 | manual |
| `app/(authenticated)/versions` | 17 | manual |
| `admin/rag` | 17 | manual |
| `app/(authenticated)/sessions` | 16 | manual |
| `app/(public)/join` | 16 | manual |
| `play-records/PlayHistory.tsx` | 16 | manual |
| `toolkit-drawer/shared` | 16 | manual |
| `upload/UploadSummary.tsx` | 16 | manual |
| `admin/PdfLimitsConfig.tsx` | 15 | manual |
| `errors/ErrorBoundary.tsx` | 15 | manual |
| `admin/notifications` | 14 | manual |
| `app/join/[inviteToken]` | 13 | manual |
| `toolkit/Scoreboard.tsx` | 13 | manual |
| `app/(authenticated)/editor` | 12 | manual |
| `editor/ConflictResolutionModal.tsx` | 12 | manual |
| `legal/LegalMarkdown.tsx` | 12 | manual |
| `onboarding/FirstGameStep.tsx` | 12 | manual |
| `ui/feedback` | 12 | manual |
| `admin/DashboardHeader.tsx` | 11 | manual |
| `chat-unified/AgentCreationWizard.tsx` | 11 | manual |
| `chat-unified/RuleSourceCard.tsx` | 11 | manual |
| `app/(public)/about` | 10 | manual |
| `admin/KPICard.tsx` | 10 | manual |
| `admin/layout` | 10 | manual |
| `onboarding/OnboardingWizard.tsx` | 10 | manual |
| `game-detail/AgentChatPanel.tsx` | 9 | manual |
| `toolkit/Counter.tsx` | 9 | manual |
| `app/(authenticated)/setup` | 8 | manual |
| `chat/panel` | 8 | manual |
| `diff/DiffSummary.tsx` | 8 | manual |
| `game-detail/game-hero-section.tsx` | 8 | manual |
| `game-detail/stats-grid.tsx` | 8 | manual |
| `modals/SessionWarningModal.tsx` | 8 | manual |
| `app/(authenticated)/gamebook` | 7 | manual |
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
| `chat/entry` | 5 | manual |
| `legal/CookieConsentBanner.tsx` | 5 | manual |
| `onboarding/InterestsStep.tsx` | 5 | manual |
| `state/BoardStateEditor.tsx` | 5 | manual |
| `toolkit/Randomizer.tsx` | 5 | manual |
| `ui/overlays` | 5 | manual |
| `ui/tags` | 5 | manual |
| `upload/UploadQueueItem.tsx` | 5 | manual |
| `app/(auth)/login` | 4 | manual |
| `app/(auth)/register` | 4 | manual |
| `app/(auth)/reset-password` | 4 | manual |
| `app/(auth)/setup-account` | 4 | manual |
| `admin/AlertsBanner.tsx` | 4 | manual |
| `admin/invitations` | 4 | manual |
| `admin/rag-quality-dashboard.tsx` | 4 | manual |
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
| `ui/detail-layout` | 4 | manual |
| `ui/navigation` | 4 | manual |
| `admin/charts` | 3 | manual |
| `auth/AuthModal.stories.tsx` | 3 | manual |
| `chat-unified/AiLoadingState.tsx` | 3 | manual |
| `comments/InlineCommentIndicator.tsx` | 3 | manual |
| `features/game-chat` | 3 | DS-10 |
| `game-detail/TypingIndicator.tsx` | 3 | manual |
| `layout/UserShell` | 3 | manual |
| `prompt/LazyPromptEditor.tsx` | 3 | manual |
| `prompt/PromptEditor.tsx` | 3 | manual |
| `pwa/UpdatePrompt.tsx` | 3 | manual |
| `toolkit/Timer.tsx` | 3 | manual |
| `ui/sheet.tsx` | 3 | manual |
| `versioning/VersionTimeline.tsx` | 3 | manual |
| `admin/ApprovalStatusFilter.tsx` | 2 | manual |
| `auth/RequireRole.tsx` | 2 | manual |
| `chat-unified/ChatInputArea.tsx` | 2 | manual |
| `chat-unified/CitationSheet.tsx` | 2 | manual |
| `chat-unified/EmbeddedChatView.tsx` | 2 | manual |
| `chat-unified/VoiceTranscriptOverlay.tsx` | 2 | manual |
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
| `ui/shared-games` | 2 | manual |
| `upload/MultiFileUpload.tsx` | 2 | manual |
| `app/(authenticated)/profile` | 1 | manual |
| `app/(authenticated)/settings` | 1 | manual |
| `admin/debug-chat` | 1 | manual |
| `admin/FeatureFlagsTab.tsx` | 1 | manual |
| `admin/ui-library` | 1 | manual |
| `admin/UserActivityTimeline.tsx` | 1 | manual |
| `auth/TwoFactorSetup.tsx` | 1 | manual |
| `chat-unified/AgentSwitchDialog.tsx` | 1 | manual |
| `chat-unified/ChatHistoryDrawer.tsx` | 1 | manual |
| `chat-unified/ChatMessageList.tsx` | 1 | manual |
| `chat-unified/ChatMobile.tsx` | 1 | manual |
| `comments/CommentForm.tsx` | 1 | manual |
| `editor/ViewModeToggle.tsx` | 1 | manual |
| `features/chat` | 1 | manual |
| `features/common` | 1 | manual |
| `features/play` | 1 | manual |
| `features/player-detail` | 1 | DS-9 |
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
| `ui/auth-card` | 1 | manual |
| `ui/invites` | 1 | manual |
| `ui/pricing-card` | 1 | manual |
| `versioning/VersionTimelineFilters.tsx` | 1 | manual |

## Top 20 files

| File | Violations |
|---|---|
| `src/app/admin/(dashboard)/agents/inspector/page.tsx` | 122 |
| `src/stories/Animations.stories.tsx` | 76 |
| `src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/analyses/page.tsx` | 57 |
| `src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx` | 54 |
| `src/components/admin/command-center/CommandCenterDashboard.tsx` | 54 |
| `src/components/loading/SkeletonLoader.tsx` | 54 |
| `src/components/admin/knowledge-base/processing-metrics.tsx` | 51 |
| `src/app/admin/(dashboard)/knowledge-base/embedding/page.tsx` | 50 |
| `src/components/play-records/NewPlayRecordSheet.tsx` | 50 |
| `src/app/admin/(dashboard)/users/[id]/page.tsx` | 48 |
| `src/components/admin/knowledge-base/upload-zone.tsx` | 48 |
| `src/components/admin/knowledge-base/rag-pipeline-flow.tsx` | 47 |
| `src/app/admin/(dashboard)/knowledge-base/documents/page.tsx` | 46 |
| `src/components/rag-dashboard/config/RagConfigurationForm.tsx` | 44 |
| `src/components/admin/knowledge-base/upload-settings.tsx` | 43 |
| `src/components/admin/shared-games/SharedGameExtraMeepleCard.tsx` | 43 |
| `src/app/admin/(dashboard)/agents/config/AgentModelsTabContent.tsx` | 40 |
| `src/app/admin/(dashboard)/agents/config/AgentStrategyTabContent.tsx` | 40 |
| `src/app/admin/(dashboard)/shared-games/[id]/client.tsx` | 40 |
| `src/app/admin/(dashboard)/agents/page.tsx` | 38 |

## Notes

- Rule is in `warn` mode during DS-3 inventory + DS-4..DS-11 cluster migrations.
- Switched to `error` in DS-12 once `pnpm lint:tokens --max-warnings 0` is green.
- Companion JSON: [`2026-05-12-token-violations.json`](./2026-05-12-token-violations.json).
