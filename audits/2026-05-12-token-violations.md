# Token Vocabulary Violations — Inventory

| Field | Value |
|---|---|
| **Date** | 2026-05-12 |
| **Generator** | `pnpm lint:tokens` (DS-2) |
| **Spec** | [`2026-05-12-token-canonicalization.md`](../docs/for-developers/specs/2026-05-12-token-canonicalization.md) |
| **Rule** | `local/no-hardcoded-color-utility` |
| **Total violations** | 5710 |
| **Files affected** | 729 |
| **Clusters affected** | 308 |

## Violations by cluster

| Cluster | Violations | Suggested stage |
|---|---|---|
| `app/admin/(dashboard)` | 1269 | manual |
| `ui/data-display` | 472 | manual |
| `admin/knowledge-base` | 314 | manual |
| `admin/shared-games` | 129 | manual |
| `session/live` | 128 | manual |
| `toolkit-drawer/tabs` | 127 | manual |
| `app/(authenticated)/library` | 113 | DS-11 |
| `library/add-game-sheet` | 113 | manual |
| `features/gamebook` | 109 | DS-10 |
| `collection/wizard` | 93 | manual |
| `features/session-live` | 92 | DS-6 |
| `admin/agents` | 83 | manual |
| `stories/Animations.stories.tsx` | 79 | manual |
| `admin/users` | 75 | manual |
| `admin/games` | 70 | manual |
| `app/(authenticated)/admin` | 69 | manual |
| `app/(authenticated)/sessions` | 65 | manual |
| `agent/config` | 64 | manual |
| `session/WhiteboardTool.tsx` | 58 | manual |
| `app/(authenticated)/play-records` | 56 | manual |
| `admin/command-center` | 54 | manual |
| `loading/SkeletonLoader.tsx` | 54 | manual |
| `layout/mobile` | 53 | manual |
| `admin/mechanic-extractor` | 52 | manual |
| `play-records/NewPlayRecordSheet.tsx` | 51 | manual |
| `rag-dashboard/config` | 44 | manual |
| `session/Scoreboard.tsx` | 44 | manual |
| `library/GameActionsModal.tsx` | 41 | manual |
| `library/PdfVersionManager.tsx` | 39 | manual |
| `app/admin/database-sync` | 36 | manual |
| `app/(authenticated)/toolkit` | 34 | manual |
| `admin/sandbox` | 34 | manual |
| `errors/ErrorBoundary.stories.tsx` | 30 | manual |
| `agent/settings` | 29 | manual |
| `app/(auth)/welcome` | 27 | manual |
| `session/ScoreInput.tsx` | 27 | manual |
| `stories/DesignTokens.stories.tsx` | 27 | manual |
| `session/CounterTool.tsx` | 26 | manual |
| `session/SessionDetailModal.tsx` | 23 | manual |
| `app/(authenticated)/n8n` | 22 | manual |
| `comments/CommentItem.tsx` | 22 | manual |
| `empty-state/EmptyState.stories.tsx` | 22 | manual |
| `stories/BackgroundTexture.stories.tsx` | 22 | manual |
| `admin/infrastructure` | 21 | manual |
| `game-night/SessionChatWidget.tsx` | 20 | manual |
| `session/SessionHeader.tsx` | 20 | manual |
| `session/ToolRail.tsx` | 20 | manual |
| `modals/ErrorModal.tsx` | 19 | manual |
| `session/StartSessionSheet.tsx` | 19 | manual |
| `app/(authenticated)/versions` | 18 | manual |
| `app/(public)/contact` | 18 | manual |
| `agent/slots` | 18 | manual |
| `game-night/ScoreAssistant.tsx` | 18 | manual |
| `play-records/PlayHistory.tsx` | 18 | manual |
| `session/DiceRoller.tsx` | 18 | manual |
| `admin/rag` | 17 | manual |
| `library/AddExpansionSheet.tsx` | 17 | manual |
| `ui/feedback` | 17 | manual |
| `app/(public)/join` | 16 | manual |
| `errors/ErrorBoundary.tsx` | 16 | manual |
| `session/TurnOrderTool.tsx` | 16 | manual |
| `toolkit-drawer/shared` | 16 | manual |
| `upload/UploadSummary.tsx` | 16 | manual |
| `admin/PdfLimitsConfig.tsx` | 15 | manual |
| `app/join/[inviteToken]` | 14 | manual |
| `admin/notifications` | 14 | manual |
| `game-night/steps` | 14 | manual |
| `app/(authenticated)/editor` | 13 | manual |
| `admin/DashboardHeader.tsx` | 13 | manual |
| `chat/panel` | 13 | manual |
| `chat-unified/AgentCreationWizard.tsx` | 13 | manual |
| `dashboard/SearchExpander.tsx` | 13 | manual |
| `features/agent-detail` | 13 | DS-8 |
| `features/game-detail` | 13 | DS-7 |
| `toolkit/Scoreboard.tsx` | 13 | manual |
| `admin/layout` | 12 | manual |
| `editor/ConflictResolutionModal.tsx` | 12 | manual |
| `legal/LegalMarkdown.tsx` | 12 | manual |
| `onboarding/FirstGameStep.tsx` | 12 | manual |
| `chat-unified/RuleSourceCard.tsx` | 11 | manual |
| `onboarding/OnboardingWizard.tsx` | 11 | manual |
| `session/SessionLobby.tsx` | 11 | manual |
| `app/(public)/about` | 10 | manual |
| `admin/KPICard.tsx` | 10 | manual |
| `session/SessionPhotoGallery.tsx` | 10 | manual |
| `session/SnapshotUploadDialog.tsx` | 10 | manual |
| `game-detail/AgentChatPanel.tsx` | 9 | manual |
| `session/ScoreNumpad.tsx` | 9 | manual |
| `toolkit/Counter.tsx` | 9 | manual |
| `app/(authenticated)/gamebook` | 8 | manual |
| `app/(authenticated)/setup` | 8 | manual |
| `diff/DiffSummary.tsx` | 8 | manual |
| `game-detail/game-hero-section.tsx` | 8 | manual |
| `game-detail/stats-grid.tsx` | 8 | manual |
| `library/DocumentSelectionPanel.tsx` | 8 | manual |
| `modals/SessionWarningModal.tsx` | 8 | manual |
| `session/ToolSheetContent.tsx` | 8 | manual |
| `accessible/Accessibility.stories.tsx` | 7 | manual |
| `auth/VerificationError.stories.tsx` | 7 | manual |
| `dashboard/SessionPanel.tsx` | 7 | manual |
| `editor/RichTextEditor.tsx` | 7 | manual |
| `features/session-summary` | 7 | DS-6 |
| `features/sessions` | 7 | DS-5 |
| `game-night/SessionHeader.tsx` | 7 | manual |
| `library/game-table` | 7 | manual |
| `profile/ClaimGuestGames.tsx` | 7 | manual |
| `session/PlayerModeCard.tsx` | 7 | manual |
| `session/ResumePhotoReview.tsx` | 7 | manual |
| `session/SpectatorModeCard.tsx` | 7 | manual |
| `toolkit/AiToolkitGenerator.tsx` | 7 | manual |
| `upload/UploadQueue.tsx` | 7 | manual |
| `versioning/ChangeItem.tsx` | 7 | manual |
| `auth/LoginForm.stories.tsx` | 6 | manual |
| `dashboard/session-hero.tsx` | 6 | manual |
| `empty-state/EmptyState.tsx` | 6 | manual |
| `game-night/LiveScoreboard.tsx` | 6 | manual |
| `legal/LegalPageLayout.tsx` | 6 | manual |
| `library/KbStatusPanel.tsx` | 6 | manual |
| `modals/SessionSetupModal.tsx` | 6 | manual |
| `session/SessionJoinForm.tsx` | 6 | manual |
| `toolkit/CardDeckTool.tsx` | 6 | manual |
| `toolkit/CounterTool.tsx` | 6 | manual |
| `toolkit/DiceRoller.tsx` | 6 | manual |
| `ui/tags` | 6 | manual |
| `auth/VerificationPending.stories.tsx` | 5 | manual |
| `auth/VerificationSuccess.stories.tsx` | 5 | manual |
| `chat/entry` | 5 | manual |
| `dashboard/game-night-hero.tsx` | 5 | manual |
| `features/game-chat` | 5 | DS-10 |
| `game-night/ResumeSessionPanel.tsx` | 5 | manual |
| `legal/CookieConsentBanner.tsx` | 5 | manual |
| `onboarding/InterestsStep.tsx` | 5 | manual |
| `session/ScoreboardPage.tsx` | 5 | manual |
| `session/SessionSnapshotPanel.tsx` | 5 | manual |
| `state/BoardStateEditor.tsx` | 5 | manual |
| `toolkit/Randomizer.tsx` | 5 | manual |
| `ui/overlays` | 5 | manual |
| `upload/UploadQueueItem.tsx` | 5 | manual |
| `app/(auth)/login` | 4 | manual |
| `app/(auth)/register` | 4 | manual |
| `app/(auth)/reset-password` | 4 | manual |
| `app/(auth)/setup-account` | 4 | manual |
| `admin/AlertsBanner.tsx` | 4 | manual |
| `admin/invitations` | 4 | manual |
| `admin/rag-quality-dashboard.tsx` | 4 | manual |
| `auth/RegisterForm.stories.tsx` | 4 | manual |
| `comments/InlineCommentIndicator.tsx` | 4 | manual |
| `diff/DiffViewerEnhanced.tsx` | 4 | manual |
| `editor/EditorToolbar.tsx` | 4 | manual |
| `errors/RouteErrorBoundary.stories.tsx` | 4 | manual |
| `game-night/QuickActions.tsx` | 4 | manual |
| `library/UsageWidget.tsx` | 4 | manual |
| `loading/TypingIndicator.tsx` | 4 | manual |
| `onboarding/FirstAgentStep.tsx` | 4 | manual |
| `onboarding/PasswordStep.tsx` | 4 | manual |
| `onboarding/ProfileStep.tsx` | 4 | manual |
| `pdf/PdfProcessingProgressBar.stories.tsx` | 4 | manual |
| `pwa/InstallPrompt.tsx` | 4 | manual |
| `pwa/UpdatePrompt.tsx` | 4 | manual |
| `rag-dashboard/builder` | 4 | manual |
| `session/KbProcessingBanner.tsx` | 4 | manual |
| `toolkit-drawer/ToolkitDrawer.tsx` | 4 | manual |
| `ui/detail-layout` | 4 | manual |
| `ui/navigation` | 4 | manual |
| `app/(authenticated)/agents` | 3 | manual |
| `admin/charts` | 3 | manual |
| `auth/AuthModal.stories.tsx` | 3 | manual |
| `chat-unified/AiLoadingState.tsx` | 3 | manual |
| `chat-unified/ChatHistoryDrawer.tsx` | 3 | manual |
| `chat-unified/VoiceTranscriptOverlay.tsx` | 3 | manual |
| `game-detail/TypingIndicator.tsx` | 3 | manual |
| `game-night/GameKbBadge.tsx` | 3 | manual |
| `game-night/SaveCompleteDialog.tsx` | 3 | manual |
| `landing/LandingFooter.tsx` | 3 | manual |
| `layout/UserShell` | 3 | manual |
| `library/PdfUploadSheet.tsx` | 3 | manual |
| `onboarding/tour` | 3 | manual |
| `onboarding/WelcomeChecklist.tsx` | 3 | manual |
| `prompt/LazyPromptEditor.tsx` | 3 | manual |
| `prompt/PromptEditor.tsx` | 3 | manual |
| `session/ParticipantList.tsx` | 3 | manual |
| `session/SessionSummaryCard.tsx` | 3 | manual |
| `shared-games/AdvancedFilterPanel.tsx` | 3 | manual |
| `toolkit/Timer.tsx` | 3 | manual |
| `ui/shared-games` | 3 | manual |
| `ui/sheet.tsx` | 3 | manual |
| `versioning/VersionTimeline.tsx` | 3 | manual |
| `app/(authenticated)/dashboard` | 2 | DS-4 |
| `app/(authenticated)/games` | 2 | manual |
| `app/(authenticated)/players` | 2 | manual |
| `app/(authenticated)/profile` | 2 | manual |
| `app/(chat)/chat` | 2 | manual |
| `admin/ApprovalStatusFilter.tsx` | 2 | manual |
| `agent/AgentCharacterSheet.tsx` | 2 | manual |
| `agent/AgentMessage.tsx` | 2 | manual |
| `agent/layout` | 2 | manual |
| `auth/RequireRole.tsx` | 2 | manual |
| `catalog/GamesFilterPanel.tsx` | 2 | manual |
| `chat-unified/AgentSwitchDialog.tsx` | 2 | manual |
| `chat-unified/ChatInputArea.tsx` | 2 | manual |
| `chat-unified/CitationSheet.tsx` | 2 | manual |
| `chat-unified/EmbeddedChatView.tsx` | 2 | manual |
| `comments/CommentThread.tsx` | 2 | manual |
| `dashboard/AddToLibraryModal.tsx` | 2 | manual |
| `dashboard/GreetingStrip.tsx` | 2 | manual |
| `dashboard/recent-sessions.tsx` | 2 | manual |
| `dashboard/WelcomeHero.tsx` | 2 | manual |
| `errors/RouteErrorBoundary.tsx` | 2 | manual |
| `forms/FormDescription.tsx` | 2 | manual |
| `game-state/MeeplePlayerStateCard.tsx` | 2 | manual |
| `layout/AdminSideDrawer` | 2 | manual |
| `layout/HubLayout` | 2 | manual |
| `layout/SideDrawer` | 2 | manual |
| `layout/ViewModeToggle.tsx` | 2 | manual |
| `library/LibraryQuickStats.tsx` | 2 | manual |
| `library/OwnershipConfirmationDialog.tsx` | 2 | manual |
| `library/OwnershipDeclarationDialog.tsx` | 2 | manual |
| `library/PdfProcessingStatus.tsx` | 2 | manual |
| `library/RagAccessBadge.tsx` | 2 | manual |
| `library/ShelfCard.tsx` | 2 | manual |
| `pdf/PdfProgressBar.tsx` | 2 | manual |
| `pdf/PdfUploadForm.stories.tsx` | 2 | manual |
| `pdf/PdfViewerModal.tsx` | 2 | manual |
| `pdf/progress-card.tsx` | 2 | manual |
| `pdf/progress-modal.tsx` | 2 | manual |
| `profile/AvatarUpload.tsx` | 2 | manual |
| `rag-dashboard/reference` | 2 | manual |
| `session/QrInviteSheet.tsx` | 2 | manual |
| `session/ScoreProposalCard.tsx` | 2 | manual |
| `session/SessionParticipantsList.tsx` | 2 | manual |
| `session/SessionToolLayout.tsx` | 2 | manual |
| `shared-games/SharedGameSearchFilters.tsx` | 2 | manual |
| `showcase/stories` | 2 | manual |
| `upload/MultiFileUpload.tsx` | 2 | manual |
| `app/(authenticated)/game-nights` | 1 | manual |
| `app/(authenticated)/settings` | 1 | manual |
| `app/(chat)/error.tsx` | 1 | manual |
| `admin/AdminAuthGuard.tsx` | 1 | manual |
| `admin/debug-chat` | 1 | manual |
| `admin/FeatureFlagsTab.tsx` | 1 | manual |
| `admin/secrets` | 1 | manual |
| `admin/ui-library` | 1 | manual |
| `admin/UserActivityTimeline.tsx` | 1 | manual |
| `auth/TwoFactorSetup.tsx` | 1 | manual |
| `chat-unified/ChatMessageList.tsx` | 1 | manual |
| `chat-unified/ChatMobile.tsx` | 1 | manual |
| `comments/CommentForm.tsx` | 1 | manual |
| `dashboard/agents-section.tsx` | 1 | manual |
| `dashboard/empty-states.tsx` | 1 | manual |
| `dashboard/HeroBanner.tsx` | 1 | manual |
| `dashboard/incomplete-session-hero.tsx` | 1 | manual |
| `dashboard/RecentGamesRow.tsx` | 1 | manual |
| `dashboard/sheet` | 1 | manual |
| `dialogs/OwnershipConfirmDialog.tsx` | 1 | manual |
| `editor/ViewModeToggle.tsx` | 1 | manual |
| `features/chat` | 1 | manual |
| `features/common` | 1 | manual |
| `features/play` | 1 | manual |
| `features/player-detail` | 1 | DS-9 |
| `features/players` | 1 | DS-9 |
| `game/rulebook-section.tsx` | 1 | manual |
| `game-detail/mobile` | 1 | manual |
| `game-detail/SplitViewLayout.tsx` | 1 | manual |
| `game-night/PlayerSetupDialog.tsx` | 1 | manual |
| `game-state/GameStateViewer.stories.tsx` | 1 | manual |
| `game-state/ResourceTracker.stories.tsx` | 1 | manual |
| `game-state/StateHistoryTimeline.stories.tsx` | 1 | manual |
| `layout/ContextualHand` | 1 | manual |
| `layout/MobileCTAPill.tsx` | 1 | manual |
| `layout/PageHeader.tsx` | 1 | manual |
| `layout/SearchOverlay.tsx` | 1 | manual |
| `library/AgentDrawerSheet.tsx` | 1 | manual |
| `library/ChatDrawerSheet.tsx` | 1 | manual |
| `library/KbDrawerSheet.tsx` | 1 | manual |
| `library/labels` | 1 | manual |
| `library/SessionDrawerSheet.tsx` | 1 | manual |
| `library/TrendingGamesRow.tsx` | 1 | manual |
| `onboarding/WelcomeWizard.tsx` | 1 | manual |
| `pdf/PdfErrorCard.stories.tsx` | 1 | manual |
| `pdf/PdfMetricsDisplay.tsx` | 1 | manual |
| `pdf/PdfProgressBar.stories.tsx` | 1 | manual |
| `pdf/PdfStatusBadge.stories.tsx` | 1 | manual |
| `pdf/PdfStatusTimeline.tsx` | 1 | manual |
| `pdf/PdfViewerModal.stories.tsx` | 1 | manual |
| `pdf/progress-toast.tsx` | 1 | manual |
| `pipeline-builder/PipelineCanvas.tsx` | 1 | manual |
| `pwa/OfflineIndicator.tsx` | 1 | manual |
| `rag-dashboard/DashboardNav.tsx` | 1 | manual |
| `rag-dashboard/metrics` | 1 | manual |
| `rag-dashboard/RagHero.tsx` | 1 | manual |
| `search/SearchModeToggle.tsx` | 1 | manual |
| `session/CameraToolContent.tsx` | 1 | manual |
| `session/GameStateDisplay.tsx` | 1 | manual |
| `session/InviteSession.tsx` | 1 | manual |
| `session/MeepleParticipantCard.tsx` | 1 | manual |
| `session/PauseSessionDialog.tsx` | 1 | manual |
| `session/PhotoUploadModal.tsx` | 1 | manual |
| `session/RagQuickLinks.tsx` | 1 | manual |
| `session/TurnIndicatorBar.tsx` | 1 | manual |
| `session/WheelSpinner.tsx` | 1 | manual |
| `state/LedgerTimeline.stories.tsx` | 1 | manual |
| `toolkit/WhiteboardWidget.tsx` | 1 | manual |
| `ui/auth-card` | 1 | manual |
| `ui/invites` | 1 | manual |
| `ui/join` | 1 | manual |
| `ui/meeple` | 1 | manual |
| `ui/pricing-card` | 1 | manual |
| `versioning/VersionTimelineFilters.tsx` | 1 | manual |

