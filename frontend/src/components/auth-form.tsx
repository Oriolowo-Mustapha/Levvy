"use client";
import { useState, type FormEvent } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  AppHeader,
  AudioVisualizer,
  ErrorState,
  PillDot,
  PrimaryActionButton,
} from "./ui";
import { request, errorText, saveAuth } from "../lib/client";
import type { AuthResponseDto } from "../lib/api";

export function AuthForm({ register = false }: { register?: boolean }) {
  const router = useRouter();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState("");
  async function submit(e: FormEvent<HTMLFormElement>) {
    e.preventDefault();
    const values = Object.fromEntries(new FormData(e.currentTarget));
    setBusy(true);
    setError("");
    try {
      const data = await request<AuthResponseDto>(
        register ? "/auth/register" : "/auth/login",
        { method: "POST", body: JSON.stringify(values) },
      );
      saveAuth(data);
      router.push("/dashboard");
    } catch (e) {
      setError(errorText(e));
    } finally {
      setBusy(false);
    }
  }
  return (
    <>
      <AppHeader />
      <main id="main" className="auth-main">
        <section className="auth-intro">
          <AudioVisualizer hero />
          <h1>
            {register ? "Create Your" : "Welcome Back"}
            <span>{register ? "Workspace" : "to Levvy"}</span>
          </h1>
          <p>Your lectures, your language, your own pace.</p>
        </section>
        <section className="auth-card panel">
          <form onSubmit={submit}>
            {register && (
              <>
                <div className="form-row">
                  <label>
                    First name
                    <input
                      name="firstName"
                      autoComplete="given-name"
                      required
                    />
                  </label>
                  <label>
                    Last name
                    <input
                      name="lastName"
                      autoComplete="family-name"
                      required
                    />
                  </label>
                </div>
                <label>
                  I’m joining as
                  <select name="role">
                    <option value="student">Student</option>
                    <option value="instructor">Instructor</option>
                  </select>
                </label>
              </>
            )}
            <label>
              Email address
              <input name="email" type="email" autoComplete="email" required />
            </label>
            <label>
              Password
              <input
                name="password"
                type="password"
                autoComplete={register ? "new-password" : "current-password"}
                minLength={register ? 8 : 1}
                required
              />
            </label>
            {error && <ErrorState message={error} />}
            <PrimaryActionButton disabled={busy}>
              {busy
                ? "Just a moment…"
                : register
                  ? "Create account"
                  : "Sign in"}
              <PillDot />
            </PrimaryActionButton>
          </form>
          <p className="auth-switch">
            {register ? "Already have an account?" : "New to Levvy?"}{" "}
            <Link href={register ? "/login" : "/signup"}>
              {register ? "Sign in" : "Create an account"}
            </Link>
          </p>
          <GoogleSignIn
            onSuccess={(data) => {
              saveAuth(data);
              router.push("/dashboard");
            }}
          />
        </section>
      </main>
    </>
  );
}

function GoogleSignIn({
  onSuccess,
}: {
  onSuccess: (data: AuthResponseDto) => void;
}) {
  const clientId = process.env.NEXT_PUBLIC_GOOGLE_CLIENT_ID;
  const [error, setError] = useState("");
  const [busy, setBusy] = useState(false);
  async function start() {
    setBusy(true);
    setError("");
    try {
      if (!clientId)
        throw new Error(
          "Google sign-in is not configured. Please use email sign-in.",
        );
      const google = await new Promise<GoogleIdentity>((resolve, reject) => {
        if (window.google) return resolve(window.google);
        const script = document.createElement("script");
        script.src = "https://accounts.google.com/gsi/client";
        script.async = true;
        script.onload = () =>
          window.google
            ? resolve(window.google)
            : reject(new Error("Google sign-in unavailable."));
        script.onerror = () =>
          reject(new Error("Unable to load Google sign-in."));
        document.head.appendChild(script);
      });
      google.accounts.id.initialize({
        client_id: clientId,
        callback: async ({ credential }) => {
          try {
            onSuccess(
              await request<AuthResponseDto>("/auth/google", {
                method: "POST",
                body: JSON.stringify({ idToken: credential }),
              }),
            );
          } catch (e) {
            setError(errorText(e));
          } finally {
            setBusy(false);
          }
        },
      });
      const root = document.getElementById("google-sign-in");
      if (root) {
        root.replaceChildren();
        google.accounts.id.renderButton(root, {
          type: "standard",
          theme: "outline",
          size: "large",
        });
      }
      setBusy(false);
    } catch (e) {
      setError(errorText(e));
      setBusy(false);
    }
  }
  return (
    <div className="google-auth">
      {clientId && (
        <>
          <button
            className="button secondary"
            disabled={busy}
            onClick={() => void start()}
          >
            Continue with Google
          </button>
          <div id="google-sign-in" />
        </>
      )}
      {error && <ErrorState message={error} />}
    </div>
  );
}
interface GoogleIdentity {
  accounts: {
    id: {
      initialize: (options: {
        client_id: string;
        callback: (data: { credential: string }) => void;
      }) => void;
      renderButton: (
        element: HTMLElement,
        options: Record<string, string>,
      ) => void;
    };
  };
}
declare global {
  interface Window {
    google?: GoogleIdentity;
  }
}
