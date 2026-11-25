-- MURAS: initial schema (PostgreSQL)
CREATE TABLE users (
    id            SERIAL PRIMARY KEY,
    username      TEXT NOT NULL,
    password_hash TEXT NOT NULL,
    display_name  TEXT NOT NULL,
    role          TEXT NOT NULL DEFAULT 'student' CHECK (role IN ('student', 'teacher', 'admin')),
    created_at    TEXT NOT NULL,
    last_seen_at  TEXT
);
CREATE UNIQUE INDEX ux_users_username ON users (LOWER(username));

CREATE TABLE classes (
    id         SERIAL PRIMARY KEY,
    name       TEXT NOT NULL,
    join_code  TEXT NOT NULL UNIQUE,
    teacher_id INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    created_at TEXT NOT NULL
);
CREATE INDEX ix_classes_teacher ON classes(teacher_id);

CREATE TABLE class_members (
    class_id  INTEGER NOT NULL REFERENCES classes(id) ON DELETE CASCADE,
    user_id   INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    joined_at TEXT NOT NULL,
    PRIMARY KEY (class_id, user_id)
);
CREATE INDEX ix_members_user ON class_members(user_id);

CREATE TABLE saves (
    user_id     INTEGER PRIMARY KEY REFERENCES users(id) ON DELETE CASCADE,
    version     INTEGER NOT NULL DEFAULT 0,
    xp          INTEGER NOT NULL DEFAULT 0,
    quests      TEXT NOT NULL DEFAULT '[]',
    extra       TEXT NOT NULL DEFAULT '{}',
    updated_at  TEXT NOT NULL,
    client_info TEXT
);

CREATE TABLE events (
    id              SERIAL PRIMARY KEY,
    user_id         INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    client_event_id TEXT NOT NULL,
    type            TEXT NOT NULL,
    payload         TEXT NOT NULL DEFAULT '{}',
    client_time     TEXT,
    created_at      TEXT NOT NULL,
    UNIQUE (user_id, client_event_id)
);
CREATE INDEX ix_events_user_type ON events(user_id, type);
CREATE INDEX ix_events_created ON events(created_at);

CREATE TABLE scores (
    id          SERIAL PRIMARY KEY,
    user_id     INTEGER NOT NULL REFERENCES users(id) ON DELETE CASCADE,
    game        TEXT NOT NULL,
    score       INTEGER NOT NULL,
    duration_ms INTEGER,
    created_at  TEXT NOT NULL
);
CREATE INDEX ix_scores_game ON scores(game, score);
CREATE INDEX ix_scores_user ON scores(user_id, game);

CREATE TABLE audit_log (
    id         SERIAL PRIMARY KEY,
    user_id    INTEGER,
    action     TEXT NOT NULL,
    detail     TEXT,
    ip         TEXT,
    created_at TEXT NOT NULL
);
CREATE INDEX ix_audit_created ON audit_log(created_at);
