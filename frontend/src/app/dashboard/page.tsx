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
  errorText,
  readAuth,
  request,
  saveAuth,
  storePreference,
} from "../../lib/client";
import type { UserDashboardDto, UserProfileDto } from "../../lib/api";

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
  const [busy, setBusy] = useState(false);
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
  const sessions = data
    ? [
        ...new Map(
          [...data.hostedSessions, ...data.attendedSessions].map((s) => [
            s.id,
            s,
          ]),
        ).values(),
      ]
        .sort((a, b) => Date.parse(b.startedAtUtc) - Date.parse(a.startedAtUtc))
        .filter(
          (s) =>
            (filter === "all" || (filter === "live" ? s.isLive : !s.isLive)) &&
            `${s.title} ${s.code}`.toLowerCase().includes(query.toLowerCase()),
        )
    : [];
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
            <p>A little more clarity. A little further every day.</p>
          </div>
          <Link href="/" className="button primary">
            Join a lecture
            <PillDot />
          </Link>
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
                <section>
                  <div className="section-heading">
                    <h2>Your lectures</h2>
                    <span>{sessions.length} in your workspace</span>
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
                  <div className="session-grid">
                    {sessions.map((s) => (
                      <SessionCard
                        key={s.id}
                        title={s.title}
                        code={s.code}
                        live={s.isLive}
                      >
                        {s.hasStudyPack && (
                          <Link href={`/session/${s.code}/pack`}>
                            Study pack
                          </Link>
                        )}
                      </SessionCard>
                    ))}
                  </div>
                  {!sessions.length && (
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
                <h2>Your preferences.</h2>
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
                <button
                  className="text-button"
                  onClick={() => {
                    sessionStorage.removeItem("levvy.auth");
                    router.push("/");
                  }}
                >
                  Sign out
                </button>
              </aside>
            </div>
          </>
        )}
      </main>
    </>
  );
}
