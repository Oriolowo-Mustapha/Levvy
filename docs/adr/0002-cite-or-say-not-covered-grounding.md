# ADR 0002: Deterministic `cite-or-say-not-covered` Grounding Verification

- **Status:** Accepted
- **Date:** 2026-11-14

## Context
Students rely on **Ask Levvy** during live lectures and **Study Packs** after class to review what their instructor actually taught. Standard LLM chat completions readily answer from pre-training knowledge when a topic hasn't been covered in class, or invent plausible-looking timestamps. In a university lecture setting, ungrounded answers mislead students about exam scope and course material.

## Decision
We enforce a two-layer grounding contract (`cite-or-say-not-covered`):
1. **Prompt-level constraint (`GroqLlmClient`):** The LLM (`openai/gpt-oss-120b` primary, `llama-3.3-70b-versatile` fallback) is given only the numbered `[mm:ss]` transcript segments from the current session and instructed to return JSON citing exact segment timestamps or `status: "not_covered"`.
2. **Deterministic post-verification (`GroundingVerifier`):**
   - Every cited timestamp returned by the LLM is parsed and matched against the session's actual `TranscriptSegment.OffsetMs` values within a strict **±1000ms** tolerance.
   - Any citation that does not resolve to a real transcript segment is stripped.
   - If an `answered` response has zero surviving valid citations, `GroundingVerifier` deterministically downgrades the answer to `status: "not_covered"` and replaces the text with an honest message stating the lecture has not covered that topic yet.
   - Similarly, study pack summaries and flashcards must anchor to valid `SourceOffsetMs` values from the transcript.

## Consequences
- **Positive:** Zero hallucinated citations reach the student UI; every `[mm:ss]` receipt chip is guaranteed to correspond to a real transcript segment and seekable audio offset.
- **Positive:** Grounding rules are unit-tested deterministically (`GroundingVerifierTests`) without requiring live LLM calls.
