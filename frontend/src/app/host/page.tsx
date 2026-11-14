"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
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
  PrimaryActionButton,
  RecordingStatus,
  RecordingTimer,
  SecondaryActionButton,
} from "../../components/ui";
import { OriginalTranscriptCard } from "../../components/lecture";
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
  const router = useRouter();
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
  return (
    <>
      <AppHeader active="host" />
      <main id="main" className="host-main">
        <div className="page-heading">
          <div>
            <span className="eyebrow">LEAD THE CONVERSATION</span>
            <h1>A lecture everyone can follow.</h1>
            <p>Your voice. Their language. A shared understanding.</p>
          </div>
          <Link className="subtle-link" href="/dashboard">
            <Icon name="history" size={16} />
            My lectures
          </Link>
        </div>
        {!signedIn && (
          <div className="notice">
            Hosting requires an account. <Link href="/login">Sign in</Link> or{" "}
            <Link href="/signup">create an account</Link> to begin.
          </div>
        )}
        {!created ? (
          <section className="create-session panel">
            <span className="small-icon">
              <Icon name="mic" />
            </span>
            <h2>Give your lecture a home.</h2>
            <p>
              Start with a title. We’ll create a room your students can join.
            </p>
            <form onSubmit={create}>
              <label>
                Lecture title
                <input
                  required
                  maxLength={200}
                  value={title}
                  onChange={(e) => setTitle(e.target.value)}
                  placeholder="e.g. Introduction to microeconomics"
                />
              </label>
              <LanguagePairSelector
                source={source}
                target={target}
                onSource={setSource}
                onTarget={setTarget}
              />
              <PrimaryActionButton disabled={busy || !signedIn}>
                {busy ? "Creating room…" : "Create lecture room"}
                <Icon name="arrow" size={18} />
              </PrimaryActionButton>
            </form>
          </section>
        ) : (
          <div className="host-grid">
            <section className="listening-panel">
              <div className="listening-header">
                <Link
                  href={`/session/${created.code}`}
                  className="icon-button"
                  aria-label="Open student view"
                >
                  <Icon name="back" />
                </Link>
                <span>{title}</span>
                <ConnectionIndicator state={lecture.connection} />
              </div>
              <div className="listening-text">
                <span className="eyebrow">
                  {ended
                    ? "LECTURE COMPLETE"
                    : lecture.lastCaption
                      ? "LATEST CAPTION"
                      : "LISTENING SPACE"}
                </span>
                <h2>
                  {lecture.lines.at(-1)?.text ||
                    "Good ideas deserve to be heard."}
                </h2>
              </div>
              <AudioVisualizer level={recorder.level} />
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
              <div className="recording-toolbar">
                <Icon name="mic" />
                <RecordingStatus
                  state={ended ? "Session ended" : recorder.state}
                  recording={recorder.recording && recorder.level > 0.01}
                />
                <Icon name="headphones" />
              </div>
              <div className="asr-status" role="status">
                {lecture.asrStatus !== "Waiting for audio"
                  ? lecture.asrStatus
                  : lecture.lastCaption
                    ? "Captions received from transcription service."
                    : recorder.recording
                      ? "Processing audio — awaiting first captions."
                      : "Transcription starts when audio reaches the server."}
              </div>
            </section>
            <aside className="host-controls">
              <section className="panel join-details">
                <span className="eyebrow">INVITE YOUR STUDENTS</span>
                <h2 className="room-code">
                  {created.code}
                  <CopyButton text={created.code} />
                </h2>
                <p>Share this room code, or copy the joining link.</p>
                <div className="join-link">
                  <span>{link}</span>
                  <CopyButton text={link} />
                </div>
                <JoinQr url={link} />
                <p>
                  {lecture.session?.participantCount ?? 0} participants in this
                  room
                </p>
              </section>
              <section className="panel source-panel">
                <h2>Where’s your audio?</h2>
                <p>Choose what your students will follow.</p>
                <div className="source-options">
                  {(
                    [
                      ["mic", "mic", "Microphone", "For in-person lectures"],
                      [
                        "tab",
                        "monitor",
                        "Browser tab",
                        "Share a tab with audio",
                      ],
                      [
                        "upload",
                        "upload",
                        "Upload recording",
                        "Save an existing WebM lecture",
                      ],
                    ] as const
                  ).map(([id, icon, label, description]) => (
                    <button
                      key={id}
                      className={audioSource === id ? "selected" : ""}
                      disabled={locked || ended}
                      onClick={() => setAudioSource(id)}
                      aria-pressed={audioSource === id}
                    >
                      <Icon name={icon} />
                      <span>
                        <strong>{label}</strong>
                        <small>{description}</small>
                      </span>
                      <span className="radio-mark" />
                    </button>
                  ))}
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
                      Upload saves playback audio. This endpoint does not
                      transcribe files.
                    </small>
                  </label>
                ) : (
                  <>
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
                      <Icon name="mic" size={18} />
                      {recorder.recording
                        ? "Recording in progress"
                        : "Start recording"}
                    </PrimaryActionButton>
                    <SecondaryActionButton
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
                      {busy ? "Saving…" : "Stop & save recording"}
                    </SecondaryActionButton>
                  </>
                )}
                <p role="status">
                  {saved
                    ? "Recording saved. You can end the lecture to generate a study pack."
                    : "Audio permission is requested only when you start."}
                </p>
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
                  <Icon name="arrow" size={18} />
                </Link>
              )}
            </aside>
          </div>
        )}
        {[error, recorder.error, lecture.error]
          .filter(Boolean)
          .map((message, i) => (
            <ErrorState key={i} message={message} />
          ))}
        {created && lecture.lines.length > 0 && (
          <section className="host-caption">
            <OriginalTranscriptCard
              line={lecture.lines.at(-1)}
              language={source}
              onPlay={() => {
                router.push(`/session/${created.code}#transcript`);
              }}
            />
          </section>
        )}
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
