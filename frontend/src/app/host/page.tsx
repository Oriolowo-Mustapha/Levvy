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
  LanguagePairSelector,
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
  uploadAudio,
} from "../../lib/client";
import type { CreateSessionResponse } from "../../lib/api";

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
  const [meetingUrl, setMeetingUrl] = useState("");
  const [meetStatus, setMeetStatus] = useState("");
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
    setMeetStatus("");
    try {
      const auth = readAuth();
      if (!auth) throw new Error("Sign in before hosting a lecture.");
      const trimmedUrl = meetingUrl.trim();
      const data = trimmedUrl
        ? await request<CreateSessionResponse>("/sessions/online-class", {
            method: "POST",
            body: JSON.stringify({
              title,
              meetingUrl: trimmedUrl,
              hostId: auth.user.id,
            }),
          })
        : await request<CreateSessionResponse>("/sessions", {
            method: "POST",
            body: JSON.stringify({ title, hostId: auth.user.id }),
          });
      setCreated(data);
      if (trimmedUrl) {
        setMeetStatus("Levvy note-taker bot dispatched to your online class.");
      }
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  async function connectMeet(e: FormEvent) {
    e.preventDefault();
    if (!created || !meetingUrl.trim()) return;
    setBusy(true);
    setError("");
    setMeetStatus("");
    try {
      await request(`/sessions/${created.sessionId}/connect-meet`, {
        method: "POST",
        body: JSON.stringify({ meetingUrl: meetingUrl.trim() }),
      });
      setMeetStatus("Levvy note-taker bot dispatched to your online class.");
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
  const latest = lecture.lines.at(-1)?.text;
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
            <LanguagePairSelector
              source={source}
              target={target}
              onSource={setSource}
              onTarget={setTarget}
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
            <label className="entry-box">
              <span className="entry-label">
                Google Meet / Online class link (optional)
              </span>
              <input
                type="url"
                value={meetingUrl}
                onChange={(e) => setMeetingUrl(e.target.value)}
                placeholder="https://meet.google.com/abc-defg-hij"
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
  return (
    <>
      <AppHeader active="host" />
      <main id="main" className="host-main">
        <section className="listening-panel" aria-labelledby="host-title">
          <div className="listening-head">
            <RecordingStatus
              state={
                ended
                  ? "Lecture complete"
                  : recorder.recording
                    ? "Listening…"
                    : recorder.state
              }
              recording={recorder.recording && recorder.level > 0.01}
            />
            <ConnectionIndicator state={lecture.connection} />
          </div>
          <h1 id="host-title" className="listening-title">
            {title}
          </h1>
          <p className="listening-text" aria-live="polite">
            {latest || "Your words will appear here as you speak."}
          </p>
          <AudioVisualizer level={recorder.level} />
          <LevelDots level={recorder.recording ? recorder.level : 0} />
          <div className="recording-readout">
            <RecordingTimer ms={recorder.elapsed} />
            <span>
              {recorder.level > 0.01
                ? "Audio input detected"
                : recorder.recording
                  ? "Waiting for audible input"
                  : "Microphone is off"}
            </span>
          </div>
          <LanguagePairSelector
            source={source}
            target={target}
            onSource={(v) => {
              if (!locked) setSource(v);
            }}
            sourceDisabled={locked || ended}
            onTarget={setTarget}
          />
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
              <Link
                href={`/session/${created.code}`}
                className="icon-button"
                aria-label="Open student view"
                title="Open student view"
              >
                <Icon name="headphones" size={16} />
              </Link>
              <PrimaryActionButton
                disabled={
                  busy ||
                  ended ||
                  saved ||
                  lecture.connection !== "Connected" ||
                  recorder.recording
                }
                onClick={() => void recorder.start(audioSource)}
              >
                {recorder.recording ? "Listening…" : "Start recording"}
                <PillDot />
              </PrimaryActionButton>
              <button
                type="button"
                className="icon-button"
                aria-label="Stop & save recording"
                title={busy ? "Saving…" : "Stop & save recording"}
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
                <Icon name="stop" size={14} />
              </button>
            </div>
          )}
          <p className="asr-status" role="status">
            {saved
              ? "Recording saved. You can end the lecture to generate a study pack."
              : lecture.asrStatus !== "Waiting for audio"
                ? lecture.asrStatus
                : lecture.lastCaption
                  ? "Captions received from transcription service."
                  : recorder.recording
                    ? "Processing audio — awaiting first captions."
                    : "Audio permission is requested only when you start."}
          </p>
          {errors.map((message, i) => (
            <ErrorState key={i} message={message} />
          ))}
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
          <section className="panel">
            <h2>Online class bot</h2>
            <form onSubmit={connectMeet}>
              <label className="entry-box">
                <span className="entry-label">Meeting URL</span>
                <input
                  type="url"
                  required
                  value={meetingUrl}
                  onChange={(e) => setMeetingUrl(e.target.value)}
                  placeholder="https://meet.google.com/abc-defg-hij"
                  disabled={busy || ended}
                />
              </label>
              <SecondaryActionButton disabled={busy || ended}>
                {busy ? "Dispatching…" : "Send Levvy bot"}
              </SecondaryActionButton>
            </form>
            {meetStatus && (
              <p className="muted" role="status">
                {meetStatus}
              </p>
            )}
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
