"use client";
import Link from "next/link";
import { useEffect, useRef, useState, type FormEvent } from "react";
import {
  AppHeader,
  BrowserReady,
  AudioVisualizer,
  CopyButton,
  ConnectionIndicator,
  ErrorState,
  Icon,
  LanguageSelector,
  LevelDots,
  PillDot,
  PrimaryActionButton,
  RecordingStatus,
  RecordingTimer,
  SecondaryActionButton,
} from "../../components/ui";
import { JoinQr } from "../../components/join-qr";
import { useLecture } from "../../lib/use-lecture";
import { useRecorder } from "../../lib/use-recorder";
import {
  errorText,
  preference,
  readAuth,
  request,
  storePreference,
  uploadAudio,
} from "../../lib/client";
import {
  formatMmSs,
  type CreateSessionResponse,
  type SessionSummaryResponse,
} from "../../lib/api";

export default function HostPage() {
  return (
    <BrowserReady>
      <HostContent />
    </BrowserReady>
  );
}
function HostContent() {
  const [created, setCreated] = useState<CreateSessionResponse | null>(null);
  const [title, setTitle] = useState("");
  const [source, setSource] = useState(() => preference("source", "en"));
  const [target, setTarget] = useState(() => preference("language", "fr"));
  const [audioSource, setAudioSource] = useState<"mic" | "tab" | "upload">(
    "mic",
  );
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  const [saved, setSaved] = useState(false);
  const [ended, setEnded] = useState(false);
  const signedIn = !!readAuth();
  const [hasPending, setHasPending] = useState(false);
  const link = created ? `${location.origin}/session/${created.code}` : "";
  const [confirm, setConfirm] = useState(false);
  const dialog = useRef<HTMLDialogElement>(null);
  const pendingBlob = useRef<Blob | null>(null);
  const lecture = useLecture(created?.code || "", target, "host");
  const recorder = useRecorder(lecture.send, source);
  const transcriptBox = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (created) return;
    const codeParam = new URLSearchParams(window.location.search)
      .get("code")
      ?.trim()
      .toUpperCase();
    if (!codeParam) return;
    let cancelled = false;
    void request<SessionSummaryResponse>(
      `/sessions/${encodeURIComponent(codeParam)}`,
    )
      .then((summary) => {
        if (cancelled) return;
        setCreated({ sessionId: summary.id, code: summary.code });
        setTitle(summary.title);
        setEnded(!summary.isLive);
      })
      .catch((e) => {
        if (!cancelled) setError(errorText(e));
      });
    return () => {
      cancelled = true;
    };
  }, [created]);
  useEffect(() => {
    const el = transcriptBox.current;
    if (!el) return;
    el.scrollTop = el.scrollHeight;
  }, [lecture.lines.length]);
  useEffect(() => {
    if (confirm) dialog.current?.showModal();
    else dialog.current?.close();
  }, [confirm]);
  useEffect(() => {
    function beforeUnload(e: BeforeUnloadEvent) {
      if (recorder.recording || pendingBlob.current) {
        e.preventDefault();
      }
    }
    window.addEventListener("beforeunload", beforeUnload);
    return () => window.removeEventListener("beforeunload", beforeUnload);
  }, [recorder.recording]);
  async function create(e: FormEvent) {
    e.preventDefault();
    setBusy(true);
    setError("");
    try {
      const auth = readAuth();
      if (!auth) throw new Error("Sign in before hosting a lecture.");
      const data = await request<CreateSessionResponse>("/sessions", {
        method: "POST",
        body: JSON.stringify({ title, hostId: auth.user.id }),
      });
      setCreated(data);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  async function save() {
    if (!created) return;
    setBusy(true);
    setError("");
    try {
      const blob = pendingBlob.current || (await recorder.stop());
      if (!blob?.size) throw new Error("There is no recording to save yet.");
      pendingBlob.current = blob;
      setHasPending(true);
      await uploadAudio(created.sessionId, blob);
      pendingBlob.current = null;
      setHasPending(false);
      setSaved(true);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  async function end() {
    if (!created) return;
    setConfirm(false);
    setBusy(true);
    setError("");
    try {
      const blob = pendingBlob.current || (await recorder.stop());
      if (blob?.size) {
        pendingBlob.current = blob;
        setHasPending(true);
        await uploadAudio(created.sessionId, blob);
        pendingBlob.current = null;
        setHasPending(false);
        setSaved(true);
      }
      await lecture.drainAudio();
      await request(`/sessions/${created.sessionId}/end`, { method: "POST" });
      setEnded(true);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  async function upload(file: File | undefined) {
    if (!file || !created) return;
    setBusy(true);
    setError("");
    try {
      if (!file.type.startsWith("audio/webm"))
        throw new Error(
          "The current playback contract supports WebM recordings. Choose an audio .webm file.",
        );
      await uploadAudio(created.sessionId, file);
      setSaved(true);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  const locked = recorder.recording || busy;
  const errors = [error, recorder.error, lecture.error].filter(Boolean);
  if (!created)
    return (
      <>
        <AppHeader active="host" />
        <main id="main" className="home-main">
          <section className="home-hero">
            <AudioVisualizer hero />
            <h1>
              Host a Lecture
              <span>Everyone Can Follow</span>
            </h1>
            <p>
              Name your lecture and we’ll open a room your students can join
              from any phone or laptop.
            </p>
          </section>
          {!signedIn && (
            <div className="notice">
              Hosting requires an account. <Link href="/login">Sign in</Link> or{" "}
              <Link href="/signup">create an account</Link> to begin.
            </div>
          )}
          <form className="composer-card" onSubmit={create}>
            <LanguageSelector
              label="Spoken lecture language"
              caption="Speaker language"
              value={source}
              onChange={(v) => {
                setSource(v);
                storePreference("source", v);
              }}
              includeAuto
            />
            <label className="entry-box">
              <span className="entry-label">Lecture title</span>
              <input
                required
                maxLength={200}
                value={title}
                onChange={(e) => setTitle(e.target.value)}
                placeholder="e.g. Introduction to microeconomics"
              />
            </label>
            {errors.map((message, i) => (
              <ErrorState key={i} message={message} />
            ))}
            <div className="pill-switch">
              <Link className="button secondary" href="/dashboard">
                My lectures
                <Icon name="history" size={15} />
              </Link>
              <PrimaryActionButton disabled={busy || !signedIn}>
                {busy ? "Creating…" : "Create lecture room"}
                <PillDot />
              </PrimaryActionButton>
            </div>
          </form>
        </main>
      </>
    );
  const fullTranscript = lecture.lines
    .map((line) => line.text)
    .filter(Boolean)
    .join(" ");
  return (
    <>
      <AppHeader active="host" />
      <main id="main" className="host-main">
        <section className="host-console" aria-labelledby="host-title">
          <div className="listening-head">
            <RecordingStatus
              state={
                ended
                  ? "Lecture ended"
                  : recorder.paused
                    ? "Paused"
                    : recorder.recording
                      ? "On air — Listening…"
                      : recorder.state
              }
              recording={
                recorder.recording && !recorder.paused && recorder.level > 0.01
              }
            />
            <div className="host-header-meta">
              <span className="host-stat-pill">
                <strong className="num">
                  {lecture.session?.participantCount ?? 0}
                </strong>{" "}
                students
              </span>
              <span className="host-stat-pill">
                <strong className="num">{lecture.lines.length}</strong> segments
              </span>
              <ConnectionIndicator state={lecture.connection} />
            </div>
          </div>

          <div className="host-stage-card">
            <div className="host-stage-top">
              <div>
                <span className="host-kicker">Lecturer broadcast console</span>
                <h1 id="host-title" className="host-stage-title">
                  {title}
                </h1>
              </div>
              <div className="recording-readout">
                <RecordingTimer ms={recorder.elapsed} />
              </div>
            </div>

            <div className="host-meter-row">
              <AudioVisualizer
                level={recorder.paused ? 0 : recorder.level}
              />
              <LevelDots
                level={
                  recorder.recording && !recorder.paused ? recorder.level : 0
                }
              />
              <span className="muted">
                {recorder.paused
                  ? "Recording paused — resume whenever you are ready"
                  : recorder.level > 0.01
                    ? "Voice detected"
                    : recorder.recording
                      ? "Listening for speech…"
                      : "Microphone is off"}
              </span>
            </div>

            {audioSource === "upload" ? (
              <label className="upload-label">
                Choose WebM audio
                <input
                  type="file"
                  accept="audio/webm,.webm"
                  disabled={busy || ended}
                  onChange={(e) => void upload(e.target.files?.[0])}
                />
                <small>
                  Upload saves playback audio. This endpoint does not transcribe
                  files.
                </small>
              </label>
            ) : (
              <div className="listening-bar">
                {!recorder.recording ? (
                  <PrimaryActionButton
                    disabled={
                      busy ||
                      ended ||
                      saved ||
                      lecture.connection !== "Connected"
                    }
                    onClick={() => void recorder.start(audioSource)}
                  >
                    Start recording
                    <PillDot />
                  </PrimaryActionButton>
                ) : (
                  <PrimaryActionButton
                    disabled={busy || ended || saved}
                    onClick={() =>
                      recorder.paused ? recorder.resume() : recorder.pause()
                    }
                  >
                    <Icon
                      name={recorder.paused ? "play" : "pause"}
                      size={14}
                    />
                    {recorder.paused ? "Resume recording" : "Pause recording"}
                  </PrimaryActionButton>
                )}
                <button
                  type="button"
                  className="button secondary"
                  aria-label="Stop & save recording"
                  disabled={
                    busy ||
                    ended ||
                    (!recorder.recording &&
                      !hasPending &&
                      recorder.elapsed === 0) ||
                    saved
                  }
                  onClick={() => void save()}
                >
                  <Icon name="stop" size={13} />
                  {busy ? "Saving…" : saved ? "Saved" : "Stop & save"}
                </button>
              </div>
            )}

            <p className="asr-status" role="status">
              {saved
                ? "Recording saved. End the lecture when you are ready to generate the study pack."
                : recorder.paused
                  ? "Paused — audio chunks are not being sent while paused."
                  : lecture.asrStatus !== "Waiting for audio"
                    ? lecture.asrStatus
                    : lecture.lastCaption
                      ? "Live captions streaming to your students."
                      : recorder.recording
                        ? "Processing audio — awaiting first captions."
                        : "Audio permission is requested only when you start."}
            </p>
            {errors.map((message, i) => (
              <ErrorState key={i} message={message} />
            ))}
          </div>

          <section
            className="host-transcript-card"
            aria-label="Live speaker transcript"
          >
            <div className="section-heading">
              <h2>What students are seeing (live transcript)</h2>
              <div className="host-transcript-actions">
                <span className="muted num">
                  {lecture.lines.length} segments
                </span>
                {fullTranscript && <CopyButton text={fullTranscript} />}
              </div>
            </div>
            <div ref={transcriptBox} className="host-transcript-stream">
              {lecture.lines.length === 0 ? (
                <p className="muted">
                  Your live transcript will appear here continuously as you
                  speak, so you can verify everything students are reading.
                </p>
              ) : (
                lecture.lines.map((line, idx) => (
                  <div
                    key={line.offsetMs}
                    className={`host-transcript-line${idx === lecture.lines.length - 1 ? " latest" : ""}`}
                  >
                    <time className="num">{formatMmSs(line.offsetMs)}</time>
                    <span>{line.text}</span>
                  </div>
                ))
              )}
            </div>
          </section>
        </section>
        <aside className="host-controls">
          <section className="panel join-details">
            <h2>Invite your students</h2>
            <div className="room-code">
              <strong className="num">{created.code}</strong>
              <CopyButton text={created.code} />
            </div>
            <div className="join-link">
              <span>{link}</span>
              <CopyButton text={link} />
            </div>
            <JoinQr url={link} />
            <p className="muted">
              {lecture.session?.participantCount ?? 0} participants in this room
            </p>
          </section>
          <section className="panel source-panel">
            <h2>Speech language</h2>
            <LanguageSelector
              label="Spoken lecture language"
              caption="Deepgram Nova-3"
              value={source}
              onChange={(v) => {
                if (!locked) {
                  setSource(v);
                  storePreference("source", v);
                }
              }}
              disabled={locked || ended}
              includeAuto
            />
          </section>
          <section className="panel source-panel">
            <h2>Audio source</h2>
            <div className="source-options">
              {(
                [
                  ["mic", "mic", "Microphone", "In-person lectures"],
                  ["tab", "monitor", "Browser tab", "Share a tab with audio"],
                  ["upload", "upload", "Upload", "An existing WebM file"],
                ] as const
              ).map(([id, icon, label, description]) => (
                <button
                  type="button"
                  key={id}
                  className={audioSource === id ? "selected" : ""}
                  disabled={locked || ended}
                  onClick={() => setAudioSource(id)}
                  aria-pressed={audioSource === id}
                >
                  <span className="icon-chip">
                    <Icon name={icon} size={15} />
                  </span>
                  <span>
                    <strong>{label}</strong>
                    <small>{description}</small>
                  </span>
                </button>
              ))}
            </div>
          </section>
          <SecondaryActionButton
            disabled={busy || ended}
            onClick={() => setConfirm(true)}
          >
            {ended ? "Lecture ended" : "End lecture"}
          </SecondaryActionButton>
          {ended && (
            <Link
              className="button primary"
              href={`/session/${created.code}/pack`}
            >
              Open study pack
              <PillDot />
            </Link>
          )}
        </aside>
        <dialog
          ref={dialog}
          onCancel={() => setConfirm(false)}
          className="confirm-dialog"
        >
          <h2>End this lecture?</h2>
          <p>
            We’ll save your recording and request a study pack. Students will
            stop receiving new captions.
          </p>
          <div>
            <SecondaryActionButton onClick={() => setConfirm(false)}>
              Keep teaching
            </SecondaryActionButton>
            <PrimaryActionButton onClick={() => void end()}>
              End & save
            </PrimaryActionButton>
          </div>
        </dialog>
      </main>
    </>
  );
}
