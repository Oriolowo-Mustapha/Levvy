"use client";
import Link from "next/link";
import { use, useEffect, useRef, useState } from "react";
import type { FlashcardDto, StudyPackResponse } from "../../../../lib/api";
import { errorText, request, roomPath } from "../../../../lib/client";
import {
  AppHeader,
  AudioReceiptChip,
  EmptyState,
  ErrorState,
  Icon,
  LoadingState,
} from "../../../../components/ui";
import {
  AudioPlaybackControl,
  TranscriptFeed,
} from "../../../../components/lecture";

function Flashcard({
  card,
  onPlay,
}: {
  card: FlashcardDto;
  onPlay: (ms: number) => void;
}) {
  const [flipped, setFlipped] = useState(false);
  return (
    <article className="flashcard">
      <span className="eyebrow">
        {flipped ? "THE EXPLANATION" : "A MOMENT TO REMEMBER"}
      </span>
      <button
        className="flashcard-face"
        aria-pressed={flipped}
        onClick={() => setFlipped(!flipped)}
      >
        <span>{flipped ? card.back : card.front}</span>
        <small>↻ {flipped ? "Back to question" : "Reveal answer"}</small>
      </button>
      <AudioReceiptChip offsetMs={card.sourceOffsetMs} onPlay={onPlay} />
    </article>
  );
}
export default function StudyPackPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = use(params);
  const [pack, setPack] = useState<StudyPackResponse | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState("");
  const [active, setActive] = useState<number | null>(null);
  const play = useRef<(ms: number) => void>(() => {});
  async function load(signal?: AbortSignal) {
    setLoading(true);
    setError("");
    try {
      setPack(
        await request<StudyPackResponse>(`${roomPath(code)}/pack`, { signal }),
      );
    } catch (e) {
      if (!(e instanceof DOMException && e.name === "AbortError"))
        setError(errorText(e));
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    const controller = new AbortController();
    request<StudyPackResponse>(`${roomPath(code)}/pack`, {
      signal: controller.signal,
    })
      .then(setPack)
      .catch((e) => {
        if (!controller.signal.aborted) setError(errorText(e));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [code]);
  const onPlay = (ms: number) => play.current(ms);
  const offsets = new Set(pack?.transcript.map((line) => line.offsetMs));
  function summary() {
    return pack?.summary.split(/(\[\d+:\d{2}\])/).map((part, i) => {
      const match = part.match(/^\[(\d+):(\d{2})\]$/);
      const ms = match ? (Number(match[1]) * 60 + Number(match[2])) * 1000 : -1;
      return match && offsets.has(ms) ? (
        <AudioReceiptChip key={i} offsetMs={ms} onPlay={onPlay} />
      ) : (
        <span key={i}>{part}</span>
      );
    });
  }
  return (
    <>
      <AppHeader />
      <main id="main" className="pack-main">
        <Link className="subtle-link" href={`/session/${code}`}>
          <Icon name="back" size={16} />
          Back to lecture
        </Link>
        <div className="page-heading">
          <div>
            <span className="eyebrow">KEEP THE UNDERSTANDING GOING</span>
            <h1>{pack?.title || "Your lecture, made memorable."}</h1>
            <p>Revisit the ideas. Hear the source. Make them yours.</p>
          </div>
          <span className="small-icon">
            <Icon name="spark" />
          </span>
        </div>
        {loading && (
          <LoadingState>Gathering your study materials…</LoadingState>
        )}
        {error && <ErrorState message={error} retry={() => void load()} />}
        {pack && (
          <>
            <AudioPlaybackControl
              code={code}
              onReady={(fn) => {
                play.current = fn;
              }}
              onActive={setActive}
            />
            <section className="summary-panel panel">
              <span className="eyebrow">THE BIG PICTURE</span>
              <h2>Your lecture in focus.</h2>
              {pack.summary ? (
                <div className="summary-text">{summary()}</div>
              ) : (
                <EmptyState title="Still putting the pieces together">
                  Your summary is not ready. Refresh after study-pack generation
                  completes.
                </EmptyState>
              )}
            </section>
            <section className="study-section">
              <div className="section-heading">
                <h2>A little practice. A lasting memory.</h2>
                <span>{pack.flashcards.length} flashcards</span>
              </div>
              <div className="flashcard-grid">
                {pack.flashcards
                  .filter((card) => offsets.has(card.sourceOffsetMs))
                  .map((card) => (
                    <Flashcard key={card.id} card={card} onPlay={onPlay} />
                  ))}
              </div>
              {!pack.flashcards.length && (
                <EmptyState title="No flashcards yet">
                  Check back after the lecture has ended and the study pack is
                  generated.
                </EmptyState>
              )}
            </section>
            <TranscriptFeed
              lines={pack.transcript}
              onPlay={onPlay}
              active={active}
            />
          </>
        )}
      </main>
    </>
  );
}
