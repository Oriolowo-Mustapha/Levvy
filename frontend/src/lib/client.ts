import { API_BASE, type AuthResponseDto } from "./api";

const AUTH_STORAGE_KEY = "levvy.auth";
const SEVEN_DAYS_MS = 7 * 24 * 60 * 60 * 1000;

export function clearAuth() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(AUTH_STORAGE_KEY);
  sessionStorage.removeItem(AUTH_STORAGE_KEY);
}
export function readAuth(): AuthResponseDto | null {
  if (typeof window === "undefined") return null;
  try {
    const raw =
      localStorage.getItem(AUTH_STORAGE_KEY) ||
      sessionStorage.getItem(AUTH_STORAGE_KEY);
    const data = JSON.parse(raw || "null");
    if (data && Date.parse(data.expiresAtUtc) > Date.now()) {
      return data;
    }
    clearAuth();
    return null;
  } catch {
    return null;
  }
}
export function saveAuth(data: AuthResponseDto, remember?: boolean) {
  if (typeof window === "undefined") return;
  const keepSignedIn =
    remember ??
    (localStorage.getItem(AUTH_STORAGE_KEY) !== null ||
      sessionStorage.getItem(AUTH_STORAGE_KEY) === null);
  const maxExpiry = Date.now() + SEVEN_DAYS_MS;
  const serverExpiry = Date.parse(data.expiresAtUtc);
  const capped =
    Number.isFinite(serverExpiry) && serverExpiry > 0
      ? Math.min(serverExpiry, maxExpiry)
      : maxExpiry;
  const payload = JSON.stringify({
    ...data,
    expiresAtUtc: new Date(capped).toISOString(),
  });
  if (keepSignedIn) {
    localStorage.setItem(AUTH_STORAGE_KEY, payload);
    sessionStorage.removeItem(AUTH_STORAGE_KEY);
  } else {
    sessionStorage.setItem(AUTH_STORAGE_KEY, payload);
    localStorage.removeItem(AUTH_STORAGE_KEY);
  }
}
export function preference(key: string, fallback: string) {
  if (typeof window === "undefined") return fallback;
  return localStorage.getItem(`levvy.${key}`) || fallback;
}
export function storePreference(key: string, value: string) {
  localStorage.setItem(`levvy.${key}`, value);
}
export async function request<T>(
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  const auth = readAuth();
  if (auth) headers.set("Authorization", `Bearer ${auth.accessToken}`);
  if (init.body && typeof init.body === "string")
    headers.set("Content-Type", "application/json");
  let response: Response;
  try {
    response = await fetch(`${API_BASE}${path}`, {
      ...init,
      headers,
      cache: "no-store",
    });
  } catch (error) {
    if (error instanceof DOMException && error.name === "AbortError")
      throw error;
    throw new Error(
      "Cannot reach Levvy. Check your connection and the API configuration.",
    );
  }
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(
      body?.error ||
        (response.status === 401
          ? "Please sign in to continue."
          : response.status === 404
            ? "This lecture or resource is not available yet."
            : `Request failed (${response.status}). Please try again.`),
    );
  }
  return response.status === 204 ? (undefined as T) : response.json();
}
export const roomPath = (code: string) =>
  `/sessions/${encodeURIComponent(code.trim().toUpperCase())}`;
export async function uploadAudio(id: string, blob: Blob) {
  return request(`/sessions/${id}/audio`, {
    method: "POST",
    headers: { "Content-Type": blob.type || "audio/webm" },
    body: blob,
  });
}
export async function uploadAndTranscribeAudio(
  id: string,
  file: File,
  sourceLanguage = "auto",
) {
  const form = new FormData();
  form.append("file", file, file.name);
  const query = new URLSearchParams({ sourceLang: sourceLanguage || "auto" });
  return request<{
    sessionId: string;
    recordingKey: string;
    segmentCount: number;
    segments: Array<{
      offsetMs: number;
      text: string;
      sourceLanguage?: string;
      isFinal: boolean;
    }>;
  }>(`/sessions/${id}/audio/transcribe?${query.toString()}`, {
    method: "POST",
    body: form,
  });
}
export async function speakSessionText(
  code: string,
  text: string,
  language?: string,
): Promise<HTMLAudioElement> {
  const headers = new Headers({ "Content-Type": "application/json" });
  const auth = readAuth();
  if (auth) headers.set("Authorization", `Bearer ${auth.accessToken}`);
  const response = await fetch(`${API_BASE}${roomPath(code)}/speak`, {
    method: "POST",
    headers,
    body: JSON.stringify({ text, language }),
  });
  if (!response.ok) {
    const body = await response.json().catch(() => null);
    throw new Error(body?.error || `Speech synthesis failed (${response.status}).`);
  }
  const blob = await response.blob();
  const url = URL.createObjectURL(blob);
  const audio = new Audio(url);
  audio.addEventListener("ended", () => URL.revokeObjectURL(url), { once: true });
  await audio.play();
  return audio;
}
export function errorText(error: unknown) {
  return error instanceof Error
    ? error.message
    : "Something went wrong. Please try again.";
}
