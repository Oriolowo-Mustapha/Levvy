"use client";
import { use, useEffect, useRef, useState } from "react";
import Link from "next/link";
import {
  AppHeader,
  BrowserReady,
  CaptionSizeControl,
  ConnectionIndicator,
  ErrorState,
  Icon,
  LanguageSelector,
  LoadingState,
  RecordingStatus,
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
  const play = useRef<(ms: number) => void>(() => {});
  const lecture = useLecture(code, language);
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
  return (
    <>
      <AppHeader />
      <main id="main" className="workspace">
        <aside className="lecture-sidebar">
          <Link className="subtle-link" href="/dashboard">
            <Icon name="back" size={16} />
            All lectures
          </Link>
          <span className="eyebrow">YOUR LECTURE SPACE</span>
          <h2>{lecture.session?.title || "Lecture room"}</h2>
          <span className="room-badge">ROOM {code}</span>
          <nav aria-label="Lecture navigation">
            <a className="selected" href="#captions">
              <Icon name="headphones" />
              Live captions
            </a>
            <a href="#transcript">
              <Icon name="book" />
              Transcript
            </a>
            <a href="#ask">
              <Icon name="spark" />
              Ask Levvy
            </a>
            <Link href={`/session/${code}/pack`}>
              <Icon name="history" />
              Study pack
            </Link>
          </nav>
          <div className="sidebar-note">
            Every word belongs to you.
            <p>
              {lecture.session
                ? `${lecture.session.participantCount} participants`
                : ""}
            </p>
            <ConnectionIndicator state={lecture.connection} />
          </div>
        </aside>
        <div className="lecture-main">
          <div className="page-heading">
            <div>
              <span className="eyebrow">FOLLOW AT YOUR OWN PACE</span>
              <h1>Room for understanding.</h1>
            </div>
            <RecordingStatus
              state={
                lecture.session && !lecture.session.isLive
                  ? "Session ended"
                  : receiving
                    ? "Receiving captions"
                    : lecture.connection === "Connected"
                      ? "Waiting for captions"
                      : lecture.connection
              }
            />
          </div>
          {lecture.error && <ErrorState message={lecture.error} />}
          {!lecture.session && !lecture.error && <LoadingState />}
          <section
            id="captions"
            className="caption-stack"
            aria-label="Latest original and translated speech"
          >
            <OriginalTranscriptCard
              language={lecture.sourceLanguage}
              line={last}
              size={size}
              onPlay={onPlay}
              active={last?.offsetMs === active}
            />
            <button
              type="button"
              className="caption-bridge"
              aria-label="Change caption language"
              onClick={() =>
                document
                  .querySelector<HTMLSelectElement>(
                    "#caption-preferences select",
                  )
                  ?.focus()
              }
            >
              <Icon name="swap" size={17} />
            </button>
            <TranslatedTranscriptCard
              language={language}
              line={
                translation && last
                  ? { ...last, text: translation }
                  : language === lecture.sourceLanguage
                    ? last
                    : undefined
              }
              size={size}
              onPlay={onPlay}
              active={last?.offsetMs === active}
            />
          </section>
          <AudioPlaybackControl
            code={code}
            onReady={(fn) => {
              play.current = fn;
            }}
            onActive={highlight}
          />
          <div id="transcript">
            <TranscriptFeed
              lines={lecture.lines}
              onPlay={onPlay}
              active={active}
              size={Math.max(20, size - 4)}
            />
          </div>
        </div>
        <aside className="lecture-rail">
          <section className="preferences-card" id="caption-preferences">
            <span className="eyebrow">MAKE IT YOURS</span>
            <h2>A familiar language.</h2>
            <LanguageSelector
              value={language}
              onChange={(v) => {
                setLanguage(v);
                storePreference("language", v);
              }}
            />
            <CaptionSizeControl value={size} onChange={setSize} />
            <p>
              Language changes apply to incoming captions. Your lecture keeps
              going.
            </p>
          </section>
          <AskLevvyPanel
            code={code}
            onPlay={onPlay}
            offsets={lecture.lines.map((line) => line.offsetMs)}
          />
        </aside>
      </main>
    </>
  );
}
