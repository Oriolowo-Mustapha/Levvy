# External Services

Every outside service Levvy depends on, what it does, and where it plugs in.
Secrets live in `.env` (never committed), user-secrets, or environment variables — never in config files or source.

**Status as of 14 Nov 2026:** all integrations below are **implemented and wired**, not stubs.

---

## 1. Groq (primary AI provider)

**Website:** https://console.groq.com

One vendor, two jobs. OpenAI-compatible REST endpoint, so one `HttpClient` (named client `"Groq"`, 30s timeout) handles both.

### 1a. Speech to Text (live captions)

| | |
|---|---|
| Model | `whisper-large-v3-turbo` |
| Endpoint | `POST /openai/v1/audio/transcriptions` (multipart) |
| Use | Transcribes the live lecture audio in ~5s chunks (configurable via `ASR_CHUNK_DURATION_MS`). Each chunk's segments get the accumulated `chunkStartOffsetMs` added, which builds the receipt timeline every citation points into. |
| Code entry point | `Levvy.Infrastructure/Asr/GroqAsrStreamer.cs` → implements `IAsrStreamer` |
| Config key | `Groq:AsrModel` (env `GROQ_ASR_MODEL`) |
| Secret | `Groq:ApiKey` (env `GROQ_API_KEY`) |
| Status | **Live.** Failover + circuit breaker in `ResilientAsrStreamer`. |

### 1b. LLM (translation + grounded answers + study pack)

| | |
|---|---|
| Models | `gpt-oss-120b` (primary), `llama-3.3-70b-versatile` (fallback on HTTP error or timeout) |
| Endpoint | `POST /openai/v1/chat/completions` |
| Uses | 1. Per-segment subtitle translation into each viewer's preferred language. 2. "Ask Levvy" mid-lecture Q&A, grounded in the transcript only, under the cite-or-say-not-covered rule. 3. Post-class study pack: summary + flashcards, each item citing its recording offset (summary must carry a `[mm:ss]` citation or it is replaced by a grounded default). |
| Code entry point | `Levvy.Infrastructure/Llm/GroqLlmClient.cs` → implements `ITranslator` and `ILlmAnswerer`; grounding enforced in `GroundingVerifier.cs` |
| Config keys | `Groq:AnswerModel`, `Groq:FallbackModel`, `Groq:BaseUrl` |
| Secret | `Groq:ApiKey` (same key serves STT and chat) |
| Status | **Live.** |

---

## 2. Deepgram (fallback ASR)

**Website:** https://console.deepgram.com

| | |
|---|---|
| Model | `nova-2` |
| Use | Insurance. If Groq's Whisper endpoint degrades or rate-limits during the demo, Deepgram takes over. `ResilientAsrStreamer` picks the provider per config (`ASR_PROVIDER`) and falls back per chunk; after 3 consecutive primary failures it skips the primary for 60s (circuit breaker). |
| Code entry point | `Levvy.Infrastructure/Asr/DeepgramAsrStreamer.cs` — same `IAsrStreamer` contract as Groq |
| Secret | `Deepgram:ApiKey` (env `DEEPGRAM_API_KEY`) |
| Status | **Live.** If no key is set, Deepgram failover silently disables. |

---

## 3. Meeting-Bot Provider (Recall.ai-compatible, Mode A)

| | |
|---|---|
| Use | Joins Google Meet/Zoom/Teams links as the "Levvy Note-Taker" bot: captures audio + live captions and posts them back via webhook into the same `CaptionFinalized` seam as the browser-tab hot pipe. |
| Code entry point | `Levvy.Infrastructure/MeetingBots/GoogleMeetBotConnector.cs` → `IMeetingBotConnector`; lifecycle jobs in `OnlineClassBotJob.cs` (dispatch → monitor ≤40 polls × 30s); webhook validation via `MeetingBot:WebhookSecret` |
| URL parsing | `Levvy.Application/Common/MeetingUrlParser.cs` — host-aware; only `meet.google.com`, `zoom.us`, `teams.microsoft.com` / `teams.live.com` (or their subdomains) are accepted |
| Secret | `MeetingBot:ApiKey` (env `MEETING_BOT_API_KEY`), `MeetingBot:ApiBaseUrl`, `MeetingBot:WebhookSecret` |
| Status | **Live.** Leave the API key empty to run Mode B (browser tab audio over `/ws/{sessionId}`); `/sessions/{id}/connect-meet` then reports `unconfigured` and the session stays Mode B. |

---

## 4. Vultr (all infrastructure)

**Website:** https://www.vultr.com

Three roles, covered by credits:

### 4a. Vultr Compute

| | |
|---|---|
| Use | Hosts the Levvy API (ASP.NET Core minimal APIs + WebSocket hub) for the demo. Attendees connect their browsers here. |
| Code entry point | `.github/workflows/deploy-vultr.yml` — runs backend tests, publishes `Levvy.Api` self-contained, rsyncs to the VM, restarts the systemd service. |
| Status | **Deploying on push to main.** |

### 4b. Vultr Object Storage (S3-compatible)

