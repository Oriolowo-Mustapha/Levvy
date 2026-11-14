"use client";
import { useRef, useState, type FormEvent, type ReactNode } from "react";
import Link from "next/link";
import {
  getSessionAudioUrl,
  type AskAnswerResponse,
  type TranscriptLineDto,
} from "../lib/api";
import { request, roomPath, errorText } from "../lib/client";
import {
  AudioReceiptChip,
  CopyButton,
  EmptyState,
  ErrorState,
  Icon,
  languageName,
  PrimaryActionButton,
} from "./ui";

export function LiveCaptionCard({
  dark = false,
  language,
  line,
  onPlay,
  size = 24,
  active = false,
}: {
  dark?: boolean;
  language: string;
  line?: TranscriptLineDto;
  onPlay: (ms: number) => void;
  size?: number;
  active?: boolean;
}) {
  return (
    <article
      className={`caption-card ${dark ? "translated" : "original"} ${active ? "playing" : ""}`}
    >
      <div className="caption-card-top">
        <span className="caption-language">
          <Icon name="volume" size={17} />
          <span>
            <small>{dark ? "TRANSLATED CAPTIONS" : "ORIGINAL CAPTIONS"}</small>
            <strong>{languageName(language)}</strong>
          </span>
        </span>
        <span className="caption-tools">
          {line && <CopyButton text={line.text} />}
        </span>
      </div>
      <div
        className="caption-text"
        aria-live="polite"
        aria-atomic="true"
        dir="auto"
        lang={language === "auto" ? undefined : language}
        style={{ fontSize: size }}
      >
        {line?.text || (
          <span className="caption-placeholder">
            {dark
              ? "Your translation will appear here."
              : "Every word, a little clearer."}
          </span>
        )}
      </div>
      <div className="caption-card-bottom">
        {line ? (
          <AudioReceiptChip offsetMs={line.offsetMs} onPlay={onPlay} />
        ) : (
          <span>
            Waiting for {dark ? "translated captions" : "the lecturer"}…
          </span>
        )}
        <span>{dark ? "In your language" : "Lecture audio"}</span>
      </div>
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
  size = 20,
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
        lines.map((line) => (
          <div
            key={line.offsetMs}
            className={`transcript-line ${line.offsetMs === active ? "active" : ""}`}
          >
            <AudioReceiptChip offsetMs={line.offsetMs} onPlay={onPlay} />
            <p dir="auto" style={{ fontSize: size }}>
              {line.text}
            </p>
          </div>
        ))
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
      <div>
        <Icon name="headphones" size={18} />
        <span>Lecture recording</span>
        <small>
          {loaded
            ? "Ready to revisit"
            : "Available when saved by your lecturer"}
        </small>
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
export function AskLevvyPanel({
  code,
  onPlay,
  offsets,
}: {
  code: string;
  onPlay: (ms: number) => void;
  offsets: number[];
}) {
  const [question, setQuestion] = useState("");
  const [answer, setAnswer] = useState<AskAnswerResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const data = await request<AskAnswerResponse>(`${roomPath(code)}/ask`, {
        method: "POST",
        body: JSON.stringify({ text: question }),
      });
      setAnswer(data);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  const receipts = (
    answer?.receiptOffsetsMs ||
    answer?.receipts?.map((n) => n * 1000) ||
    []
  ).filter((n) => Number.isFinite(n) && offsets.includes(n));
  const grounded = answer?.status === "ok" && receipts.length > 0;
  return (
    <section className="ask-panel" id="ask">
      <div className="section-heading">
        <h2>
          <Icon name="spark" size={18} />
          Ask Levvy
        </h2>
        <span className="mini-badge">AI</span>
      </div>
      <p>
        A little clarity, right when you need it. Answers come from this
        lecture.
      </p>
      <form onSubmit={submit}>
        <label className="sr-only" htmlFor="question">
          Your question about this lecture
        </label>
        <textarea
          id="question"
          required
          value={question}
          onChange={(e) => setQuestion(e.target.value)}
          placeholder="Could you explain that last point?"
          rows={4}
          maxLength={2000}
        />
        <PrimaryActionButton disabled={busy || !question.trim()}>
          {busy ? "Checking the lecture…" : "Ask a question"}
          <Icon name="arrow" size={17} />
        </PrimaryActionButton>
      </form>
      {error && <ErrorState message={error} />}
      <div role="status">
        {answer && (
          <div className="answer-card">
            <span className={`answer-label ${grounded ? "verified" : ""}`}>
              {grounded
                ? "✓ Supported by this lecture"
                : "Not verified in this lecture"}
            </span>
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
          </div>
        )}
      </div>
      <div className="ask-footnote">
        <Icon name="book" size={14} />
        Always grounded. Never a guess.
      </div>
    </section>
  );
}
export function SessionCard({
  title,
  code,
  live,
  children,
}: {
  title: string;
  code: string;
  live?: boolean;
  children?: ReactNode;
}) {
  return (
    <article className="session-card">
      <div className="session-card-icon">
        <Icon name="book" />
      </div>
      <span className="eyebrow">
        {live ? "LECTURE IN PROGRESS" : "YOUR LECTURE"}
      </span>
      <h3>
        <Link href={`/session/${code}`}>{title}</Link>
      </h3>
      <p>Room {code}</p>
      <div className="session-card-footer">
        <Link href={`/session/${code}`}>
          {live ? "Join lecture" : "Revisit lecture"}
          <Icon name="arrow" size={15} />
        </Link>
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
      <Icon name="spark" />
      <h3>
        <Link href={`/session/${code}/pack`}>{title}</Link>
      </h3>
      {children}
      <Link className="subtle-link" href={`/session/${code}/pack`}>
        Open study pack
        <Icon name="arrow" size={16} />
      </Link>
    </article>
  );
}
