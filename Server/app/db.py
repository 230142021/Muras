"""Thin database layer over SQLite (stdlib) and PostgreSQL (psycopg 3).

The rest of the app writes portable SQL with `?` placeholders; for PostgreSQL they are
converted to `%s`. Schema changes live in versioned SQL files under migrations/<dialect>/.
"""
import json
import os
import re
import sqlite3
import time
from contextlib import contextmanager
from pathlib import Path

from flask import current_app, g

MIGRATIONS_DIR = Path(__file__).resolve().parent.parent / "migrations"


class Database:
    def __init__(self, url: str):
        self.url = url
        if url.startswith("sqlite:///"):
            self.dialect = "sqlite"
            self.path = url[len("sqlite:///"):]
            if self.path != ":memory:":
                Path(self.path).parent.mkdir(parents=True, exist_ok=True)
        elif url.startswith(("postgres://", "postgresql://")):
            self.dialect = "postgres"
            self.path = None
        else:
            raise ValueError("Unsupported DATABASE_URL (use sqlite:/// or postgresql://)")

    # ------------------------------------------------------------------ connections
    def connect(self):
        if self.dialect == "sqlite":
            conn = sqlite3.connect(self.path, timeout=10, isolation_level=None, check_same_thread=False)
            conn.row_factory = sqlite3.Row
            conn.execute("PRAGMA foreign_keys = ON")
            conn.execute("PRAGMA journal_mode = WAL")
            conn.execute("PRAGMA busy_timeout = 5000")
            return conn
        import psycopg                     # imported lazily: not needed for local SQLite runs
        from psycopg.rows import dict_row
        url = self.url.replace("postgres://", "postgresql://", 1)
        return psycopg.connect(url, row_factory=dict_row, autocommit=True)

    def sql(self, q: str) -> str:
        return q.replace("?", "%s") if self.dialect == "postgres" else q

    # ------------------------------------------------------------------ migrations
    def migrate(self, conn=None) -> list:
        own = conn is None
        conn = conn or self.connect()
        try:
            cur = conn.cursor()
            cur.execute("CREATE TABLE IF NOT EXISTS schema_migrations (version TEXT PRIMARY KEY, applied_at TEXT NOT NULL)")
            cur.execute("SELECT version FROM schema_migrations")
            done = {(r["version"] if not isinstance(r, tuple) else r[0]) for r in cur.fetchall()}
            applied = []
            for f in sorted((MIGRATIONS_DIR / self.dialect).glob("*.sql")):
                version = f.stem
                if version in done:
                    continue
                script = f.read_text(encoding="utf-8")
                if self.dialect == "sqlite":
                    conn.executescript("BEGIN;" + script + ";COMMIT;")
                else:
                    with conn.transaction():
                        conn.execute(script)
                cur = conn.cursor()
                cur.execute(self.sql("INSERT INTO schema_migrations (version, applied_at) VALUES (?, ?)"), (version, now_iso()))
                applied.append(version)
            return applied
        finally:
            if own:
                conn.close()


class Conn:
    """Request-scoped connection wrapper with dict rows and portable placeholders."""

    def __init__(self, db: Database, raw):
        self.db, self.raw = db, raw

    def execute(self, q, params=()):
        cur = self.raw.cursor()
        cur.execute(self.db.sql(q), tuple(params))
        return cur

    def one(self, q, params=()):
        r = self.execute(q, params).fetchone()
        return dict(r) if r is not None else None

    def all(self, q, params=()):
        return [dict(r) for r in self.execute(q, params).fetchall()]

    def scalar(self, q, params=(), default=None):
        r = self.execute(q, params).fetchone()
        if r is None:
            return default
        v = list(dict(r).values())[0]
        return default if v is None else v

    @contextmanager
    def transaction(self):
        if self.db.dialect == "sqlite":
            self.raw.execute("BEGIN IMMEDIATE")
            try:
                yield self
                self.raw.execute("COMMIT")
            except Exception:
                self.raw.execute("ROLLBACK")
                raise
        else:
            with self.raw.transaction():
                yield self


def now_iso() -> str:
    return time.strftime("%Y-%m-%dT%H:%M:%SZ", time.gmtime())


def get_db() -> Conn:
    if "db" not in g:
        db: Database = current_app.extensions["muras_db"]
        g.db = Conn(db, db.connect())
    return g.db


def close_db(_exc=None):
    c = g.pop("db", None)
    if c is not None:
        c.raw.close()


def dumps(obj) -> str:
    return json.dumps(obj, ensure_ascii=False, separators=(",", ":"))


def loads(s, default=None):
    if s is None or s == "":
        return default
    try:
        return json.loads(s)
    except (ValueError, TypeError):
        return default
