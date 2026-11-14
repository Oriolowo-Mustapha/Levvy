# Levvy implementation report

14 November 2026 · Base commit `9e06e2e81ba9f692a7ae01eefa066c8500f08ced`

The screenshot-led application is implemented in the existing Next.js/.NET repository. The source is ready for review and deployment testing. It has not been pushed or deployed. Local browser checks use explicit service-contract fixtures; production provider-backed functionality is not claimed as verified.

## Pages implemented

| Route | Behavior |
| --- | --- |
| `/` | Dotted audio-intelligence visual, assistant heading, paired language preferences, real room-code join request, host entry and most recent authenticated lecture when available. |
| `/login` | Email login through `/auth/login`; optional Google Identity integration when configured. |
| `/signup` | Email registration through `/auth/register`, role selection and optional Google Identity sign-in. |
| `/dashboard` | Actual profile, hosted/attended lectures, search/filtering, study packs, profile updates, preferred language and caption/accessibility preferences. |
| `/host` | Authenticated creation, room code/link copying, local QR generation, microphone/tab audio selection, actual audio input level, recording timer/status, WebM upload, recording save and session-end confirmation. |
| `/session/[code]` | Three-zone desktop workspace and stacked mobile view; synchronized original/translated captions, live socket state, caption size/language, transcript, Ask Levvy and recording receipts. |
| `/session/[code]/pack` | Backend summary with timestamp buttons, keyboard-operated flashcards with receipts, transcript and recording controls; unavailable/generating states support retry. |

## Reusable components

`LevvyLogo`, `AppHeader`, `Icon`, `BrowserReady`, `PrimaryActionButton`, `SecondaryActionButton`, `LanguageSelector`, `LanguagePairSelector`, `AudioVisualizer`, `RecordingTimer`, `RecordingStatus`, `ConnectionIndicator`, `EmptyState`, `LoadingState`, `ErrorState`, `RoomCodeInput`, `AudioReceiptChip`, `CopyButton`, `CaptionSizeControl`, `LiveCaptionCard`, `OriginalTranscriptCard`, `TranslatedTranscriptCard`, `TranscriptFeed`, `AudioPlaybackControl`, `AskLevvyPanel`, `SessionCard`, `StudyPackCard`, `AuthForm`, `GoogleSignIn`, `JoinQr`, `RecentLecture` and `Flashcard`.

Shared lifecycle modules are `useLecture` and `useRecorder`; typed DTOs stay in `api.ts`, with authenticated requests and consistent errors in `client.ts`. Fetching is not spread through multiple unrelated URL/config implementations.

## Visual changes

The three reference compositions drive the product: the assistant entry uses a dotted orb, centered heading, generous whitespace, rounded language selectors and black pill actions; the host uses a restrained listening composition with input-driven dotted visualization; the viewer pairs a light original-speech card with a deep charcoal translation card. The screenshot is not embedded in the application. All surfaces are React/HTML/CSS.

Desktop retains the left navigation, central caption/transcript area and right assistant/preferences rail. Mobile uses the same components vertically, with wrapping navigation and readable caption sizes. Dashboard, authentication and study-pack pages share the same neutral palette, typography, spacing, borders and controls.

## Functional integration

The UI calls the actual session, join, transcript, ask, end, recording, pack, email auth, Google auth, dashboard and profile contracts. It does not seed a demo automatically or substitute fixtures for production responses. Test fixtures live only in the test suite.

Caption messages are validated before entering state, merged by source offset and deduplicated. Translations are retained by language plus offset; changing language sends `set_lang` on the same socket. Reconnection uses capped exponential backoff with jitter. Socket, polling and pending initial fetches are cleaned up. Ended sessions stop reconnecting. Initial service failure is visible.

Ask Levvy submits against the lecture API. An answer is labeled supported only when status is `ok` and receipts match transcript offsets. Invalid receipts are hidden. Valid receipt clicks seek `offsetMs / 1000` and request playback; missing recordings produce an honest unavailable state. Transcript playback highlights the corresponding segment.

Microphone and tab capture use actual browser MediaRecorder and Web Audio APIs. Capability/MIME checks precede recording. Each ASR frame is an independently initialized WebM chunk; a second recorder produces continuous playback audio. The visualizer reads real input samples. Tracks, contexts, timers and animation frames are released on stop/unmount; permission denial, silence and connection failures have distinct feedback. The host saves audio through the existing raw-body upload contract and waits for outstanding ASR acknowledgements before ending.

## Backend changes and boundaries

- Added `POST /sessions/{sessionId}/ws-ticket`. A valid owner Bearer token and a live session are required. Random tickets are session-scoped, expire after 30 seconds and are single use. Browser WebSocket URLs contain a short-lived ticket, not a user JWT or shared host secret.
- Host upgrades reject unauthorized claims with 403 rather than silently downgrading to a viewer.
- Recording uploads now use the existing `HostAccess` authorization rule.
- Host sockets receive additive `asr_status` processing/ready/error frames. Caption frames include detected `sourceLanguage` when available. Existing speech, translation, storage and background-job architecture remains in use.
- Added ticket expiry, cross-session and replay tests. No provider credentials or security checks were disabled.

The ticket store is process-local; a multi-instance deployment needs affinity or a shared implementation. Existing anonymous/demo HostAccess behavior is retained. ASR completion acknowledgements do not guarantee that all existing Hangfire persistence jobs have committed before study-pack generation. This timing needs a real-service rehearsal.

