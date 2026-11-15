"use client";
import {
  useEffect,
  useRef,
  useState,
  type FormEvent,
  type KeyboardEvent,
  type ReactNode,
} from "react";
import Link from "next/link";
import {
  formatMmSs,
  getSessionAudioUrl,
  type AskAnswerResponse,
  type TranscriptLineDto,
} from "../lib/api";
import {
  request,
  roomPath,
  errorText,
  speakSessionText,
} from "../lib/client";
import {
  AudioReceiptChip,
  CopyButton,
  EmptyState,
  ErrorState,
  Icon,
  LanguagePill,
} from "./ui";

export function LiveCaptionCard({
  dark = false,
  language,
  languageControl,
  line,
  lines,
  onPlay,
  onSpeak,
  speaking = false,
  size = 24,
  active = false,
  activeOffset = null,
}: {
  dark?: boolean;
  language: string;
  /** Replaces the static language pill, e.g. with a LanguageSelector. */
  languageControl?: ReactNode;
  line?: TranscriptLineDto;
  lines?: TranscriptLineDto[];
  onPlay: (ms: number) => void;
  onSpeak?: (text: string) => void;
  speaking?: boolean;
  size?: number;
  active?: boolean;
  activeOffset?: number | null;
}) {
  const card = useRef<HTMLElement>(null);
  const streamRef = useRef<HTMLDivElement>(null);
  const items = lines && lines.length > 0 ? lines : line ? [line] : [];
  const latest = items[items.length - 1];
  const fullText = items
    .map((item) => item.text)
    .filter(Boolean)
    .join(" ");

  useEffect(() => {
    const el = streamRef.current;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  }, [items.length, latest?.text]);

  return (
    <article
      ref={card}
      className={`caption-card ${dark ? "translated" : "original"} ${active ? "playing" : ""}`}
      aria-label={dark ? "Translated captions" : "Original captions"}
    >
      <div className="caption-card-top">
        <button
          type="button"
          className="icon-button"
          aria-label="Show captions full screen"
          onClick={() => {
            if (document.fullscreenElement) void document.exitFullscreen();
            else void card.current?.requestFullscreen?.().catch(() => {});
          }}
        >
          <Icon name="expand" size={15} />
        </button>
        <button
          type="button"
          className={`icon-button${speaking ? " speaking" : ""}`}
          disabled={!latest}
          aria-pressed={onSpeak ? speaking : undefined}
          aria-label={
            onSpeak
              ? speaking
                ? "Stop translated speech"
                : "Listen to translated speech"
              : latest
                ? `Listen from ${formatMmSs(latest.offsetMs)}`
                : "Nothing to listen to yet"
          }
          onClick={() => {
            if (onSpeak && fullText) onSpeak(fullText);
            else if (latest) onPlay(latest.offsetMs);
          }}
        >
          <Icon name={speaking ? "stop" : "volume"} size={15} />
        </button>
        {languageControl || (
          <LanguagePill value={language} caption={dark ? "To" : "From"} />
        )}
        <span className="caption-tools">
          {fullText && <CopyButton text={fullText} />}
        </span>
      </div>
      <div
        ref={streamRef}
        className="caption-text caption-stream"
        aria-live="polite"
        aria-atomic="false"
        dir="auto"
        lang={language === "auto" ? undefined : language}
        style={{ fontSize: size }}
      >
        {items.length > 0 ? (
          items.map((item, idx) => {
            const isLatest = idx === items.length - 1;
            const isPlaying = activeOffset === item.offsetMs;
            return (
              <span
                key={item.offsetMs}
                className={`caption-segment${isLatest ? " latest" : ""}${isPlaying ? " playing" : ""}`}
                onClick={() => onPlay(item.offsetMs)}
                title={`Play from ${formatMmSs(item.offsetMs)}`}
              >
                {item.text}{" "}
              </span>
            );
          })
        ) : (
          <span className="caption-placeholder">
            {dark
              ? "Your translation will appear here."
              : "Waiting for the lecturer to speak…"}
          </span>
        )}
      </div>
      {latest && (
        <time className="caption-time num">{formatMmSs(latest.offsetMs)}</time>
      )}
    </article>
  );
}
export const OriginalTranscriptCard = (
  props: Omit<Parameters<typeof LiveCaptionCard>[0], "dark">,
) => <LiveCaptionCard {...props} />;
export const TranslatedTranscriptCard = (
  props: Omit<Parameters<typeof LiveCaptionCard>[0], "dark">,
) => <LiveCaptionCard {...props} dark />;
export function TranscriptFeed({
  lines,
  onPlay,
  active,
  size = 18,
}: {
  lines: TranscriptLineDto[];
  onPlay: (ms: number) => void;
  active: number | null;
  size?: number;
}) {
  return (
    <section className="transcript-feed" aria-label="Full lecture transcript">
      <div className="section-heading">
        <h2>Transcript</h2>
        <span>{lines.length} segments</span>
      </div>
      {lines.length === 0 ? (
        <EmptyState title="Ready when your lecturer is">
          Captions will appear as speech is transcribed. There is no transcript
          yet.
        </EmptyState>
      ) : (
        <div className="transcript-list">
          {lines.map((line) => (
            <div
              key={line.offsetMs}
              className={`transcript-line ${line.offsetMs === active ? "active" : ""}`}
            >
              <AudioReceiptChip offsetMs={line.offsetMs} onPlay={onPlay} />
              <p dir="auto" style={{ fontSize: size }}>
                {line.text}
              </p>
            </div>
          ))}
        </div>
      )}
    </section>
  );
}
export function AudioPlaybackControl({
  code,
  onReady,
  onActive,
}: {
  code: string;
  onReady: (play: (ms: number) => void) => void;
  onActive: (ms: number) => void;
}) {
  const audio = useRef<HTMLAudioElement>(null);
  const [error, setError] = useState("");
  const [loaded, setLoaded] = useState(false);
  function play(ms: number) {
    const el = audio.current;
    if (!el) return;
    setError("");
    el.currentTime = ms / 1000;
    void el
      .play()
      .then(() => onActive(ms))
      .catch(() =>
        setError(
          "Recording unavailable or still being saved. Try again after the lecture.",
        ),
      );
  }
  return (
    <div className="audio-player">
      <div className="audio-player-head">
        <span className="icon-chip">
          <Icon name="headphones" size={15} />
        </span>
        <span>
          <strong>Lecture recording</strong>
          <small>
            {loaded
              ? "Ready to revisit"
              : "Available when saved by your lecturer"}
          </small>
        </span>
      </div>
      <audio
        ref={(el) => {
          audio.current = el;
          if (el) onReady(play);
        }}
        controls
        preload="none"
        src={getSessionAudioUrl(code)}
        onLoadedMetadata={() => setLoaded(true)}
        onTimeUpdate={() => {
          if (audio.current) onActive(audio.current.currentTime * 1000);
        }}
        onError={() => setError("No recording is available yet.")}
        aria-label="Lecture audio recording"
      />
      {error && (
        <p className="muted" role="status">
          {error}
        </p>
      )}
    </div>
  );
}
/**
 * Ask Levvy as the reference's bottom composer: [leading] [input · send]
 * [trailing]. The answer card renders just above it.
 */
