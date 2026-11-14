# Levvy — Build Plan (HackGlobal 2026)

> **Build Window:** 14–15 Nov 2026 (starts 23:00 on 13 Nov).  
> **Live Deployment:** `https://levvy.tech` (Vultr Compute `64.177.55.161` + Vultr Managed PostgreSQL + Vultr Object Storage)

---

## Phase 0 — Pre-Build Prep ✅
- [x] Freeze stack & WebSocket/REST contract (`README.md`, `EXTERNAL-SERVICES.md`)
- [x] Scaffold .NET 10 Clean Architecture in `backend/` + verify `dotnet test`
- [x] Scaffold Next.js + TS + Tailwind in `frontend/`
- [x] Create `architecture.md`, `design.md`, `plan.md`, `progresstracker.md`
- [x] Configure Groq API key & Deepgram backup/primary ASR keys in `.env` (`DotEnvLoader`)
- [ ] Prepare 2 demo audio fixtures (10-min lecture + 2-min teammate clip)

---

## Phase 1 — Repo Init & Core Backend Build ✅

Git repository initialized at `c:\Users\ADMIN\source\repos\Levvy`, pushed to `origin/main`, and **Vultr Managed PostgreSQL** + **Vultr Object Storage (`levvy-recordings`)** + **Vultr Compute (`64.177.55.161`)** provisioned.

### Oriolowo Mustapha (Backend Dev A) — AI/LLM, ASR Hot Pipe, WebSocket Hub, Auth & User Dashboard ✅
- **Patterns:** Strategy (`IAsrStreamer`, `ITranslator`, `ILlmAnswerer`), Hot-Pipe Bypass (no MediatR on raw audio frames), Observer (`CaptionFinalized` publisher), Fallback/Resilience (`ResilientAsrStreamer`), Result Pattern, JWT Bearer + Google OAuth2.
- **Files owned:** `Levvy.Infrastructure/Asr/`, `Levvy.Infrastructure/Llm/`, `Levvy.Infrastructure/Authentication/`, `Levvy.Api/Hubs/`, `AuthEndpoints.cs`, `UserEndpoints.cs`.
- **Tasks:**
  1. [x] Implement `GroqLlmClient` (`ITranslator` + `ILlmAnswerer`) calling `POST /openai/v1/chat/completions` (`gpt-oss-120b` primary, `llama-3.3-70b-versatile` fallback) with strict JSON citation output (`cite-or-say-not-covered`).
  2. [x] Implement `GroqAsrStreamer` (`IAsrStreamer`) calling `POST /openai/v1/audio/transcriptions` (`whisper-large-v3-turbo`) with `chunkStartOffsetMs` timeline math.
  3. [x] Enhance `DeepgramAsrStreamer` & `ResilientAsrStreamer` (`ASR_PROVIDER=groq|deepgram`, configurable `DEEPGRAM_MODEL`, `detected_language` extraction, automatic Groq <-> Deepgram failover).
  4. [x] Build `/ws/{sessionId}?lang=xx` WebSocket hub (`LectureSessionHub`) + per-session connection registry (`CaptionFinalized` fan-out & private `AskQuestionCommand` answers).
  5. [x] Implement **User Auth & Google Sign-In/Sign-Up** (`User` entity with `FirstName`, `LastName`, `Email`, `Role`, `Institution`, `Department`, `PreferredLanguage`, `AccessibilityMode`, `CaptionFontSize`, PBKDF2 `PasswordHasher`, HS256 `JwtTokenService`, `GoogleAuthVerifier`, `POST /auth/register`, `POST /auth/login`, `POST /auth/google`, `GET /auth/me`).
  6. [x] Implement **User Profile & Dashboard** (`IUserRepository` / `UserRepository`, `UpdateUserProfileCommand`, `GetUserDashboardQuery`, `GET /users/me/dashboard`, `PUT /users/me/profile`, session attendance linking via `Participant.UserId`).
  7. [x] Write unit tests in `Levvy.Tests` (`AsrAndLlmTests`, `AuthAndDashboardTests`).