## Top 20 files

| File | Violations |
|---|---|
| `src/app/admin/(dashboard)/agents/inspector/page.tsx` | 123 |
| `src/stories/Animations.stories.tsx` | 79 |
| `src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/analyses/page.tsx` | 59 |
| `src/components/session/WhiteboardTool.tsx` | 58 |
| `src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx` | 55 |
| `src/components/admin/command-center/CommandCenterDashboard.tsx` | 54 |
| `src/components/loading/SkeletonLoader.tsx` | 54 |
| `src/components/admin/knowledge-base/processing-metrics.tsx` | 51 |
| `src/components/play-records/NewPlayRecordSheet.tsx` | 51 |
| `src/app/(authenticated)/library/private/[privateGameId]/toolkit/configure/client.tsx` | 50 |
| `src/app/admin/(dashboard)/knowledge-base/embedding/page.tsx` | 50 |
| `src/app/admin/(dashboard)/users/[id]/page.tsx` | 48 |
| `src/components/admin/knowledge-base/upload-zone.tsx` | 48 |
| `src/components/admin/knowledge-base/rag-pipeline-flow.tsx` | 47 |
| `src/app/admin/(dashboard)/knowledge-base/documents/page.tsx` | 46 |
| `src/components/rag-dashboard/config/RagConfigurationForm.tsx` | 44 |
| `src/components/session/Scoreboard.tsx` | 44 |
| `src/components/admin/knowledge-base/upload-settings.tsx` | 43 |
| `src/components/admin/shared-games/SharedGameExtraMeepleCard.tsx` | 43 |
| `src/app/admin/(dashboard)/agents/config/AgentModelsTabContent.tsx` | 41 |

## Notes

- Rule is in `warn` mode during DS-3 inventory + DS-4..DS-11 cluster migrations.
- Switched to `error` in DS-12 once `pnpm lint:tokens --max-warnings 0` is green.
- Companion JSON: [`2026-05-12-token-violations.json`](./2026-05-12-token-violations.json).
