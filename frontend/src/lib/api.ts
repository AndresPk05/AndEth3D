export const API_URL = (import.meta.env.PUBLIC_API_URL ?? (import.meta.env.DEV ? "http://localhost:5000" : "")).replace(/\/$/, "");

export function escapeHtml(value: unknown): string {
  return String(value ?? "").replace(/[&<>"']/g, c => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]!));
}

export function safeNext(value: string | null, fallback: string): string {
  if (!value || !value.startsWith("/") || value.startsWith("//") || /[\\\x00-\x20]/.test(value)) return fallback;
  const url = new URL(value, window.location.origin);
  return url.origin === window.location.origin ? url.pathname + url.search + url.hash : fallback;
}

export interface UserDto {
  id: number;
  name: string;
  email: string;
  role: string;
}

export interface Session {
  token: string;
  user: UserDto;
}

const KEY = "andeth3d_session";

export function getSession(): Session | null {
  try {
    localStorage.removeItem(KEY); // Retire persistent sessions from older versions.
    const session = JSON.parse(sessionStorage.getItem(KEY) || "null");
    if (!session?.token || !session.user) return null;
    const payload = JSON.parse(atob(session.token.split(".")[1].replace(/-/g, "+").replace(/_/g, "/")));
    if (!payload.exp || payload.exp * 1000 <= Date.now()) { clearSession(); return null; }
    return session;
  } catch {
    return null;
  }
}

export function saveSession(session: Session) {
  sessionStorage.setItem(KEY, JSON.stringify(session));
}

export function clearSession() {
  localStorage.removeItem(KEY);
  sessionStorage.removeItem(KEY);
}

export async function apiFetch<T = unknown>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const session = getSession();
  const headers: Record<string, string> = {
    ...(options.headers as Record<string, string>),
  };
  if (session?.token) headers["Authorization"] = `Bearer ${session.token}`;
  if (options.body && !(options.body instanceof FormData))
    headers["Content-Type"] = "application/json";

  const res = await fetch(`${API_URL}${path}`, { ...options, headers });
  if (res.status === 401 && session) {
    clearSession();
    window.location.assign(`/login?next=${encodeURIComponent(window.location.pathname)}`);
  }
  if (res.status === 204) return null as T;

  const data = await res.json().catch(() => null);
  if (!res.ok) throw new Error(data?.message || (data?.errors ? Object.values(data.errors).flat().join(" ") : `Error ${res.status}`));
  return data as T;
}

export function fileUrl(path?: string | null): string {
  if (!path) return "";
  return /^\/uploads\/[a-f0-9]{32}\.(png|jpg|jpeg|webp|gif)$/.test(path) ? `${API_URL}${path}` : "";
}

export function requireAuth(role?: "Admin" | "Client"): Session {
  const session = getSession();
  if (!session) {
    window.location.href = "/login";
    throw new Error("No autenticado");
  }
  if (role && session.user.role !== role) {
    window.location.href = "/";
    throw new Error("Sin permisos");
  }
  return session;
}

export async function downloadQuote(id: number) {
  const response = await fetch(`${API_URL}/api/quotes/${id}/file`, {
    headers: { Authorization: `Bearer ${getSession()?.token ?? ""}` }
  });
  if (response.status === 401) { clearSession(); window.location.assign("/login"); }
  if (!response.ok) throw new Error("No fue posible descargar el archivo.");
  const url = URL.createObjectURL(await response.blob());
  const link = document.createElement("a");
  link.href = url;
  link.download = response.headers.get("Content-Disposition")?.match(/filename="?([^";]+)"?/)?.[1] ?? `referencia-${id}`;
  link.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
