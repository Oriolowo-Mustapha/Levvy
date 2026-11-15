"use client";
import { use, useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  AppHeader,
  BrowserReady,
  ConnectionIndicator,
  ErrorState,
  Icon,
  LanguageSelector,
  LoadingState,
  RecordingStatus,
  TextSizeButton,
} from "../../../components/ui";
import {
  AskLevvyPanel,
  AudioPlaybackControl,
  OriginalTranscriptCard,
  TranscriptFeed,
  TranslatedTranscriptCard,
} from "../../../components/lecture";
import { useLecture } from "../../../lib/use-lecture";
import {
  preference,
  readAuth,
  request,
  roomPath,
  speakSessionText,
  storePreference,
} from "../../../lib/client";

export default function SessionPage({
  params,
}: {
  params: Promise<{ code: string }>;
}) {
  const { code } = use(params);
  return (
    <BrowserReady>
      <SessionContent code={code} />
    </BrowserReady>
  );
}
function SessionContent({ code }: { code: string }) {
  const [language, setLanguage] = useState(
    () => readAuth()?.user.preferredLanguage || preference("language", "fr"),
  );
  const [size, setSize] = useState(
    () =>
      ({ sm: 20, md: 24, lg: 30, xl: 36 })[
        readAuth()?.user.captionFontSize || "md"
      ],
  );
  const [active, setActive] = useState<number | null>(null);
  const [speakingTranslation, setSpeakingTranslation] = useState(false);
  const ttsAudio = useRef<HTMLAudioElement | null>(null);
  const play = useRef<(ms: number) => void>(() => {});
  const lecture = useLecture(code, language);
  useEffect(
    () => () => {
      ttsAudio.current?.pause();
      if (typeof window !== "undefined" && "speechSynthesis" in window) {
        window.speechSynthesis.cancel();
      }
    },
    [],
  );
  async function speakTranslation(text: string) {
    if (speakingTranslation) {
      ttsAudio.current?.pause();
      ttsAudio.current = null;
      if (typeof window !== "undefined" && "speechSynthesis" in window) {
        window.speechSynthesis.cancel();
      }
      setSpeakingTranslation(false);
      return;
    }
    setSpeakingTranslation(true);
    try {
      const audio = await speakSessionText(code, text, language);
      ttsAudio.current = audio;
      audio.addEventListener(
        "ended",
        () => {
          ttsAudio.current = null;
          setSpeakingTranslation(false);
        },
        { once: true },
      );
      audio.addEventListener(
        "error",
        () => {
          ttsAudio.current = null;
          setSpeakingTranslation(false);
        },
        { once: true },
      );
    } catch {
      if (typeof window !== "undefined" && "speechSynthesis" in window) {
        window.speechSynthesis.cancel();
        const utterance = new SpeechSynthesisUtterance(text);
        utterance.lang = language;
        utterance.onend = () => setSpeakingTranslation(false);
        utterance.onerror = () => setSpeakingTranslation(false);
        window.speechSynthesis.speak(utterance);
      } else {
        setSpeakingTranslation(false);
      }
    }
  }
  const last = lecture.lines.at(-1);
  const translation =
    last && lecture.translations[`${language}:${last.offsetMs}`];
  const [now, setNow] = useState(0);
  useEffect(() => {
    const auth = readAuth();
    const timer = setInterval(() => setNow(Date.now()), 2000);
    if (!sessionStorage.getItem(`levvy.participant.${code}`)) {
      void request<{ participantId: string }>(`${roomPath(code)}/join`, {
        method: "POST",
        body: JSON.stringify({
          displayName: auth?.user.firstName || "Student",
          preferredLanguage: preference("language", "fr"),
        }),
      })
        .then((data) =>
          sessionStorage.setItem(
            `levvy.participant.${code}`,
            data.participantId,
          ),
        )
        .catch(() => {});
    }
    return () => clearInterval(timer);
  }, [code]);
  const receiving =
    lecture.connection === "Connected" &&
    lecture.lastCaption > 0 &&
    now - lecture.lastCaption < 15000;
  const onPlay = (ms: number) => play.current(ms);
  function highlight(ms: number) {
    const line = [...lecture.lines]
      .reverse()
      .find((line) => line.offsetMs <= ms);
    setActive(line?.offsetMs ?? null);
  }
  const status =
    lecture.session && !lecture.session.isLive
      ? "Session ended"
      : receiving
        ? "Listening…"
        : lecture.connection === "Connected"
          ? "Waiting for captions"
          : lecture.connection;
  return (
    <>
      <AppHeader back={{ href: "/dashboard", label: "All lectures" }} />
      <main id="main" className="workspace">
        <div className="lecture-main">
          <div className="lecture-heading">
            <RecordingStatus state={status} recording={receiving} />
            <h1>{lecture.session?.title || "Lecture room"}</h1>
            <p className="lecture-meta">
              <span className="num">Room {code}</span>
              {lecture.session && (
                <span>{lecture.session.participantCount} participants</span>
              )}
              <ConnectionIndicator state={lecture.connection} />
            </p>
          </div>
          {lecture.error && <ErrorState message={lecture.error} />}
          {!lecture.session && !lecture.error && <LoadingState />}
          <section
            id="captions"
            className="caption-stack"
            aria-label="Live original and translated speech"
          >
            <OriginalTranscriptCard
              language={lecture.sourceLanguage}
              lines={lecture.lines}
              size={size}
              onPlay={onPlay}
              onSpeak={(text) => void speakTranslation(text)}
              speaking={speakingTranslation}
              active={last?.offsetMs === active}
              activeOffset={active}
            />
            <div className="caption-bridge">
              <button
                type="button"
                aria-label="Change caption language"
                onClick={() =>
                  document
                    .querySelector<HTMLSelectElement>(".translated select")
                    ?.focus()
                }
              >
                <Icon name="swap" size={15} />
              </button>
            </div>
            <TranslatedTranscriptCard
              language={language}
              languageControl={
                <LanguageSelector
                  caption="To"
                  value={language}
                  onChange={(v) => {
                    setLanguage(v);
                    storePreference("language", v);
                  }}
                />
              }
              lines={lecture.lines.flatMap((item) => {
                const translatedText =
                  lecture.translations[`${language}:${item.offsetMs}`] ??
                  (language === lecture.sourceLanguage ? item.text : undefined);
                return translatedText ? [{ ...item, text: translatedText }] : [];
              })}
              size={size}
              onPlay={onPlay}
              onSpeak={(text) => void speakTranslation(text)}
              speaking={speakingTranslation}
              active={last?.offsetMs === active}
              activeOffset={active}
            />
          </section>
          <AskLevvyPanel
            code={code}
            onPlay={onPlay}
            offsets={lecture.lines.map((line) => line.offsetMs)}
            leading={<TextSizeButton value={size} onChange={setSize} />}
            trailing={
              <a
                className="icon-button"
                href="#transcript"
                aria-label="Jump to full transcript"
                title="Full transcript"
              >
                <Icon name="list" size={16} />
              </a>
            }
          />
        </div>
        <aside className="lecture-rail" id="transcript">
          <AudioPlaybackControl
            code={code}
            onReady={(fn) => {
              play.current = fn;
            }}
            onActive={highlight}
          />
          <Link className="rail-link" href={`/session/${code}/pack`}>
            <span className="icon-chip">
              <Icon name="book" size={15} />
            </span>
            <span>
              <strong>
                {lecture.session && !lecture.session.isLive
                  ? "Open study pack & flashcards"
                  : "Study pack"}
              </strong>
              <small>
                {lecture.session && !lecture.session.isLive
                  ? "Lecture ended — summary & flashcards"
                  : "Summary and flashcards after class"}
              </small>
            </span>
            <Icon name="arrow" size={15} />
          </Link>
          <TranscriptFeed
            lines={lecture.lines}
            onPlay={onPlay}
            active={active}
          />
        </aside>
      </main>
    </>
  );
}
