import { createHmac, timingSafeEqual } from "crypto";
import { OAuth2Client } from "google-auth-library";
import users from "./users.json";

interface ScalewayEvent {
  path: string;
  httpMethod: string;
  queryStringParameters?: Record<string, string>;
  headers?: Record<string, string>;
}

interface ScalewayResponse {
  statusCode: number;
  headers?: Record<string, string>;
  body?: string;
}

const GOOGLE_CLIENT_ID = process.env.GOOGLE_CLIENT_ID!;
const GOOGLE_CLIENT_SECRET = process.env.GOOGLE_CLIENT_SECRET!;
const HMAC_SECRET = process.env.HMAC_SECRET!;
const FUNCTION_URL = process.env.FUNCTION_URL!;
const TRIPLETEX_CONSUMER_TOKEN = process.env.TRIPLETEX_CONSUMER_TOKEN!;
const TRIPLETEX_EMPLOYEE_TOKEN = process.env.TRIPLETEX_EMPLOYEE_TOKEN!;
const ALLOWED_DOMAIN = process.env.ALLOWED_DOMAIN || "fink.no";
const TOKEN_EXPIRY_SECONDS = 60 * 60 * 24 * 365;

const userMap = users as Record<string, { employeeId: number; name: string }>;

function redirect(url: string): ScalewayResponse {
  return { statusCode: 302, headers: { Location: url } };
}

function json(statusCode: number, body: object): ScalewayResponse {
  return {
    statusCode,
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  };
}

function errorPage(statusCode: number, message: string): ScalewayResponse {
  return {
    statusCode,
    headers: { "Content-Type": "text/html" },
    body: `<!DOCTYPE html><html><body style="font-family:sans-serif;padding:2em">
      <h2>Authentication Failed</h2><p>${message}</p>
      <p>You can close this window.</p></body></html>`,
  };
}

function signPayload(payload: object): string {
  const data = JSON.stringify(payload);
  const signature = createHmac("sha256", HMAC_SECRET)
    .update(data)
    .digest("hex");
  const encoded = Buffer.from(data).toString("base64url");
  return `${encoded}.${signature}`;
}

function getOAuthClient(callbackUrl: string): OAuth2Client {
  return new OAuth2Client(GOOGLE_CLIENT_ID, GOOGLE_CLIENT_SECRET, callbackUrl);
}

async function handleAuth(
  params: Record<string, string>
): Promise<ScalewayResponse> {
  const { port, state } = params;
  if (!port || !state) {
    return json(400, { error: "Missing port or state parameter" });
  }

  const callbackUrl = `${FUNCTION_URL}/callback`;
  const client = getOAuthClient(callbackUrl);

  const authUrl = client.generateAuthUrl({
    access_type: "offline",
    scope: ["openid", "email", "profile"],
    state: JSON.stringify({ port, state }),
    hd: ALLOWED_DOMAIN,
    prompt: "select_account",
  });

  return redirect(authUrl);
}

async function handleCallback(
  params: Record<string, string>
): Promise<ScalewayResponse> {
  const { code, state: stateJson, error } = params;

  if (error) {
    return errorPage(400, `Google OAuth error: ${error}`);
  }
  if (!code || !stateJson) {
    return errorPage(400, "Missing code or state");
  }

  let parsed: { port: string; state: string };
  try {
    parsed = JSON.parse(stateJson);
  } catch {
    return errorPage(400, "Invalid state parameter");
  }

  const callbackUrl = `${FUNCTION_URL}/callback`;
  const client = getOAuthClient(callbackUrl);

  let tokens;
  try {
    const res = await client.getToken(code);
    tokens = res.tokens;
  } catch (err: any) {
    return errorPage(500, `Token exchange failed: ${err.message}`);
  }

  if (!tokens.id_token) {
    return errorPage(500, "No ID token received");
  }

  const ticket = await client.verifyIdToken({
    idToken: tokens.id_token,
    audience: GOOGLE_CLIENT_ID,
  });

  const googlePayload = ticket.getPayload();
  if (!googlePayload) {
    return errorPage(500, "Could not verify ID token");
  }

  const email = googlePayload.email?.toLowerCase();
  const domain = email?.split("@")[1];

  if (!email || domain !== ALLOWED_DOMAIN) {
    return errorPage(
      403,
      `Access denied. Only @${ALLOWED_DOMAIN} accounts are allowed.`
    );
  }

  const user = userMap[email];
  if (!user) {
    return errorPage(
      403,
      `Your account (${email}) is not registered. Contact your administrator.`
    );
  }

  const payload = {
    consumerToken: TRIPLETEX_CONSUMER_TOKEN,
    employeeToken: TRIPLETEX_EMPLOYEE_TOKEN,
    employeeId: user.employeeId,
    employeeName: user.name,
    email,
    exp: Math.floor(Date.now() / 1000) + TOKEN_EXPIRY_SECONDS,
  };

  const signed = signPayload(payload);
  const localCallback = `http://localhost:${parsed.port}/callback?payload=${encodeURIComponent(signed)}&state=${encodeURIComponent(parsed.state)}`;

  return redirect(localCallback);
}

export async function handle(
  event: ScalewayEvent
): Promise<ScalewayResponse> {
  const path = event.path || "/";
  const params = event.queryStringParameters || {};

  if (path === "/health" || path === "/health/") {
    return json(200, { status: "ok" });
  }

  if (path === "/auth" || path === "/auth/") {
    return handleAuth(params);
  }

  if (path === "/callback" || path === "/callback/") {
    return handleCallback(params);
  }

  return json(404, { error: "Not found" });
}
