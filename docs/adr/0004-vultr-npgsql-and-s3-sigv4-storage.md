# ADR 0004: Vultr Npgsql Persistence & Streamed S3 SigV4 Audio Storage

- **Status:** Accepted
- **Date:** 2026-11-14

## Context
Every `[mm:ss]` receipt chip in Levvy promises that a student can click the citation and hear the exact moment of the lecture recording. Storing multi-megabyte WebM audio recordings inside PostgreSQL `bytea` columns or buffering entire 60-minute lecture recordings in API memory would degrade database performance and risk out-of-memory crashes on the compute instance.

## Decision
1. **Vultr Managed PostgreSQL (`Npgsql`) for Relational State:**
   - All structured entities (`Users`, `Sessions`, `Participants`, `TranscriptSegments`, `Questions`, `Answers`, `AnswerCitations`, `StudyPacks`, `Flashcards`) and Hangfire job tables live in PostgreSQL via EF Core (`UseNpgsql`), with additive migrations applied at startup.
   - Multi-collection queries use `.AsSplitQuery()` to avoid relational Cartesian explosion.
2. **Vultr Object Storage (`VultrAudioStore`) with Streamed AWS SigV4:**
   - Lecture recordings are stored in Vultr S3-compatible Object Storage (`ewr1.vultrobjects.com`) and referenced on `Session.RecordingKey`.
   - `VultrAudioStore` implements zero-buffer streaming: `PUT` requests use S3 `UNSIGNED-PAYLOAD` SigV4 signing over TLS so the file stream is sent directly to object storage without loading the recording into a `byte[]`, and `GET` requests use `HttpCompletionOption.ResponseHeadersRead` with HTTP Range support (`enableRangeProcessing: true`) so browsers can seek directly to `offsetMs / 1000`.
   - When object storage credentials are omitted in local development, `VultrAudioStore` transparently falls back to local disk under `recordings/`.

## Consequences
- **Positive:** Constant memory footprint (`O(1)` RAM) when uploading or streaming long lecture audio recordings.
- **Positive:** Instant browser `<audio>` scrubbing to any `[mm:ss]` citation via HTTP `206 Partial Content` range requests.