| | |
|---|---|
| Use | Stores the raw lecture recordings. Every playable receipt ultimately resolves to a position in a recording stored here. Local-filesystem fallback under `AppContext.BaseDirectory/recordings` keeps dev/demo working without credentials. |
| Code entry point | `Levvy.Infrastructure/Storage/VultrAudioStore.cs` → implements `IAudioStore`. Hand-rolled SigV4 (PUT streams the file with `UNSIGNED-PAYLOAD`; GET streams the response through with `ResponseHeadersRead`). |
| Config | `Storage:Endpoint` (`ewr1.vultrobjects.com`), `Storage:Region`, `Storage:Bucket` |
| Secret | `Storage:AccessKey`, `Storage:SecretKey` |
| Status | **Live.** One caveat: the streamed-PUT signing mode should be verified once on a real deploy (no local object storage to test against). |

### 4c. Vultr Managed Databases (PostgreSQL)

| | |
|---|---|
| Use | The **only** persistence provider. Npgsql-only discipline is enforced through all merge cycles; migrations are additive. SQLite exists solely as the test fake. |
| Code entry point | `Levvy.Infrastructure/Persistence/LevvyDbContext.cs` + `DependencyInjection.cs` (hard `UseNpgsql`); connection string from `DATABASE_URL` or `ConnectionStrings:Levvy`. Migrations run automatically at startup (`db.Database.Migrate()`). |
| Secret | `DATABASE_URL` (env var on the compute instance) |
| Status | **Live.** |

---

## 5. Google Identity (Sign in with Google)

| | |
|---|---|
| Use | `/auth/google` accepts a Google ID token, verifies it against the `tokeninfo` endpoint, and signs the user in or up. Audience (`aud`) is checked against `GOOGLE_CLIENT_ID` when configured — **the startup logs a loud warning in non-dev if the client id is missing**, because audience validation is then skipped. |
| Code entry point | `Levvy.Infrastructure/Authentication/GoogleAuthVerifier.cs` → `IGoogleAuthVerifier`; consumed by `AuthEndpoints.cs` |
| Secret | `Google:ClientId`, `Google:ClientSecret` |
| Status | **Live.** Post-hackathon: move to local JWKS verification (Google deprecates `tokeninfo`). |

---

## 6. GitHub (Actions)

| | |
|---|---|
| Use | CI (backend + frontend test/build) and CD — deploys `main` to Vultr. Tests gate deploys: a red `dotnet test` blocks release. |
| Workflows | `.github/workflows/backend-ci.yml`, `frontend-ci.yml`, `deploy-vultr.yml` |
| Auth | `GITHUB_TOKEN` + deploy secrets (`VULTR_HOST`, `VULTR_*`, `DEPLOY_KEY`) |
| Status | **Live.** |

---

## Secrets map (single source of truth)

All keys are documented in `backend/.env.example`; copy it to `backend/.env` and fill in. The `DotEnvLoader` maps flat env keys to .NET hierarchical config automatically.

| Env var | Feeds | Notes |
|---|---|---|
| `DATABASE_URL` | Npgsql / Hangfire storage | Vultr Managed Postgres |
| `GROQ_API_KEY` | GroqAsrStreamer + GroqLlmClient | required |
| `DEEPGRAM_API_KEY` | fallback ASR | optional |
| `STORAGE_ACCESS_KEY` / `STORAGE_SECRET_KEY` / `STORAGE_BUCKET` / `STORAGE_ENDPOINT` | VultrAudioStore | optional — local fallback without them |
| `JWT_SECRET` | JwtTokenService | **required ≥32 chars in non-dev; startup throws otherwise** |
| `GOOGLE_CLIENT_ID` | GoogleAuthVerifier | warn if missing in non-dev |
| `MEETING_BOT_API_KEY` / `MEETING_BOT_API_BASE_URL` / `MEETING_BOT_WEBHOOK_SECRET` | Meeting-bot Mode A | optional — Mode B fallback |
| `CORS_ALLOWED_ORIGINS` | CORS policy | required in prod |
| `WS_HOST_KEY` | WebSocket host-role escape hatch | optional |
| `HANGFIRE_DASHBOARD_TOKEN` | `/jobs` dashboard access | required for non-loopback access |
| `ASR_CHUNK_DURATION_MS` | live-caption chunking (default 5000) | optional |

## Dependency order if something fails

1. Groq STT fails → circuit breaker trips after 3 failures → Deepgram (same `IAsrStreamer` seam)
2. Groq LLM fails → `Groq:FallbackModel` (`llama-3.3-70b-versatile`), same client, also on timeouts
3. Both AI providers fail → captions stop; record a fixture-based offline demo as the safety net
4. Meeting-bot provider fails → session stays Mode B (browser tab audio), demo unaffected
5. Vultr services are owned by us, so the failure window is provisioning, not outages

## UI integration verification — 14 November 2026

The new frontend connects to the configured API/WebSocket addresses and contains no provider keys. Local contract tests use explicit fixtures, not the external services above. Read-only requests to `https://levvy.tech/health` and `/sessions/K4F-8Q2` returned 502 Bad Gateway during verification. Production PostgreSQL, ASR, translation, grounding, object storage, OAuth and study-generation integration should not be considered verified by the frontend test suite.
