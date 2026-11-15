"use client";
import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState, type FormEvent } from "react";
import {
  AppHeader,
  BrowserReady,
  EmptyState,
  ErrorState,
  Icon,
  LanguageSelector,
  LoadingState,
  PillDot,
  PrimaryActionButton,
} from "../../components/ui";
import { SessionCard, StudyPackCard } from "../../components/lecture";
import {
  clearAuth,
  errorText,
  readAuth,
  request,
  saveAuth,
  storePreference,
} from "../../lib/client";
import type {
  CreateSessionResponse,
  UserDashboardDto,
  UserProfileDto,
} from "../../lib/api";

export default function DashboardPage() {
  return (
    <BrowserReady>
      <DashboardContent />
    </BrowserReady>
  );
}
function DashboardContent() {
  const router = useRouter();
  const [data, setData] = useState<UserDashboardDto | null>(null);
  const [loading, setLoading] = useState(() => !!readAuth());
  const [error, setError] = useState("");
  const signedIn = !!readAuth();
  const [query, setQuery] = useState("");
  const [filter, setFilter] = useState("all");
  const [saved, setSaved] = useState(false);
  const [endingId, setEndingId] = useState<string | null>(null);
  const [endError, setEndError] = useState("");
  const [busy, setBusy] = useState(false);
  const [botTitle, setBotTitle] = useState("");
  const [botUrl, setBotUrl] = useState("");
  const [botBusy, setBotBusy] = useState(false);
  const [botError, setBotError] = useState("");
  const [botRoom, setBotRoom] = useState<CreateSessionResponse | null>(null);
  async function load() {
    try {
      const result = await request<UserDashboardDto>("/users/me/dashboard");
      setData(result);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setLoading(false);
    }
  }
  useEffect(() => {
    if (!signedIn) return;
    const controller = new AbortController();
    request<UserDashboardDto>("/users/me/dashboard", {
      signal: controller.signal,
    })
      .then(setData)
      .catch((e) => {
        if (!controller.signal.aborted) setError(errorText(e));
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false);
      });
    return () => controller.abort();
  }, [signedIn]);
  async function update(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!data) return;
    setBusy(true);
    setError("");
    setSaved(false);
    const payload = {
      ...data.profile,
      ...Object.fromEntries(new FormData(e.currentTarget)),
    };
    try {
      const profile = await request<UserProfileDto>("/users/me/profile", {
        method: "PUT",
        body: JSON.stringify(payload),
      });
      const auth = readAuth();
      if (auth) saveAuth({ ...auth, user: profile });
      setData({ ...data, profile });
      storePreference("language", profile.preferredLanguage);
      setSaved(true);
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  async function dispatchBot(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    if (!data || !botTitle.trim() || !botUrl.trim()) return;
    setBotBusy(true);
    setBotError("");
    setBotRoom(null);
    try {
      const created = await request<CreateSessionResponse>(
        "/sessions/online-class",
        {
          method: "POST",
          body: JSON.stringify({
            title: botTitle.trim(),
            meetingUrl: botUrl.trim(),
            hostId: data.profile.id,
          }),
        },
      );
      setBotRoom(created);
      setBotTitle("");
      setBotUrl("");
      void load();
    } catch (e) {
      setBotError(errorText(e));
    } finally {
      setBotBusy(false);
    }
  }
  async function endHostedSession(sessionId: string) {
    setEndingId(sessionId);
    setEndError("");
    try {
      await request(`/sessions/${sessionId}/end`, { method: "POST" });
      await load();
    } catch (e) {
      setEndError(errorText(e));
    } finally {
      setEndingId(null);
    }
  }
  const matchesFilterAndQuery = (s: UserDashboardDto["hostedSessions"][number]) =>
    (filter === "all" || (filter === "live" ? s.isLive : !s.isLive)) &&
    `${s.title} ${s.code}`.toLowerCase().includes(query.toLowerCase());
  const hostedSessions = data
    ? [...data.hostedSessions]
        .sort((a, b) => Date.parse(b.startedAtUtc) - Date.parse(a.startedAtUtc))
        .filter(matchesFilterAndQuery)
    : [];
  const hostedIds = new Set((data?.hostedSessions ?? []).map((s) => s.id));
  const attendedSessions = data
    ? [...data.attendedSessions]
        .filter((s) => !hostedIds.has(s.id))
        .sort((a, b) => Date.parse(b.startedAtUtc) - Date.parse(a.startedAtUtc))
        .filter(matchesFilterAndQuery)
    : [];
  const totalVisible = hostedSessions.length + attendedSessions.length;
  return (
    <>
      <AppHeader active="dashboard" />
      <main id="main" className="dashboard-main">
        <div className="page-heading">
          <div>
            <h1>
              {data
                ? `Hello, ${data.profile.firstName}.`
                : "Your learning, all together."}
            </h1>
            <p>
              {data && (data.profile.department || data.profile.institution)
                ? [data.profile.department, data.profile.institution]
                    .filter(Boolean)
                    .join(" · ")
                : "A little more clarity. A little further every day."}
            </p>
          </div>
          <div className="pill-switch">
            <Link href="/host" className="button secondary">
              Host a lecture
              <Icon name="mic" size={15} />
            </Link>
            <Link href="/" className="button primary">
              Join a lecture
              <PillDot />
            </Link>
          </div>
        </div>
        {loading && <LoadingState />}
        {error && <ErrorState message={error} retry={() => void load()} />}
        {!loading && !signedIn && (
          <section className="panel signed-out">
            <EmptyState title="Make room for your learning">
              Sign in to see attended lectures, recordings and study packs. You
              can always join a lecture without an account.
            </EmptyState>
            <Link className="button primary" href="/login">
              Sign in to your workspace
              <Icon name="arrow" size={18} />
            </Link>
            <Link className="subtle-link" href="/signup">
              Create an account
            </Link>
          </section>
        )}
        {data && (
          <>
            <section className="stat-grid" aria-label="Learning overview">
              {[
                [data.totalSessionsAttended, "Lectures attended"],
                [data.totalSessionsHosted, "Lectures hosted"],
                [data.totalQuestionsAsked, "Questions explored"],
                [data.totalStudyPacksAvailable, "Study packs ready"],
              ].map(([n, label]) => (
                <div key={label}>
                  <strong className="num">{n}</strong>
                  <span>{label}</span>
                </div>
              ))}
            </section>
            <div className="dashboard-grid">
              <div>
                <section className="panel" style={{ marginBottom: 24 }}>
                  <div className="section-heading">
                    <h2>Send Levvy to a meeting</h2>
                    <span>Zoom · Google Meet · Teams</span>
                  </div>
                  <p className="muted" style={{ marginBottom: 14 }}>
                    Got a meeting link from your lecturer? Paste it below so
                    Levvy joins to record, transcribe, translate, and build your
                    study pack.
                  </p>
                  <form onSubmit={dispatchBot}>
                    <div className="form-row">
                      <label>
                        Lecture or class title
                        <input
                          required
                          maxLength={200}
                          value={botTitle}
                          onChange={(e) => setBotTitle(e.target.value)}
                          placeholder="e.g. CSC 301 Algorithms"
                        />
                      </label>
                      <label>
                        Meeting URL
                        <input
                          type="url"
                          required
                          value={botUrl}
                          onChange={(e) => setBotUrl(e.target.value)}
                          placeholder="https://zoom.us/j/... or meet.google.com/..."
                        />
                      </label>
                    </div>
                    {botError && <ErrorState message={botError} />}
                    <div className="pill-switch">
                      <PrimaryActionButton disabled={botBusy}>
                        {botBusy ? "Dispatching bot…" : "Send Levvy bot"}
                        <PillDot />
                      </PrimaryActionButton>
                    </div>
                  </form>
                  {botRoom && (
                    <div className="notice" role="status">
                      Bot dispatched! Room code:{" "}
                      <strong className="num">{botRoom.code}</strong> ·{" "}
                      <Link href={`/session/${botRoom.code}`}>
                        Open live captions →
                      </Link>
                    </div>
                  )}
                </section>
                <section>
                  <div className="section-heading">
                    <h2>Your lectures</h2>
                    <span>{totalVisible} in your workspace</span>
                  </div>
                  <div className="search-row">
                    <label className="search-input">
                      <Icon name="search" size={18} />
                      <input
                        aria-label="Search lectures"
                        placeholder="Find a lecture or room code"
                        value={query}
                        onChange={(e) => setQuery(e.target.value)}
                      />
                    </label>
                    <select
                      aria-label="Filter lectures"
                      value={filter}
                      onChange={(e) => setFilter(e.target.value)}
                    >
                      <option value="all">All lectures</option>
                      <option value="live">In progress</option>
                      <option value="ended">Past lectures</option>
                    </select>
                  </div>
                  {endError && <ErrorState message={endError} />}
                  {hostedSessions.length > 0 && (
                    <div style={{ marginBottom: 24 }}>
                      <div className="section-heading">
                        <h3>Hosted by you</h3>
                        <span>{hostedSessions.length} lecturer rooms</span>
                      </div>
                      <div className="session-grid">
                        {hostedSessions.map((s) => (
                          <SessionCard
                            key={s.id}
                            title={s.title}
                            code={s.code}
                            live={s.isLive}
                            hosted
                            participantCount={s.participantCount}
                            hasRecording={s.hasRecording}
                            ending={endingId === s.id}
                            onEnd={() => void endHostedSession(s.id)}
                          />
                        ))}
                      </div>
                    </div>
                  )}
                  {attendedSessions.length > 0 && (
                    <div>
                      {hostedSessions.length > 0 && (
                        <div className="section-heading">
                          <h3>Attended lectures</h3>
                          <span>{attendedSessions.length} joined</span>
                        </div>
                      )}
                      <div className="session-grid">
                        {attendedSessions.map((s) => (
                          <SessionCard
                            key={s.id}
                            title={s.title}
                            code={s.code}
                            live={s.isLive}
                            participantCount={s.participantCount}
                            hasRecording={s.hasRecording}
                          >
                            {s.hasStudyPack && (
                              <Link href={`/session/${s.code}/pack`}>
                                Study pack
                              </Link>
                            )}
                          </SessionCard>
                        ))}
                      </div>
                    </div>
                  )}
                  {totalVisible === 0 && (
                    <EmptyState title="Your next lecture is waiting">
                      Join a room or host a lecture to start your history.
                    </EmptyState>
                  )}
                </section>
                <section className="study-section">
                  <div className="section-heading">
                    <h2>Study packs</h2>
                    <span>{data.studyPacks.length} ready</span>
                  </div>
                  <div className="session-grid">
                    {data.studyPacks.map((pack) => (
                      <StudyPackCard
                        key={pack.studyPackId}
                        title={pack.sessionTitle}
                        code={pack.sessionCode}
                      >
                        <p>
                          {pack.flashcardCount} flashcards · Source-linked notes
                        </p>
                      </StudyPackCard>
                    ))}
                  </div>
                  {!data.studyPacks.length && (
                    <EmptyState title="Your study shelf starts here">
                      Study packs appear once your lecturer ends a session and
                      generation finishes.
                    </EmptyState>
                  )}
                </section>
              </div>
              <aside className="panel profile-panel">
                <div className="section-heading">
                  <h2>Account</h2>
                  <button
                    type="button"
                    className="text-button"
                    onClick={() => {
                      clearAuth();
                      setData(null);
                      router.push("/");
                      router.refresh();
                    }}
                  >
                    Sign out
                  </button>
                </div>
                <p className="muted" style={{ marginBottom: 16 }}>
                  {data.profile.email} ·{" "}
                  {data.profile.role === "instructor"
                    ? "Instructor"
                    : "Student"}{" "}
                  ·{" "}
                  {data.profile.authProvider === "google" ? "Google" : "Email"}
                </p>
                <h3>Preferences</h3>
                <form onSubmit={update}>
                  <div className="form-row">
                    <label>
                      First name
                      <input
                        required
                        name="firstName"
                        defaultValue={data.profile.firstName}
                      />
                    </label>
                    <label>
                      Last name
                      <input
                        required
                        name="lastName"
                        defaultValue={data.profile.lastName}
                      />
                    </label>
                  </div>
                  <label>
                    Institution
                    <input
                      name="institution"
                      defaultValue={data.profile.institution || ""}
                    />
                  </label>
                  <label>
                    Department
                    <input
                      name="department"
                      defaultValue={data.profile.department || ""}
                    />
                  </label>
                  <LanguageSelector
                    value={data.profile.preferredLanguage}
                    onChange={(v) =>
                      setData({
                        ...data,
                        profile: { ...data.profile, preferredLanguage: v },
                      })
                    }
                  />
                  <input
                    type="hidden"
                    name="preferredLanguage"
                    value={data.profile.preferredLanguage}
                  />
                  <label>
                    Caption size
                    <select
                      name="captionFontSize"
                      defaultValue={data.profile.captionFontSize}
                    >
                      <option value="sm">Comfortable</option>
                      <option value="md">Large</option>
                      <option value="lg">Extra large</option>
                      <option value="xl">Largest</option>
                    </select>
                  </label>
                  <label>
                    Accessibility preference
                    <select
                      name="accessibilityMode"
                      defaultValue={data.profile.accessibilityMode}
                    >
                      <option value="standard">Standard</option>
                      <option value="deaf_hoh">Deaf / hard of hearing</option>
                      <option value="international">
                        International student
                      </option>
                    </select>
                  </label>
                  <PrimaryActionButton disabled={busy}>
                    {busy ? "Saving…" : "Save preferences"}
                  </PrimaryActionButton>
                  <p role="status">
                    {saved ? "Your preferences are saved." : ""}
                  </p>
                </form>
              </aside>
            </div>
          </>
        )}
      </main>
    </>
  );
}
