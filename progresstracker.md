# Levvy — Progress Tracker

> **Last updated:** 2026-11-13  
> **Target:** HackGlobal 2026 Demo (15 Nov 2026)

---

## Overall Status

| Area | Status | Notes |
|---|---|---|
| Monorepo & Tooling | 🟢 Done | `backend/` (.NET 10) + `frontend/` (Next.js/TS/Tailwind) + GitHub Actions CI/CD |
| Backend Domain & Contracts | 🟢 Done | Entities, MediatR commands/queries, interfaces defined; `dotnet test` 9/9 passing |
| Backend Implementation | 🟢 Done | Groq ASR/LLM, Deepgram fallback, S3 AudioStore, Minimal APIs, WebSocket hub |
| Frontend UI | 🟡 In Progress (Frontend Dev) | `src/lib/api.ts` + design tokens ready; pages owned by Frontend Dev |
| Vultr Infrastructure | ⚪ Not Started | Compute, Managed Postgres, Object Storage provisioned on build day |
| Demo & Validation | ⚪ Not Started | 2 audio fixtures + 10 user convos/country + 5-min pitch rehearsal |

---

## Backend (`backend/`)

### Shared Baseline
- [x] Clean Architecture solution (`Domain`, `Application`, `Infrastructure`, `Api`, `Tests`)
- [x] Domain entities (`Session`, `Participant`, `TranscriptSegment`, `Question`, `Answer`, `AnswerCitation`, `StudyPack`, `Flashcard`)
- [x] `LevvyDbContext` + SQLite/Postgres provider switch in `Program.cs`
- [x] Baseline DI test passing (`PipelineTests`)

### Oriolowo Mustapha (Dev A) — AI/LLM, ASR Hot Pipe & WebSockets
- [x] `GroqLlmClient` (`ITranslator` + `ILlmAnswerer` with `gpt-oss-120b` / `llama-3.3-70b-versatile`)
- [x] `GroqAsrStreamer` (`whisper-large-v3-turbo`) + `chunkStartOffsetMs` timeline math
- [x] `DeepgramAsrStreamer` fallback + `ResilientAsrStreamer` (`IAsrStreamer` Strategy)
- [x] `/ws/{sessionId}` WebSocket hub (`LectureSessionHub`) + per-viewer language registry + `CaptionFinalized` fan-out
- [x] Unit tests for timestamp offset math & Groq response/fallback parsing (`AsrAndLlmTests`)

### Adeyemi Mubarak (Dev B) — Vultr Postgres, Object Storage, CQRS & REST
- [ ] `ISessionRepository` EF Core implementation + Npgsql migrations (straight on Vultr Managed Postgres)
- [ ] `IAudioStore` (straight on Vultr Object Storage S3)
- [ ] `CreateSessionCommand`, `JoinSessionCommand`, `EndSessionCommand` handlers + validators
- [ ] `GetSessionByCodeQuery` & `GetTranscriptQuery` handlers
- [ ] `AskQuestionCommand` handler + `AskQuestionCommandValidator`
- [ ] `SessionEnded` event handler + `GenerateStudyPackCommand` handler
- [ ] Minimal API REST endpoints (`/sessions`, `/join`, `/ask`, `/transcript`, `/end`, `/audio`)
- [ ] Unit tests for validation, grounding, and study-pack citations

---

## Frontend (`frontend/`)

- [x] Next.js App Router + TypeScript + Tailwind CSS + ESLint scaffolded
- [ ] Apply design tokens (`#F4F5F7` ground, `#2563EB` accent, `tabular-nums`) in `globals.css`
- [ ] Typed REST client (`src/lib/api.ts`) & WebSocket hook (`src/lib/ws.ts`)
- [ ] Landing / Join page (`src/app/page.tsx`)
- [ ] Host console (`src/app/host/page.tsx`) — mic, tab audio, file upload, QR code
- [ ] Viewer workspace (`src/app/session/[code]/page.tsx`) — live transcript, subtitle selector, Ask Levvy, audio receipt player
- [ ] Study pack view (`src/app/session/[code]/pack/page.tsx`) — cited summary + flashcards
- [ ] Mobile responsive pass & accessibility check (keyboard + contrast)

---

## Infrastructure & Pre-Demo Checklist

- [ ] `git init` & initial push at 23:00 / 14 Nov
- [ ] `Groq:ApiKey` set in user-secrets / env vars
- [ ] `Deepgram:ApiKey` ready as fallback
- [ ] Vultr Object Storage bucket + keys configured
- [ ] Vultr Managed Postgres provisioned + connection string set
- [ ] Vultr Compute deployed with HTTPS + WSS
- [ ] Two audio fixtures tested end-to-end
- [ ] Full 5-minute demo rehearsal passed twice
