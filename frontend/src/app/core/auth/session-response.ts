export interface SessionResponse {
  username: string;
  expiresIn: number;
  permissions: readonly string[];
}
