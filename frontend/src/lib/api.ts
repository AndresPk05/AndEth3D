export const API_URL = "http://localhost:5000";

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
    return JSON.parse(localStorage.getItem(KEY) || "null");
  } catch {
    return null;
  }
}

export function saveSession(session: Session) {
  localStorage.setItem(KEY, JSON.stringify(session));
}

export function clearSession() {
  localStorage.removeItem(KEY);
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
  if (res.status === 204) return null as T;

  const data = await res.json().catch(() => null);
  if (!res.ok) throw new Error(data?.message || `Error ${res.status}`);
  return data as T;
}

export function fileUrl(path?: string | null): string {
  if (!path) return "";
  return path.startsWith("http") ? path : `${API_URL}${path}`;
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
