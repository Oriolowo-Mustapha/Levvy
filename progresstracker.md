# Levvy — Progress Tracker

> **Last updated:** 2026-11-14  
> **Target:** HackGlobal 2026 Demo (15 Nov 2026)  
> **Live URL:** `https://levvy.tech`

---

## Overall Status

| Area | Status | Notes |
|---|---|---|
| Monorepo & Tooling | 🟢 Done | `backend/` (.NET 10 Clean Arch) + `frontend/` (Next.js standalone) + GitHub Actions CD (`deploy-vultr.yml`) |
| Backend Domain & Contracts | 🟢 Done | Entities, Result pattern, MediatR CQRS, FluentValidation, Domain Events |
| Backend Implementation | 🟢 Done | Groq LLM/ASR, Deepgram AI (`nova-2`, auto-lang, `ASR_PROVIDER`), S3 AudioStore, Hangfire, Npgsql Migrations, OpenAPI/Swagger (`/swagger`) |
| Frontend Foundation (`api.ts`) | 🟢 Done | Typed REST + WebSocket client (`frontend/src/lib/api.ts`), design tokens (`globals.css`), `output: "standalone"` |
| Frontend Pages | 🟡 In Progress (Frontend Dev) | Landing/Join, Host Console, Viewer Workspace, Study Pack UI pages |
| Vultr Infrastructure | 🟢 Provisioned | Compute (`64.177.55.161`), Managed Postgres, Object Storage (`levvy-recordings`), `levvy.tech` HTTPS/WSS |
| Demo & Validation | ⚪ Not Started | 2 audio fixtures + 10 user convos/country + 5-min pitch rehearsal |

---

## Backend (`backend/`)

### Shared Baseline
- [x] Clean Architecture solution (`Domain`, `Application`, `Infrastructure`, `Api`, `Tests`)
- [x] Domain entities (`Session`, `Participant`, `TranscriptSegment`, `Question`, `Answer`, `AnswerCitation`, `StudyPack`, `Flashcard`)
- [x] `LevvyDbContext` + **Npgsql-only** persistence (`ConnectionStrings:Levvy` / `DATABASE_URL`; local dev default `Host=localhost;Database=levvy`)
- [x] EF Core migrations (`InitialCreate`) + design-time factory; `Database.Migrate()` replaces `EnsureCreated`
- [x] Baseline DI test passing (`PipelineTests`)

### AI/LLM, ASR Hot Pipe & WebSockets
- [x] `GroqLlmClient` (`ITranslator` + `ILlmAnswerer` with `gpt-oss-120b` / `llama-3.3-70b-versatile`)
- [x] `GroqAsrStreamer` (`whisper-large-v3-turbo`) + `chunkStartOffsetMs` timeline math
- [x] `DeepgramAsrStreamer` (`nova-2` / configurable `DEEPGRAM_MODEL`, `detected_language` extraction, word-level fallback timestamps)
- [x] `ResilientAsrStreamer` (`ASR_PROVIDER=groq|deepgram`, automatic bidirectional failover between Groq and Deepgram)
- [x] `/ws/{sessionId}` WebSocket hub (`LectureSessionHub`) + per-viewer language registry + `CaptionFinalized` fan-out
- [x] Unit tests for Groq + Deepgram timestamp offset math, `ASR_PROVIDER=deepgram` routing, and LLM fallback/citation parsing (`AsrAndLlmTests`)

### Adeyemi Mubarak (Dev B) — Vultr Postgres, Object Storage, CQRS & REST
- [x] `ISessionRepository` EF Core implementation + Npgsql migration (straight on Vultr Managed Postgres)
- [x] `IAudioStore` (straight on Vultr Object Storage S3) — upload via signed PUT **and signed GET read fallback** so receipt playback resolves from S3 when no local copy exists
- [x] `CreateSessionCommand`, `JoinSessionCommand`, `EndSessionCommand` handlers + validators
- [x] `GetSessionByCodeQuery` & `GetTranscriptQuery` handlers
- [x] `AskQuestionCommand` handler + `AskQuestionCommandValidator`
- [x] `SessionEnded` event handler + `GenerateStudyPackCommand` handler (Hangfire-enqueued with 3 retries)
- [x] Minimal API REST endpoints (`/sessions`, `/demo`, `/join`, `/ask`, `/transcript`, `/end`, `/pack`, `/audio` up+down)
- [x] Unit tests for validation, grounding, and study-pack citations
- [x] Persistence unit tests (`SessionRepositoryTests`): code normalization, unique Code index, full-graph load, study-pack query mapping (SQLite in-memory as test-only fake provider)
### Postgres, Object Storage, CQRS, Hangfire & REST
- [x] `SessionRepository` EF Core implementation + initial Npgsql migration (`20261114031932_InitialCreate`)
- [x] `VultrAudioStore` (`IAudioStore` S3-compatible against Vultr Object Storage)
- [x] `CreateSessionCommand`, `JoinSessionCommand`, `EndSessionCommand` handlers + FluentValidation validators
- [x] `GetSessionByCodeQuery`, `GetTranscriptQuery`, `GetStudyPackByCodeQuery` handlers
- [x] `AskQuestionCommand` handler + validator (`cite-or-say-not-covered`)
- [x] Hangfire background jobs (`CaptionPersistenceJob`, `StudyPackGenerationJob`, `StaleSessionCleanupJob`, `RecurringJobRegistrationService`) + `/jobs` dashboard
- [x] Minimal API REST endpoints (`/health`, `/sessions`, `/sessions/demo`, `/join`, `/ask`, `/transcript`, `/end`, `/pack`, `/audio`) with OpenAPI summaries
- [x] End-to-end CQRS + Hangfire worker tests passing (`PipelineTests`)

---

## Frontend (`frontend/`)

- [x] Next.js App Router + TypeScript + Tailwind CSS + ESLint + `output: "standalone"` configured
- [x] Apply design tokens (`#F4F5F7` ground, `#2563EB` accent, `tabular-nums`) in `globals.css`
- [x] Typed REST client & WebSocket helper (`src/lib/api.ts`)
- [ ] Landing / Join page (`src/app/page.tsx`)
- [ ] Host console (`src/app/host/page.tsx`) — mic, tab audio, file upload, QR code
- [ ] Viewer workspace (`src/app/session/[code]/page.tsx`) — live transcript, subtitle selector, Ask Levvy, audio receipt player
- [ ] Study pack view (`src/app/session/[code]/pack/page.tsx`) — cited summary + flashcards
- [ ] Mobile responsive pass & accessibility check (keyboard + contrast)

---

## Infrastructure & Pre-Demo Checklist

- [x] Git repo initialized & pushed to GitHub (`main`) with automated Vultr CD workflow
- [x] Vultr Object Storage bucket (`levvy-recordings`) + keys configured
- [x] Vultr Managed Postgres provisioned + connection string set
- [x] Vultr Compute deployed (`64.177.55.161`) with `levvy.tech` Let's Encrypt HTTPS + WSS
- [ ] Update Nginx regex on VM to include `swagger|openapi` (`location ~ ^/(health|sessions|jobs|swagger|openapi)`) and verify `https://levvy.tech/swagger`
- [ ] Two audio fixtures tested end-to-end (with both Groq and `ASR_PROVIDER=deepgram`)
- [ ] Full 5-minute demo rehearsal passed twice