### Adeyemi Mubarak (Backend Dev B) — Vultr Postgres Persistence, Object Storage, CQRS, Hangfire & REST API ✅
- **Patterns:** Repository, CQRS + MediatR, Result Pattern, Pipeline Validation (`FluentValidation`), Domain Event Handlers (`INotificationHandler`), Hangfire Background Jobs, Minimal APIs + OpenAPI/Swagger.
- **Files owned:** `Levvy.Infrastructure/Persistence/`, `Levvy.Infrastructure/Storage/`, `Levvy.Infrastructure/BackgroundJobs/`, `Levvy.Application/`, `Levvy.Api/Endpoints/`.
- **Tasks:**
  1. [x] Implement `SessionRepository` (EF Core against **Vultr Managed PostgreSQL**) + `LevvyDbContextFactory` + standard EF Core `Add-Migration` (`20261114031741_InitialCreate`) & `Update-Database` / `db.Database.Migrate()`.
  2. [x] Implement `VultrAudioStore` (`IAudioStore`) against **Vultr Object Storage** (`levvy-recordings` S3-compatible).
  3. [x] Implement `CreateSessionCommand`, `JoinSessionCommand`, `EndSessionCommand` handlers + FluentValidation validators.
  4. [x] Implement `GetSessionByCodeQuery`, `GetTranscriptQuery`, & `GetStudyPackByCodeQuery` handlers.
  5. [x] Implement `AskQuestionCommand` handler + validator (enforcing `cite-or-say-not-covered`).
  6. [x] Implement Hangfire background jobs (`CaptionPersistenceJob`, `StudyPackGenerationJob`, `StaleSessionCleanupJob`, `RecurringJobRegistrationService`) & Hangfire dashboard at `/jobs`.
  7. [x] Wire Minimal API REST endpoints (`/health`, `/sessions`, `/sessions/demo`, `/join`, `/ask`, `/transcript`, `/end`, `/pack`, `/audio`) + OpenAPI (`/openapi/v1.json`) & Swagger UI (`/swagger`).
  8. [x] Write unit tests in `Levvy.Tests` for `.env` loading, Result pattern, validation, Hangfire dispatch, grounding, and study-pack citations.

---

## Phase 2 — Google Meet & Online Class Integration (Assigned to Adeyemi Mubarak — Backend Dev B) ✅

Enable Levvy to **join live Google Meet (and Zoom/Teams) online classes**, record the class audio, transcribe & translate live for **Deaf, hard-of-hearing, and international students**, answer grounded private questions mid-class, and generate cited study notes afterward.

- **Owner:** **Adeyemi Mubarak (Backend Dev B)**
- **Integration Modes:**
  - **Mode A — Bot Joins Google Meet (`IMeetingBotConnector` + Hangfire `OnlineClassBotJob`):** User pastes `meet.google.com/xxx-yyyy-zzz` -> Levvy spawns a meeting bot (`Recall.ai` / `MeetingBaas` via `MEETING_BOT_API_KEY` or headless Chromium + FFmpeg worker) that joins as **"Levvy Note-Taker"**, streams audio into `IAsrStreamer` + `IAudioStore`, fans out live captions/translations over `LectureSessionHub`, and triggers `StudyPackGenerationJob` on call end.
  - **Mode B — Zero-Bot Browser Tab Audio Fallback:** Host/student watching Google Meet in Chrome/Edge shares tab audio (`getDisplayMedia`) straight into `/ws/{sessionId}` when hosts restrict external bots.
