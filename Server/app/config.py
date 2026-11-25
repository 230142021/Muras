"""Configuration from environment variables (12-factor). No secrets in code."""
import os
import secrets


class Config:
    # Database: "sqlite:///path/to/file.db" (local) or "postgresql://user:pass@host:5432/db" (deployment)
    DATABASE_URL = os.environ.get("DATABASE_URL", "sqlite:///instance/muras.db")
    # Signs JWT tokens and dashboard session cookies. MUST be set in production.
    SECRET_KEY = os.environ.get("SECRET_KEY") or ""
    JWT_TTL_HOURS = int(os.environ.get("JWT_TTL_HOURS", "336"))          # 14 days
    AUTO_MIGRATE = os.environ.get("AUTO_MIGRATE", "1") == "1"
    ENV = os.environ.get("MURAS_ENV", "development")                      # development | production | test
    # Rate limits (requests per minute per client IP)
    RATE_AUTH_PER_MIN = int(os.environ.get("RATE_AUTH_PER_MIN", "10"))
    RATE_WRITE_PER_MIN = int(os.environ.get("RATE_WRITE_PER_MIN", "120"))
    # Teachers can only register with this invite code (empty = teacher self-registration disabled)
    TEACHER_INVITE_CODE = os.environ.get("TEACHER_INVITE_CODE", "")
    TRUST_PROXY = os.environ.get("TRUST_PROXY", "0") == "1"
    MAX_EVENTS_PER_BATCH = 100
    MAX_BODY_BYTES = 256 * 1024
    LOG_LEVEL = os.environ.get("LOG_LEVEL", "INFO")
    SESSION_COOKIE_HTTPONLY = True
    SESSION_COOKIE_SAMESITE = "Lax"
    SESSION_COOKIE_SECURE = os.environ.get("MURAS_ENV", "development") == "production"

    @classmethod
    def finalize(cls, cfg: dict) -> dict:
        if not cfg.get("SECRET_KEY"):
            if cfg.get("ENV") == "production":
                raise RuntimeError("SECRET_KEY must be set in production")
            cfg["SECRET_KEY"] = secrets.token_hex(32)     # dev/test only: random per process
        return cfg
