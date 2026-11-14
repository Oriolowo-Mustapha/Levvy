# Levvy — Progress Tracker

> **Last updated:** 2026-11-14  
> **Target:** HackGlobal 2026 Demo (15 Nov 2026)  
> **Live URL:** `https://levvy.tech`

---

## Overall Status

| Area | Status | Notes |
|---|---|---|
| Monorepo & Tooling | 🟢 Done | `backend/` (.NET 10 Clean Arch) + `frontend/` (Next.js standalone) + GitHub Actions CD (`deploy-vultr.yml`) |
| Backend Domain & Contracts | 🟢 Done | Entities (`User`, `Session`, etc.), Result pattern, MediatR CQRS, FluentValidation, Domain Events |
| Backend Auth & Dashboard | 🟢 Done | Email/password + Google Sign-In/Sign-Up (`POST /auth/google`), JWT, User Profile & Dashboard (`GET /users/me/dashboard`) |
| Backend Core & AI | 🟢 Done | Groq LLM/ASR, Deepgram AI (`nova-2`, auto-lang, `ASR_PROVIDER`), S3 AudioStore (signed PUT upload + signed GET read fallback for receipts), Hangfire, Npgsql Migrations, OpenAPI/Swagger (`/swagger`) |
| Google Meet & Online Class Bot | 🟢 Done | **Mubarak (Dev B)** — `IMeetingBotConnector` + `GoogleMeetBotConnector` (Recall.ai-style), `OnlineClassBotJob` dispatch/monitor, `POST /sessions/online-class` · `/connect-meet` · `/meet-webhook`, `CaptionFinalized` webhook seam, migration `AddOnlineClassFields`; Mode B tab-audio fallback stays the zero-key option |
| Frontend Foundation (`api.ts`) | 🟢 Done | Typed Auth, Dashboard, Session REST + WebSocket client (`frontend/src/lib/api.ts`), design tokens (`globals.css`), `output: "standalone"` |
| Frontend Pages | 🟡 In Progress (Frontend Dev) | Auth, User Dashboard, Landing/Join, Host Console, Viewer Workspace, Study Pack UI pages |
| Vultr Infrastructure | 🟢 Provisioned | Compute (`64.177.55.161`), Managed Postgres, Object Storage (`levvy-recordings`), `levvy.tech` HTTPS/WSS |
| Security & Reliability Audit Fixes | 🟢 Done | Full audit (14 Nov) → all backend findings fixed on `fix/audit-hardening`: host-role auth on `/ws` + `/end` + `/connect-meet`, identity-spoofing fix, JWT secret startup guard, rate limiting, CORS allow-list, token-gated Hangfire dashboard, HTTP timeouts, ASR circuit breaker, idempotent caption persistence, streamed S3 uploads/downloads, tighter receipt grounding (±500ms), bot-monitor attempt cap, env config (`backend/.env.example` section 7). Frontend audit items tracked separately with the frontend dev. |
| Demo & Validation | ⚪ Not Started | 2 audio fixtures + 10 user convos/country + 5-min pitch rehearsal |

---

## Backend (`backend/`)

### Shared Baseline
- [x] Clean Architecture solution (`Domain`, `Application`, `Infrastructure`, `Api`, `Tests`)
- [x] Domain entities (`User`, `Session`, `Participant`, `TranscriptSegment`, `Question`, `Answer`, `AnswerCitation`, `StudyPack`, `Flashcard`)
- [x] `LevvyDbContext` + **Npgsql-only** persistence (`ConnectionStrings:Levvy` / `DATABASE_URL`; local dev default `Host=localhost;Database=levvy`)
- [x] Standard EF Core migrations (`Add-Migration` / `Update-Database` -> `20261114031741_InitialCreate`, `AddUsersAndDashboard`)
- [x] OpenAPI (`/openapi/v1.json`) + interactive Swagger UI (`/swagger`) wired in `Program.cs` & `SwaggerEndpoints`
- [x] Baseline DI, `.env`, and Result pattern tests passing (`PipelineTests`)

### Oriolowo Mustapha (Dev A) — AI/LLM, ASR, WebSockets, Auth & User Dashboard
- [x] `GroqLlmClient` (`ITranslator` + `ILlmAnswerer` with `gpt-oss-120b` / `llama-3.3-70b-versatile`)
- [x] `GroqAsrStreamer` (`whisper-large-v3-turbo`) + `chunkStartOffsetMs` timeline math
- [x] `DeepgramAsrStreamer` (`nova-2` / configurable `DEEPGRAM_MODEL`, `detected_language` extraction, word-level fallback timestamps)
- [x] `ResilientAsrStreamer` (`ASR_PROVIDER=groq|deepgram`, automatic bidirectional failover between Groq and Deepgram)
- [x] `/ws/{sessionId}` WebSocket hub (`LectureSessionHub`) + per-viewer language registry + `CaptionFinalized` fan-out
- [x] **Email/Password + Google Auth** (`PasswordHasher`, `JwtTokenService`, `GoogleAuthVerifier`, `RegisterUserCommand`, `LoginUserCommand`, `GoogleAuthCommand`, `/auth/*` endpoints)
- [x] **User Profile & Dashboard** (`UserRepository`, `UpdateUserProfileCommand`, `GetUserDashboardQuery`, `/users/me/dashboard`, `/users/me/profile`, `Participant.UserId` tracking)
- [x] Unit tests for ASR/LLM & Auth/Google/Dashboard (`AsrAndLlmTests`, `AuthAndDashboardTests`)