- **Tasks for Mubarak:**
  1. [x] Extend `Session` entity (`MeetingPlatform`, `ExternalMeetingUrl`, `BotStatus`, `ExternalBotId`) and run `Add-Migration` + `Update-Database`. *(Migration `20261114052538_AddOnlineClassFields` — apply with `Update-Database` on deploy)*
  2. [x] Define `IMeetingBotConnector` in `Levvy.Application/Common/Interfaces/` and implement `GoogleMeetBotConnector` in `Levvy.Infrastructure/Meetings/`. *(Recall.ai-style REST: POST/GET/DELETE `/bot`; unconfigured key → `"unconfigured"` → Mode B fallback)*
  3. [x] Implement `ConnectOnlineClassCommand` (+ FluentValidation URL check for `meet.google.com`, `zoom.us`, `teams.microsoft.com`) and Hangfire `OnlineClassBotJob` to dispatch/monitor the bot. *(Dispatch job + 30s `Schedule` monitor loop via `IBackgroundJobScheduler`; `MeetingUrlParser`, shared `SessionCodeGenerator`)*
  4. [x] Add `POST /sessions/online-class`, `POST /sessions/{id:guid}/connect-meet`, and `POST /sessions/{id:guid}/meet-webhook` (to ingest live bot transcript/audio & lifecycle events and trigger `EndSessionCommand` -> `StudyPackGenerationJob` when the call ends). *(Webhook publishes `CaptionFinalized` — same seam as the hot pipe; `X-Meeting-Webhook-Secret` check when `MEETING_BOT_WEBHOOK_SECRET` is set)*
  5. [x] Add integration/unit tests covering Google Meet URL validation, bot lifecycle transitions, webhook caption persistence, and auto-generated study packs. *(22 tests in `OnlineClassTests`; suite 43/43 green)*

---

## Phase 3 — Next.js Frontend (`frontend/src/`) (Frontend Dev)
1. [x] Design Tokens (`globals.css`), `output: "standalone"` (`next.config.ts`), & Typed API/WS/Auth/Dashboard Client (`frontend/src/lib/api.ts`).
2. [ ] **Auth Pages (`/login`, `/signup`):** Email/password form + Google Sign-In button (`POST /auth/google`).
3. [ ] **User Dashboard (`/dashboard`):** Displays `FirstName`, `LastName`, `Institution`, `Department`, `PreferredLanguage`, `AccessibilityMode`, `CaptionFontSize`, hosted/attended sessions, saved Study Packs, and quick link to join or record an online class.
4. [ ] **Join / Landing Page (`/`):** Room code input + "Host a session" CTA + Demo Seed button (`POST /sessions/demo`).
5. [ ] **Host Console (`/host`):** Mic + Tab Audio (`getDisplayMedia` for Google Meet) + File Upload + Google Meet URL input, QR code + room link, live audio meter.
6. [ ] **Viewer Workspace (`/session/[code]`):** Live transcript feed, subtitle language selector, **Ask Levvy** private Q&A box with playable receipt chips, and synchronized `<audio>` receipt player.
7. [ ] **Study Pack (`/session/[code]/pack`):** Summary + interactive flashcards with receipt playback.

---

## Phase 4 — Vultr Compute Deploy & Demo Rehearsal
1. [x] Provision **Vultr Compute** instance (`64.177.55.161`, `vx1-g-2c-8g-120s`, Ubuntu 24.04 LTS), **Vultr Managed PostgreSQL**, and **Vultr Object Storage** (`levvy-recordings`).
2. [x] Configure `levvy.tech` DNS, Nginx reverse proxy (`/`, `/api/`, `/health`, `/sessions`, `/jobs`, `/ws/`, `/swagger`, `/openapi/`), Let's Encrypt HTTPS/WSS, and GitHub Actions CD (`deploy-vultr.yml`).
3. [ ] Update Nginx regex to include `auth|users|swagger|openapi` (`location ~ ^/(health|auth|users|sessions|jobs|swagger|openapi)`), verify `/swagger`, and deploy Auth + Dashboard migration.
4. [ ] Rehearse 5-minute demo twice: Deaf POV → International POV → Google Meet live captions → Ask Levvy + audio receipt → Study pack.

## Frontend implementation checkpoint — 14 November 2026

The screenshot-led entry, authentication, dashboard, host, viewer and study-pack routes are implemented and locally checked. See progresstracker.md and IMPLEMENTATION-REPORT.md for tested behavior. Earlier Phase 3 entries also contain demo seeding and meeting-bot URL controls, so those composite entries are not blanket-marked complete. The production backend returned 502; the real-service rehearsal remains open.