export function AskLevvyPanel({
  code,
  onPlay,
  offsets,
  leading,
  trailing,
}: {
  code: string;
  onPlay: (ms: number) => void;
  offsets: number[];
  leading?: ReactNode;
  trailing?: ReactNode;
}) {
  const [question, setQuestion] = useState("");
  const [asked, setAsked] = useState("");
  const [answer, setAnswer] = useState<AskAnswerResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [speaking, setSpeaking] = useState(false);
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    if (!question.trim() || busy) return;
    setBusy(true);
    setError("");
    try {
      const data = await request<AskAnswerResponse>(`${roomPath(code)}/ask`, {
        method: "POST",
        body: JSON.stringify({ text: question }),
      });
      setAsked(question);
      setAnswer(data);
      setQuestion("");
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  function onKeyDown(e: KeyboardEvent<HTMLTextAreaElement>) {
    if (e.key === "Enter" && !e.shiftKey && !e.nativeEvent.isComposing) {
      e.preventDefault();
      e.currentTarget.form?.requestSubmit();
    }
  }
  const receipts = (
    answer?.receiptOffsetsMs ||
    answer?.receipts?.map((n) => n * 1000) ||
    []
  ).filter((n) => Number.isFinite(n) && offsets.includes(n));
  const grounded = answer?.status === "ok" && receipts.length > 0;
  return (
    <>
      {error && <ErrorState message={error} />}
      <div role="status" className="answer-slot">
        {busy && <p className="answer-pending">Checking the lecture…</p>}
        {answer && (
          <div className="answer-card">
            <div className="answer-head">
              <span className="icon-chip">
                <Icon name="spark" size={14} />
              </span>
              <span className={`answer-label ${grounded ? "verified" : ""}`}>
                {grounded
                  ? "Supported by this lecture"
                  : "Not verified in this lecture"}
              </span>
              <button
                type="button"
                className="icon-button"
                aria-label="Dismiss answer"
                onClick={() => setAnswer(null)}
              >
                <Icon name="plus" size={14} />
              </button>
            </div>
            {asked && <p className="answer-question">{asked}</p>}
            <p>
              {grounded || answer.status === "not_covered"
                ? answer.text
                : "Levvy could not verify this answer against a valid source. Please try another question."}
            </p>
            {grounded && (
              <div className="receipts">
                {receipts.map((ms) => (
                  <AudioReceiptChip key={ms} offsetMs={ms} onPlay={onPlay} />
                ))}
              </div>
            )}
            {(grounded || answer.status === "not_covered") && (
              <button
                type="button"
                className="button secondary"
                disabled={speaking}
                onClick={async () => {
                  setSpeaking(true);
                  setError("");
                  try {
                    const audio = await speakSessionText(code, answer.text);
                    audio.addEventListener("ended", () => setSpeaking(false), {
                      once: true,
                    });
                  } catch (e) {
                    setError(errorText(e));
                    setSpeaking(false);
                  }
                }}
              >
                <Icon name="volume" size={14} />
                {speaking ? "Speaking…" : "Read answer aloud"}
              </button>
            )}
          </div>
        )}
      </div>
      <form className="composer" id="ask" onSubmit={submit}>
        {leading}
        <div className="composer-field">
          <label className="sr-only" htmlFor="question">
            Your question about this lecture
          </label>
          <textarea
            id="question"
            required
            value={question}
            onChange={(e) => setQuestion(e.target.value)}
            onKeyDown={onKeyDown}
            placeholder="Ask about this lecture…"
            rows={1}
            maxLength={2000}
          />
          <button
            type="submit"
            className="send-button"
            aria-label="Ask a question"
            disabled={busy || !question.trim()}
          >
            {busy ? (
              <span className="spinner light" />
            ) : (
              <Icon name="arrowUp" size={16} />
            )}
          </button>
        </div>
        {trailing}
      </form>
    </>
  );
}
export function SessionCard({
  title,
  code,
  live,
  hosted = false,
  participantCount,
  hasRecording,
  ending = false,
  onEnd,
  children,
}: {
  title: string;
  code: string;
  live?: boolean;
  hosted?: boolean;
  participantCount?: number;
  hasRecording?: boolean;
  ending?: boolean;
  onEnd?: () => void;
  children?: ReactNode;
}) {
  const primaryHref =
    hosted && live ? `/host?code=${encodeURIComponent(code)}` : `/session/${code}`;
  const primaryLabel = hosted
    ? live
      ? "Open host console"
      : "View lecture"
    : live
      ? "Join lecture"
      : "Revisit";
  return (
    <article className="session-card">
      <div className="session-card-top">
        <span className={`status-pill ${live ? "live" : ""}`}>
          <span />
          {live ? "In progress" : "Ended"}
        </span>
        <span className="num muted">
          {hosted ? "Hosted · " : ""}
          {code}
        </span>
      </div>
      <h3>
        <Link href={primaryHref}>{title}</Link>
      </h3>
      {(typeof participantCount === "number" || hasRecording) && (
        <p className="muted" style={{ fontSize: 13, margin: 0 }}>
          {typeof participantCount === "number"
            ? `${participantCount} ${participantCount === 1 ? "student" : "students"}`
            : ""}
          {typeof participantCount === "number" && hasRecording ? " · " : ""}
          {hasRecording ? "Recording saved" : ""}
        </p>
      )}
      <div className="session-card-footer">
        <Link className="button primary small" href={primaryHref}>
          {primaryLabel}
          <Icon name="arrow" size={14} />
        </Link>
        {hosted && live && onEnd && (
          <button
            type="button"
            className="button secondary small"
            disabled={ending}
            onClick={onEnd}
          >
            {ending ? "Ending…" : "End lecture"}
          </button>
        )}
        {children}
      </div>
    </article>
  );
}
export function StudyPackCard({
  code,
  title,
  children,
}: {
  code: string;
  title: string;
  children?: ReactNode;
}) {
  return (
    <article className="study-card">
      <h3>
        <Link href={`/session/${code}/pack`}>{title}</Link>
      </h3>
      {children}
      <Link className="pill-link" href={`/session/${code}/pack`}>
        Open study pack
        <Icon name="arrow" size={14} />
      </Link>
    </article>
  );
}