## Accessibility

Added a skip link, semantic forms/navigation, explicit labels, visible keyboard focus, status/error announcements, keyboard-operable flashcards and a native modal confirmation. Captions support four sizes, correct known-language attributes and automatic text direction. Reduced-motion preferences are honored. The language bridge focuses the actual caption selector, and source-language controls are disabled during recording. No fake audio signal or unsupported browser success state is displayed.

Automated axe WCAG 2 A/AA, 2.1 AA and 2.2 AA checks run across all seven routes at 390px and 1440px. Passing automated scans does not replace a full accessibility audit or testing with Deaf/HoH users and assistive technology.

## Verification results

| Check | Actual result |
| --- | --- |
| Final lockfile `npm ci` in a clean directory | Passed; 385 packages installed, zero reported vulnerabilities. |
| `npm run lint` | Passed. |
| `npx tsc --noEmit` | Passed. |
| `npm run build` | Passed, including standalone output; postbuild copies static/public assets. |
| Standalone `npm start` | Started successfully and served the tested production application. |
| Playwright browser suite | 13 tests passed against the standalone production app using explicit REST/WebSocket fixtures and browser-generated synthetic microphone input. |
| Responsive checks | Seven routes at 360, 390, 430, 768, 1024, 1440 and 1920px; active host also checked at all seven widths. No horizontal overflow in these checks. |
| Automated accessibility | Seven routes at two sizes; no violations under the selected WCAG tags. |
| Backend API build | Passed with zero warnings/errors. |
| Backend tests | 48 passed, zero failed or skipped, including three new ticket tests. |
| Public backend health/session | HTTP 502 Bad Gateway for `/health` and `/sessions/K4F-8Q2`; no production data was changed. |

Browser tests cover room-code/language submission, live caption synchronization and deduplication, language changes, socket recovery, ungrounded-answer rejection, receipt seeking, WebM chunk headers and recording upload, stopped audio tracks, end confirmation, permission denial, profile updates, login/registration, flashcard keyboard interaction, error states, responsiveness and axe scans. Page-error checks passed. Expected failed recording/API requests are exercised intentionally in error tests.

A transient Windows sandbox directory-creation race initially caused EPERM during builds. A temporary, workspace-only Node preload retried those filesystem operations; the normal standalone configuration was preserved. This helper is not shipped as application code. Verification used a locally installed .NET 10 SDK and isolated NuGet configuration/cache; no personal NuGet config was needed.

## Important files

- `frontend/src/app/page.tsx`, `globals.css`, `layout.tsx`: assistant entry and shared design/accessibility foundation.
- `frontend/src/app/host/page.tsx`: recording and lecture-management flow.
- `frontend/src/app/session/[code]/page.tsx`, `layout.tsx`, `pack/page.tsx`: live workspace, dynamic-route loading boundary and study pack.
- `frontend/src/app/dashboard/page.tsx`, `login/page.tsx`, `signup/page.tsx`: account workspace and authentication routes.
- `frontend/src/components/ui.tsx`, `lecture.tsx`, `auth-form.tsx`, `join-qr.tsx`, `recent-lecture.tsx`: reusable components.
- `frontend/src/lib/client.ts`, `use-lecture.ts`, `use-recorder.ts`: service requests, realtime state and browser media lifecycle.
- `frontend/tests/lecture.spec.ts`, `playwright.config.ts`: browser/contract/accessibility verification.
- `frontend/scripts/prepare-standalone.mjs`, `package.json`, `package-lock.json`: runnable standalone packaging and dependencies.
- `backend/src/Levvy.Api/Endpoints/WebSocketEndpoints.cs`, `SessionEndpoints.cs`, `Hubs/LectureSessionHub.cs`, `Program.cs`: authenticated browser hosting, upload authorization and ASR feedback.
- `backend/src/Levvy.Infrastructure/Authentication/WebSocketTicketStore.cs`, `backend/tests/Levvy.Tests/WebSocketTicketTests.cs`: scoped ticket lifecycle and tests.
- `.github/workflows/frontend-ci.yml`: production browser/accessibility suite added to frontend CI.
- `frontend/.env.example`, `frontend/README.md`, root architecture/design/plan/progress/service documentation: configuration, operation and accurate completion status.

## Remaining blockers and unverified behavior

The public backend outage prevents real end-to-end validation of PostgreSQL, Groq/Deepgram, translation, grounded AI answers, object storage, background persistence and study-pack generation. No working service credentials or local PostgreSQL instance were provided. Google Identity requires a configured web client ID and real OAuth testing. Real microphone hardware, browser-tab/system audio, multi-device operation and cross-browser behavior remain unverified; automated microphone tests use synthetic browser audio.

Existing recording upload stores WebM audio but does not transcribe uploaded files. The UI says so and offers only that supported storage flow. Pause/resume, translated speech synthesis and speaker metadata are not fabricated. Historical translations cannot be recovered from the current REST transcript endpoint. Other source MIME types need backend playback/content-type support before being offered. Backend timestamp/persistence behavior across disconnects and after the final chunk needs the live rehearsal.

Screenshots of live/host/study pages use explicit test lectures. They show the implemented UI, not a claim of a live production class or service success. Earlier plan features for meeting-bot URL controls and automatic demo seeding were outside this screenshot-led journey and remain separate backlog items.
