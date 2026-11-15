# ADR 0001: Bypass MediatR for the Live Audio Hot Pipe

- **Status:** Accepted
- **Date:** 2026-11-14

## Context
Levvy's core user experience requires sub-3-second latency from the moment a lecturer speaks into their microphone (`MediaRecorder` WebM chunks over WebSockets) to the moment captions and translated subtitles appear on students' screens.

While MediatR CQRS commands and `FluentValidation` pipeline behaviors are ideal for structured state transitions (`CreateSessionCommand`, `JoinSessionCommand`, `AskQuestionCommand`, `EndSessionCommand`), routing high-frequency binary audio frames through MediatR and EF Core change tracking on the hot path adds unnecessary allocations, lock contention, and database round-trips before captions can be broadcast.

## Decision
1. **Hot Audio Pipe in `LectureSessionHub`:** Incoming binary WebSocket frames from the authenticated host are streamed directly into `IAsrStreamer` (`ResilientAsrStreamer` → Deepgram Nova-3 / Groq Whisper) without wrapping each chunk in a MediatR command.
2. **Immediate Broadcast:** As soon as `TranscriptSegment` items return from ASR, `LectureSessionHub` immediately broadcasts `{ "type": "caption" }` frames to connected room clients and fans out parallel `ITranslator` requests for only the distinct target languages currently active in the room.
3. **Out-of-Band Persistence:** Finalized segments are persisted asynchronously via `CaptionPersistenceJob` (Hangfire) using an append-only, idempotent insert (`SessionRepository.AppendSegmentIfMissingAsync`) that does not load the full `Session` entity graph.

## Consequences
- **Positive:** Live caption and translation latency stays low regardless of how long the lecture runs or how many historical segments exist in PostgreSQL.
- **Positive:** Database spikes or transient Hangfire retries cannot block live caption broadcasting in the classroom.
- **Trade-off:** When a host ends a lecture, the frontend waits briefly for outstanding `asr_status: "ready"` acknowledgements before triggering `POST /sessions/{id}/end` so final chunks are captured.