### Adeyemi Mubarak (Dev B) — Postgres, Object Storage, CQRS, Hangfire & Google Meet Bot
- [x] `SessionRepository` EF Core implementation + Npgsql migrations
- [x] `VultrAudioStore` (`IAudioStore` S3-compatible against Vultr Object Storage) — upload via signed PUT + signed GET read fallback
- [x] `CreateSessionCommand`, `JoinSessionCommand`, `EndSessionCommand` handlers + FluentValidation validators
- [x] `GetSessionByCodeQuery`, `GetTranscriptQuery`, `GetStudyPackByCodeQuery` handlers
- [x] `AskQuestionCommand` handler + `AskQuestionCommandValidator` (`cite-or-say-not-covered`)
- [x] Hangfire background jobs (`CaptionPersistenceJob`, `StudyPackGenerationJob` with 3 retries, `StaleSessionCleanupJob`, `RecurringJobRegistrationService`) + `/jobs` dashboard
- [x] Minimal API REST endpoints (`/health`, `/sessions`, `/sessions/demo`, `/join`, `/ask`, `/transcript`, `/end`, `/pack`, `/audio`) with OpenAPI summaries
- [x] End-to-end CQRS + Hangfire worker + EF persistence tests (`PipelineTests`, `SessionRepositoryTests`)
- [x] **Google Meet & Online Class Bot Integration (`plan.md` Phase 2):**
  - [x] Add `MeetingPlatform`, `ExternalMeetingUrl`, `BotStatus`, `ExternalBotId` to `Session` + EF Core migration (`20261114052538_AddOnlineClassFields`)
  - [x] Implement `IMeetingBotConnector` / `GoogleMeetBotConnector` (Recall.ai-style, `MEETING_BOT_API_KEY`) + Hangfire `OnlineClassBotJob` (dispatch + 30s monitor loop)
  - [x] Add `ConnectOnlineClassCommand`, `POST /sessions/online-class`, `POST /sessions/{id:guid}/connect-meet`, & `POST /sessions/{id:guid}/meet-webhook` (`CaptionFinalized` seam + `call_ended` → `EndSessionCommand` → study pack)
  - [x] Wire live bot transcript ingestion into the `CaptionFinalized` seam (persist via `CaptionPersistenceJob`, fan out via `LectureSessionHub`, study pack on call end); audio recording upload path unchanged (`/sessions/{id}/audio`)
  - [x] 22 new tests in `OnlineClassTests` (URL validation, bot lifecycle, webhook captions, study pack trigger) — suite 43/43

---

## Frontend (implementation update, 14 November 2026)

The screenshot-led application is implemented. The checks below describe code and local browser verification, not a successful production-service deployment.

- [x] Entry page with language preferences, anonymous room-code joining and host navigation.
- [x] Email login/sign-up and optional configured Google Identity component (real Google OAuth remains unverified).
- [x] Authenticated dashboard, session search/filter, study-pack links and saved profile/accessibility preferences.
- [x] Host console: room creation, copyable link/code, QR, mic/tab source selection, WebM capture, input meter, timer, recording upload and end confirmation.
- [x] Three-zone desktop viewer and stacked mobile original/translated caption cards, offset matching, reconnect/backoff, transcript, Ask Levvy and recording receipts.
- [x] Study-pack summary citations, keyboard-operated flashcards and transcript playback.
- [x] Shared neutral design system; screen-reader labels, focus rings, reduced motion and caption sizes.
- [x] All seven requested viewport widths checked; automated axe AA scans of seven routes at mobile and desktop sizes.
- [x] Build, TypeScript, lint, frontend contract/browser tests and backend unit tests pass (see IMPLEMENTATION-REPORT.md).
- [x] Short-lived single-use session-scoped WebSocket tickets; authenticated recording uploads; host ASR status frames.
- [ ] Actual multi-device live transcription/translation/QA/storage/study-generation rehearsal with configured PostgreSQL and provider credentials.
- [ ] Real microphone/tab sharing and Google OAuth testing across supported browsers and devices.

Public API health and session requests returned HTTP 502 during verification. Upload currently stores a WebM recording; the backend does not provide file-transcription ingestion. Pause/resume is not offered. Existing demo seeding and meeting-bot URL controls from the earlier plan are not added to this task's frontend.

---

## Infrastructure & Pre-Demo Checklist

- [x] Git repo initialized & pushed to GitHub (`main`) with automated Vultr CD workflow
- [x] Vultr Object Storage bucket (`levvy-recordings`) + keys configured
- [x] Vultr Managed Postgres provisioned + connection string set (`DATABASE_URL`; app is Npgsql-only)
- [x] Vultr Compute deployed (`64.177.55.161`) with `levvy.tech` Let's Encrypt HTTPS + WSS
- [ ] Update Nginx regex on VM to include `auth|users|swagger|openapi` (`location ~ ^/(health|auth|users|sessions|jobs|swagger|openapi)`) and verify `https://levvy.tech/swagger`
- [ ] Two audio fixtures tested end-to-end (with both Groq and `ASR_PROVIDER=deepgram`)
- [ ] Full 5-minute demo rehearsal passed twice
