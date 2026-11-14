# Levvy — Build Plan (HackGlobal 2026)

> **Build Window:** 14–15 Nov 2026 (starts 23:00 on 13 Nov).  
> **Rule:** No `git init` or remote push until build starts at 23:00 / 14 Nov.

---

## Phase 0 — Pre-Build Prep (Before 23:00, 13 Nov) ✅
- [x] Freeze stack & WebSocket/REST contract (`README.md`, `EXTERNAL-SERVICES.md`)
- [x] Scaffold .NET 10 Clean Architecture in `backend/` + verify `dotnet test`
- [x] Scaffold Next.js + TS + Tailwind in `frontend/`
- [x] Create `architecture.md`, `design.md`, `plan.md`, `progresstracker.md`
- [ ] Verify Groq API key (`dotnet user-secrets set "Groq:ApiKey" "..."`) & Deepgram backup key
- [ ] Prepare 2 demo audio fixtures (10-min lecture + 2-min teammate clip)

---

## Phase 1 — Repo Init & Parallel Backend Build (14 Nov, Morning – Afternoon)

First, initialize Git at `c:\Users\ADMIN\source\repos\Levvy` and push the initial monorepo. Provision **Vultr Managed PostgreSQL** and **Vultr Object Storage** immediately so both devs build straight against production services (no SQLite or local-disk detours).

### Oriolowo Mustapha (Backend Dev A) — AI/LLM, ASR Hot Pipe & WebSocket Hub
- **Patterns:** Strategy (`IAsrStreamer`, `ITranslator`, `ILlmAnswerer`), Hot-Pipe Bypass (no MediatR on raw audio frames), Observer (`CaptionFinalized` publisher), Fallback/Resilience.
- **Files owned:** `Levvy.Infrastructure/Asr/`, `Levvy.Infrastructure/Llm/`, `Levvy.Api/Hubs/` (WebSocket `/ws/{sessionId}`).
- **Tasks:**
  1. Implement `GroqLlmClient` (`ITranslator` + `ILlmAnswerer`) calling `POST /openai/v1/chat/completions` (`gpt-oss-120b` primary, `llama-3.3-70b-versatile` fallback) with strict JSON citation output (`cite-or-say-not-covered`).
  2. Implement `GroqAsrStreamer` (`IAsrStreamer`) calling `POST /openai/v1/audio/transcriptions` (`whisper-large-v3-turbo`) with `chunkStartOffsetMs` timeline math.
  3. Prepare `DeepgramAsrStreamer` (`IAsrStreamer` Strategy fallback) if Groq rate-limits or degrades.
  4. Build `/ws/{sessionId}?lang=xx` WebSocket hub + per-session connection registry:
     - Binary audio frames → `IAsrStreamer` + `IAudioStore` → publish `CaptionFinalized` → fan out `caption` & per-viewer `translation` JSON.
     - Incoming `{ "type": "ask", "text": "..." }` → dispatch `AskQuestionCommand` via `IMediator` → return `answer` JSON privately to asker.
  5. Write unit tests in `Levvy.Tests` for `chunkStartOffsetMs` timeline math and LLM fallback/citation parsing.

### Adeyemi Mubarak (Backend Dev B) — Vultr Postgres Persistence, Object Storage, CQRS & REST API
- **Patterns:** Repository, CQRS + MediatR, Pipeline Validation (`FluentValidation`), Domain Event Handlers (`INotificationHandler`), Adapter (`IAudioStore`), Minimal APIs.
- **Files owned:** `Levvy.Infrastructure/Persistence/`, `Levvy.Infrastructure/Storage/`, `Levvy.Application/Commands/*Handler.cs`, `Levvy.Application/Queries/*Handler.cs`, `Levvy.Api/Endpoints/` (REST).
- **Tasks:**
  1. Implement `ISessionRepository` (EF Core against **Vultr Managed PostgreSQL** via `Database:Provider=postgres`) + initial Npgsql migration.
  2. Implement `IAudioStore` directly against **Vultr Object Storage** (S3-compatible) for recordings & packs.
  3. Implement `CreateSessionCommand`, `JoinSessionCommand`, `EndSessionCommand` handlers + FluentValidation validators.
  4. Implement `GetSessionByCodeQuery` & `GetTranscriptQuery` handlers.
  5. Implement `AskQuestionCommand` handler + `AskQuestionCommandValidator` (enforcing `cite-or-say-not-covered` against `ISessionRepository` + `ILlmAnswerer`).
  6. Implement `SessionEnded` `INotificationHandler` → `GenerateStudyPackCommand` handler (cited summary + cited flashcards).
  7. Wire Minimal API REST endpoints (`POST /sessions`, `POST /sessions/{code}/join`, `POST /sessions/{code}/ask`, `GET /sessions/{code}/transcript`, `POST /sessions/{id}/end`, `GET /sessions/{code}/audio`).
  8. Write unit tests in `Levvy.Tests` for CQRS validation, grounding enforcement, and study-pack citations.

### Integration Seam (Both Devs)
- Merge **Oriolowo Mustapha** + **Adeyemi Mubarak** branches via `Program.cs` DI registration and run end-to-end test: audio fixture → WS captions → translation → `AskQuestionCommand` → `EndSessionCommand` → `StudyPack`.

---

## Phase 3 — Next.js Frontend (`frontend/src/`) (14 Nov, Evening – 15 Nov, Morning)
1. **Design Tokens & Shell:** Apply tokens from `design.md` in `globals.css`.
2. **Join / Landing Page (`/`):** Room code input + "Host a session" CTA.
3. **Host Console (`/host`):** Mic + Tab Audio (`getDisplayMedia`) + File Upload chunked streamer over WebSocket, QR code + room link, live audio meter.
4. **Viewer Workspace (`/session/[code]`):**
   - Live transcript feed with auto-scroll & active line highlight
   - Subtitle language radio selector
   - **Ask Levvy** private Q&A box with playable receipt chips
   - Synchronized `<audio>` receipt player (click any line or receipt chip → jump to `offsetMs`)
5. **Study Pack (`/session/[code]/pack`):** Summary + interactive flashcards with receipt playback.

---

## Phase 4 — Vultr Compute Deploy & Demo Rehearsal (15 Nov, Afternoon)
1. Provision **Vultr Compute** instance and configure production env vars (`Database:Provider=postgres`, `ConnectionStrings:Levvy`, `Groq:ApiKey`, `Storage:*`).
2. Deploy `Levvy.Api` + `frontend/` behind HTTPS/WSS on Vultr (already wired to Vultr Managed Postgres & Object Storage from Phase 1).
3. Rehearse 5-minute demo twice: Deaf POV → International POV → Live speech + captions → Ask Levvy + audio receipt → Study pack.
