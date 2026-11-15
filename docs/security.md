# Levvy — Security & Reliability Audit Summary

This document summarizes the security, authorization, and resilience audit findings across the Levvy backend and frontend and the exact fixes implemented in code.

---

## 1. Critical Findings (`C1` – `C3`) & Scoped Host Tickets

| ID | Finding | Fix Implemented | Code Reference |
|---|---|---|---|
| **C1** | **Silent Bearer Fallback:** Presenting an invalid/expired `Authorization: Bearer` header previously fell back to `X-User-Id` instead of rejecting the request. | `HttpUserResolver.ResolveCurrentUserId` now fails closed (`null` → `401 Unauthorized`) whenever a `Bearer` header is present but invalid. | `Levvy.Api/Endpoints/HttpUserResolver.cs` |
| **C2** | **Unauthenticated Host Actions:** Client-declared `?role=host` on WebSockets, `POST /sessions/{id}/end`, `POST /sessions/{id}/audio`, and `POST /sessions/{id}/connect-meet` could be invoked by non-hosts. | Centralized `HostAccess.IsHostAuthorizedAsync` (checks owner Bearer JWT, constant-time `X-Host-Key` comparison, or anonymous demo session) + added **single-use 30s WebSocket tickets** (`POST /sessions/{id}/ws-ticket`). Unauthorized host WS upgrade attempts return `403 Forbidden`. | `Levvy.Api/Endpoints/HostAccess.cs`, `WebSocketEndpoints.cs`, `WebSocketTicketStore.cs` |
| **C3** | **Hardcoded Dev JWT Secret Risk:** Running outside `Development` without configuring `JWT_SECRET` risked signing production tokens with a known fallback string. | `Program.cs` enforces a startup guard in non-Development environments: throws `InvalidOperationException` if `JWT_SECRET` is unset, `< 32` chars, or matches the dev placeholder. | `Levvy.Api/Program.cs` |

---

## 2. High-Severity Findings (`H1` – `H7`)

| ID | Finding | Fix Implemented | Code Reference |
|---|---|---|---|
| **H1** | **Credential Stuffing & PBKDF2 CPU DoS:** `/auth/login`, `/auth/register`, and `/auth/google` had no per-IP rate limiting. | Registered a per-IP fixed-window rate limiter (`auth` policy, configurable via `RateLimit:AuthPermitLimitPerMinute`, returning `429 Too Many Requests`). | `Levvy.Api/Program.cs` |
| **H2** | **Google OAuth Audience Verification:** Missing `GOOGLE_CLIENT_ID` in production could skip `aud` verification silently. | `GoogleAuthVerifier` validates `aud == Google:ClientId` when configured, and `Program.cs` logs a startup warning in non-Dev if `GOOGLE_CLIENT_ID` is absent. | `Levvy.Infrastructure/Authentication/GoogleAuthVerifier.cs`, `Program.cs` |
| **H3** | **Wildcard CORS Policy:** `AllowAnyOrigin()` was active in all environments. | Restricted CORS to configured `CORS_ALLOWED_ORIGINS` in production, retaining `AllowAnyOrigin` only as a local development fallback. | `Levvy.Api/Program.cs` |
| **H4** | **Hangfire Dashboard Exposure behind Reverse Proxy:** Hangfire's default `LocalRequestsOnlyAuthorizationFilter` can misclassify reverse-proxied requests as local. | Implemented `TokenDashboardAuthorizationFilter` using constant-time `CryptographicOperations.FixedTimeEquals` against `HANGFIRE_DASHBOARD_TOKEN`. | `Levvy.Api/Endpoints/TokenDashboardAuthorizationFilter.cs` |
| **H5** | **Unbounded Upstream HTTP Timeouts:** Default 100s `HttpClient` timeouts could stall live captioning or Q&A threads. | Configured explicit 30s timeouts on all named `HttpClient` registrations (`Groq`, `Deepgram`, `DeepgramTts`, `MeetingBot`, `VultrS3`, `GoogleAuth`). | `Levvy.Infrastructure/DependencyInjection.cs` |
| **H6** | **Full Session-Graph Load on Every Caption:** Persisting each 5s caption previously loaded the entire `Session` graph and failed on Hangfire retry duplicates. | Replaced with append-only, idempotent `SessionRepository.AppendSegmentIfMissingAsync` in `CaptionPersistenceJob`. | `Levvy.Infrastructure/BackgroundJobs/CaptionPersistenceJob.cs` |
| **H7** | **In-Memory Audio Buffering (`byte[]`):** Uploading or streaming long WebM lecture recordings buffered the entire file in RAM. | `VultrAudioStore` now streams uploads directly to S3 via `UNSIGNED-PAYLOAD` SigV4 and streams downloads via `ResponseHeadersRead` with HTTP Range support. | `Levvy.Infrastructure/Storage/VultrAudioStore.cs` |

---

## 3. Medium & Minor Hardening (`M1` – `M7`)

| ID | Finding | Fix Implemented | Code Reference |
|---|---|---|---|
| **M1** | **Loose Citation Matching (`±2000ms`):** Allowed citations up to 2 seconds away from actual segment boundaries. | Tightened `GroundingVerifier` timestamp matching tolerance to **`±1000ms`** (`MaxOffsetToleranceMs = 1000`). | `Levvy.Infrastructure/Llm/GroundingVerifier.cs` |
| **M2** | **Uncited Study Pack Summaries:** Study pack summaries could pass without citing any transcript timestamp. | `GroundingVerifier.VerifyStudyPack` requires at least one valid `[mm:ss]` citation in the summary or replaces it with a grounded fallback citing the first segment. | `Levvy.Infrastructure/Llm/GroundingVerifier.cs` |
| **M3** | **LLM Timeout Failover:** Primary model timeouts (`TaskCanceledException`) did not trigger fallback to the secondary model. | `GroqLlmClient` catches both `HttpRequestException` and per-request timeout `TaskCanceledException` to fail over cleanly from `openai/gpt-oss-120b` to `llama-3.3-70b-versatile`. | `Levvy.Infrastructure/Llm/GroqLlmClient.cs` |
| **M5** | **Cartesian Explosion on Multi-Collection Includes:** Loading `Session` with `Participants`, `Segments`, `Questions`, and `StudyPack.Flashcards` in one SQL join multiplied rows. | Added `.AsSplitQuery()` to `SessionRepository` aggregate queries. | `Levvy.Infrastructure/Persistence/SessionRepository.cs` |
| **M6** | **Unbounded Meeting-Bot Polling:** Stuck external meeting bots could re-enqueue polling jobs indefinitely. | Capped `OnlineClassBotJob.MonitorBotAsync` at `MaxPollAttempts = 40` (20 minutes) before marking the bot `failed`. | `Levvy.Infrastructure/BackgroundJobs/OnlineClassBotJob.cs` |
| **M7** | **Lookalike Meeting URL Bypass:** Substring matching allowed `https://evil.example.com/meet.google.com`. | `MeetingUrlParser` parses `Uri.Host` strictly and only accepts `meet.google.com`, `zoom.us`, `teams.microsoft.com`, or `teams.live.com`. | `Levvy.Application/Common/MeetingUrlParser.cs` |
