"""Passwords, JWT tokens, role checks, rate limiting and the unified error format."""
import base64
import hashlib
import hmac
import secrets
import threading
import time
from collections import defaultdict, deque
from datetime import datetime, timedelta, timezone
from functools import wraps

import jwt
from flask import current_app, g, jsonify, request

from .db import get_db, now_iso

PBKDF2_ITERATIONS = 240_000


# ---------------------------------------------------------------------- errors
class ApiError(Exception):
    def __init__(self, status: int, code: str, message: str, details=None):
        super().__init__(message)
        self.status, self.code, self.message, self.details = status, code, message, details


def error_response(status, code, message, details=None):
    body = {"error": {"code": code, "message": message}}
    if details:
        body["error"]["details"] = details
    return jsonify(body), status


# ---------------------------------------------------------------------- passwords
def hash_password(password: str) -> str:
    salt = secrets.token_bytes(16)
    dk = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), salt, PBKDF2_ITERATIONS)
    return "pbkdf2_sha256${}${}${}".format(PBKDF2_ITERATIONS, base64.b64encode(salt).decode(), base64.b64encode(dk).decode())


def verify_password(password: str, stored: str) -> bool:
    try:
        algo, iters, salt_b64, hash_b64 = stored.split("$")
        if algo != "pbkdf2_sha256":
            return False
        dk = hashlib.pbkdf2_hmac("sha256", password.encode("utf-8"), base64.b64decode(salt_b64), int(iters))
        return hmac.compare_digest(dk, base64.b64decode(hash_b64))
    except (ValueError, TypeError):
        return False


# ---------------------------------------------------------------------- tokens
def issue_token(user: dict) -> str:
    now = datetime.now(timezone.utc)
    payload = {
        "sub": str(user["id"]),
        "role": user["role"],
        "iat": int(now.timestamp()),
        "exp": int((now + timedelta(hours=current_app.config["JWT_TTL_HOURS"])).timestamp()),
    }
    return jwt.encode(payload, current_app.config["SECRET_KEY"], algorithm="HS256")


def decode_token(token: str) -> dict:
    return jwt.decode(token, current_app.config["SECRET_KEY"], algorithms=["HS256"])


def current_user():
    return g.get("user")


def require_auth(*roles):
    """Decorator: valid Bearer token required; optionally restrict to roles."""
    def deco(fn):
        @wraps(fn)
        def wrapper(*args, **kwargs):
            header = request.headers.get("Authorization", "")
            if not header.startswith("Bearer "):
                raise ApiError(401, "unauthorized", "Missing bearer token")
            try:
                claims = decode_token(header[7:].strip())
            except jwt.ExpiredSignatureError:
                raise ApiError(401, "token_expired", "Token expired, please log in again")
            except jwt.InvalidTokenError:
                raise ApiError(401, "unauthorized", "Invalid token")
            db = get_db()
            user = db.one("SELECT id, username, display_name, role FROM users WHERE id = ?", (int(claims["sub"]),))
            if user is None:
                raise ApiError(401, "unauthorized", "User no longer exists")
            if roles and user["role"] not in roles:
                raise ApiError(403, "forbidden", "This action requires role: " + ", ".join(roles))
            g.user = user
            db.execute("UPDATE users SET last_seen_at = ? WHERE id = ?", (now_iso(), user["id"]))
            return fn(*args, **kwargs)
        return wrapper
    return deco


# ---------------------------------------------------------------------- rate limiting
class RateLimiter:
    """Sliding-window limiter per (bucket, client IP). In-memory: fine for one instance."""

    def __init__(self):
        self.hits = defaultdict(deque)
        self.lock = threading.Lock()

    def check(self, bucket: str, limit: int, window: float = 60.0):
        key = (bucket, client_ip())
        now = time.monotonic()
        with self.lock:
            q = self.hits[key]
            while q and now - q[0] > window:
                q.popleft()
            if len(q) >= limit:
                retry = int(window - (now - q[0])) + 1
                raise ApiError(429, "rate_limited", "Too many requests, try again in %d s" % retry, {"retry_after": retry})
            q.append(now)

    def reset(self):
        with self.lock:
            self.hits.clear()


limiter = RateLimiter()


def rate_limit(bucket: str, config_key: str):
    def deco(fn):
        @wraps(fn)
        def wrapper(*args, **kwargs):
            limiter.check(bucket, current_app.config[config_key])
            return fn(*args, **kwargs)
        return wrapper
    return deco


def client_ip() -> str:
    # Behind a reverse proxy (Render, nginx) the real client address is in X-Forwarded-For.
    # Only trust that header when TRUST_PROXY=1, otherwise a client could spoof it to dodge rate limits.
    if current_app.config.get("TRUST_PROXY"):
        fwd = request.headers.get("X-Forwarded-For", "")
        if fwd:
            return fwd.split(",")[0].strip()
    return request.remote_addr or "?"


def audit(action: str, detail: str = "", user_id=None):
    get_db().execute("INSERT INTO audit_log (user_id, action, detail, ip, created_at) VALUES (?, ?, ?, ?, ?)",
                     (user_id, action, detail[:500], client_ip(), now_iso()))
