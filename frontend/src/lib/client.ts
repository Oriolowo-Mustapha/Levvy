import { API_BASE, type AuthResponseDto } from "./api";

export function readAuth(): AuthResponseDto | null {
  if (typeof window === "undefined") return null;
  try {
    const data = JSON.parse(sessionStorage.getItem("levvy.auth") || "null");
    return data && Date.parse(data.expiresAtUtc) > Date.now() ? data : null;
  } catch {
    return null;
  }
}
export function saveAuth(data: AuthResponseDto) {
  sessionStorage.setItem("levvy.auth", JSON.stringify(data));
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
