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

## Phase 1 — Repo Init & Backend Build ✅

Git repository initialized at `c:\Users\ADMIN\source\repos\Levvy`, pushed to `origin/main`, and **Vultr Managed PostgreSQL** + **Vultr Object Storage (`levvy-recordings`)** + **Vultr Compute (`64.177.55.161`)** provisioned.

### Oriolowo Mustapha (Backend Dev A) — AI/LLM, ASR Hot Pipe & WebSocket Hub ✅
- **Patterns:** Strategy (`IAsrStreamer`, `ITranslator`, `ILlmAnswerer`), Hot-Pipe Bypass (no MediatR on raw audio frames), Observer (`CaptionFinalized` publisher), Fallback/Resilience (`ResilientAsrStreamer`).
- **Files owned:** `Levvy.Infrastructure/Asr/`, `Levvy.Infrastructure/Llm/`, `Levvy.Api/Hubs/` (WebSocket `/ws/{sessionId}`).
- **Tasks:**
  1. [x] Implement `GroqLlmClient` (`ITranslator` + `ILlmAnswerer`) calling `POST /openai/v1/chat/completions` (`gpt-oss-120b` primary, `llama-3.3-70b-versatile` fallback) with strict JSON citation output (`cite-or-say-not-covered`).
  2. [x] Implement `GroqAsrStreamer` (`IAsrStreamer`) calling `POST /openai/v1/audio/transcriptions` (`whisper-large-v3-turbo`) with `chunkStartOffsetMs` timeline math.
  3. [x] Enhance `DeepgramAsrStreamer` & `ResilientAsrStreamer` (`ASR_PROVIDER=groq|deepgram`, configurable `DEEPGRAM_MODEL`, `detected_language` extraction, automatic Groq <-> Deepgram failover).
  4. [x] Build `/ws/{sessionId}?lang=xx` WebSocket hub (`LectureSessionHub`) + per-session connection registry (`CaptionFinalized` fan-out & private `AskQuestionCommand` answers).
  5. [x] Write unit tests in `Levvy.Tests` for `chunkStartOffsetMs` timeline math, Deepgram parsing/provider switching, and LLM fallback/citation parsing.

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

## Phase 3 — Next.js Frontend (`frontend/src/`) (Frontend Dev)
1. [x] Design Tokens (`globals.css`), `output: "standalone"` (`next.config.ts`), & Typed API/WS Client (`frontend/src/lib/api.ts`).
2. [ ] **Join / Landing Page (`/`):** Room code input + "Host a session" CTA + Demo Seed button (`POST /sessions/demo`).
3. [ ] **Host Console (`/host`):** Mic + Tab Audio (`getDisplayMedia`) + File Upload chunked streamer over WebSocket, QR code + room link, live audio meter.
4. [ ] **Viewer Workspace (`/session/[code]`):**
   - Live transcript feed with auto-scroll & active line highlight
   - Subtitle language radio selector
   - **Ask Levvy** private Q&A box with playable receipt chips
   - Synchronized `<audio>` receipt player (click any line or receipt chip → jump to `offsetMs`)
5. [ ] **Study Pack (`/session/[code]/pack`):** Summary + interactive flashcards with receipt playback.

---

## Phase 4 — Vultr Compute Deploy & Demo Rehearsal
1. [x] Provision **Vultr Compute** instance (`64.177.55.161`, `vx1-g-2c-8g-120s`, Ubuntu 24.04 LTS), **Vultr Managed PostgreSQL**, and **Vultr Object Storage** (`levvy-recordings`).
2. [x] Configure `levvy.tech` DNS, Nginx reverse proxy (`/`, `/api/`, `/health`, `/sessions`, `/jobs`, `/ws/`, `/swagger`, `/openapi/`), Let's Encrypt HTTPS/WSS, and GitHub Actions CD (`deploy-vultr.yml`).
3. [ ] Verify Nginx routing for `/swagger` + `/openapi/v1.json` and deploy EF Core migration + Deepgram updates.
4. [ ] Rehearse 5-minute demo twice: Deaf POV → International POV → Live speech + captions → Ask Levvy + audio receipt → Study pack.
