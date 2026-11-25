"""Player-facing API used by the Unity game client."""
from pathlib import Path

from flask import Blueprint, current_app, g, jsonify, request, send_file

from ..db import dumps, get_db, loads, now_iso
from ..rules import (GAMES, EventsIn, JoinIn, LoginIn, RegisterIn, SaveIn, ScoreIn, merge_saves, validate_score)
from ..security import (ApiError, audit, hash_password, issue_token, rate_limit, require_auth, verify_password)

bp = Blueprint("api", __name__)
_DUMMY_HASH = hash_password("timing-equaliser")   # compared when the user does not exist (no user enumeration via timing)


def body(model):
    data = request.get_json(silent=True)
    if not isinstance(data, dict):
        raise ApiError(400, "bad_request", "Expected a JSON object body")
    return model.model_validate(data)


def public_user(u: dict) -> dict:
    return {"id": u["id"], "username": u["username"], "display_name": u["display_name"], "role": u["role"]}


def save_row(user_id: int) -> dict:
    db = get_db()
    r = db.one("SELECT version, xp, quests, extra, updated_at FROM saves WHERE user_id = ?", (user_id,))
    if r is None:
        return {"version": 0, "xp": 0, "quests": [], "extra": {}, "updated_at": None}
    return {"version": r["version"], "xp": r["xp"], "quests": loads(r["quests"], []), "extra": loads(r["extra"], {}),
            "updated_at": r["updated_at"]}


# ====================================================================== auth
@bp.post("/auth/register")
@rate_limit("auth", "RATE_AUTH_PER_MIN")
def register():
    data = body(RegisterIn)
    if data.role == "teacher":
        code = current_app.config["TEACHER_INVITE_CODE"]
        if not code or data.teacher_code != code:
            raise ApiError(403, "teacher_code_invalid", "A valid teacher invite code is required")
    db = get_db()
    if db.one("SELECT id FROM users WHERE LOWER(username) = LOWER(?)", (data.username,)):
        raise ApiError(409, "username_taken", "This username is already taken")
    with db.transaction():
        uid = db.scalar("INSERT INTO users (username, password_hash, display_name, role, created_at) VALUES (?, ?, ?, ?, ?) RETURNING id",
                        (data.username, hash_password(data.password), data.display_name, data.role, now_iso()))
        db.execute("INSERT INTO saves (user_id, version, xp, quests, extra, updated_at) VALUES (?, 0, 0, '[]', '{}', ?)", (uid, now_iso()))
    user = db.one("SELECT id, username, display_name, role FROM users WHERE id = ?", (uid,))
    audit("register", data.role, uid)
    return jsonify(token=issue_token(user), user=public_user(user)), 201


@bp.post("/auth/login")
@rate_limit("auth", "RATE_AUTH_PER_MIN")
def login():
    data = body(LoginIn)
    db = get_db()
    user = db.one("SELECT id, username, display_name, role, password_hash FROM users WHERE LOWER(username) = LOWER(?)", (data.username,))
    ok = verify_password(data.password, user["password_hash"] if user else _DUMMY_HASH)
    if not user or not ok:
        audit("login_failed", data.username[:40], user["id"] if user else None)
        raise ApiError(401, "invalid_credentials", "Wrong username or password")
    db.execute("UPDATE users SET last_seen_at = ? WHERE id = ?", (now_iso(), user["id"]))
    audit("login", "", user["id"])
    return jsonify(token=issue_token(user), user=public_user(user))


# ====================================================================== account
@bp.get("/me")
@require_auth()
def me():
    u, db = g.user, get_db()
    if u["role"] == "teacher":
        classes = db.all("SELECT id, name, join_code FROM classes WHERE teacher_id = ? ORDER BY id", (u["id"],))
    else:
        classes = db.all("SELECT c.id, c.name FROM classes c JOIN class_members m ON m.class_id = c.id WHERE m.user_id = ? ORDER BY c.id", (u["id"],))
    return jsonify(user=public_user(u), classes=classes)


@bp.get("/me/export")
@require_auth()
def export_me():
    """Privacy: everything the server stores about the player, as JSON."""
    u, db = g.user, get_db()
    return jsonify(
        user=public_user(u),
        save=save_row(u["id"]),
        classes=db.all("SELECT c.name, m.joined_at FROM class_members m JOIN classes c ON c.id = m.class_id WHERE m.user_id = ?", (u["id"],)),
        scores=db.all("SELECT game, score, duration_ms, created_at FROM scores WHERE user_id = ? ORDER BY id", (u["id"],)),
        events=[dict(e, payload=loads(e["payload"], {})) for e in
                db.all("SELECT type, payload, client_time, created_at FROM events WHERE user_id = ? ORDER BY id", (u["id"],))],
    )


@bp.delete("/me")
@require_auth()
def delete_me():
    """Privacy: delete the account and all its data (password confirmation required)."""
    data = request.get_json(silent=True) or {}
    db = get_db()
    row = db.one("SELECT password_hash FROM users WHERE id = ?", (g.user["id"],))
    if not verify_password(str(data.get("password", "")), row["password_hash"]):
        raise ApiError(403, "invalid_credentials", "Password confirmation failed")
    audit("account_deleted", g.user["username"], None)
    db.execute("DELETE FROM users WHERE id = ?", (g.user["id"],))     # ON DELETE CASCADE removes saves/events/scores
    return "", 204


@bp.post("/classes/join")
@require_auth("student")
@rate_limit("write", "RATE_WRITE_PER_MIN")
def join_class():
    data = body(JoinIn)
    db = get_db()
    cls = db.one("SELECT id, name FROM classes WHERE join_code = ?", (data.code.strip().upper(),))
    if cls is None:
        raise ApiError(404, "class_not_found", "No class with this code")
    db.execute("INSERT INTO class_members (class_id, user_id, joined_at) VALUES (?, ?, ?) ON CONFLICT DO NOTHING",
               (cls["id"], g.user["id"], now_iso()))
    return jsonify(**{"class": cls, "joined": True})


# ====================================================================== cloud save
@bp.get("/progress")
@require_auth()
def get_progress():
    return jsonify(save_row(g.user["id"]))


@bp.put("/progress")
@require_auth()
@rate_limit("write", "RATE_WRITE_PER_MIN")
def put_progress():
    """Optimistic concurrency: if base_version matches, the client save is stored as is (fast-forward).
    If another device wrote in between, the two saves are merged (see rules.merge_saves) and the merged
    result is returned so the client can apply it."""
    data = body(SaveIn)
    db = get_db()
    lock = " FOR UPDATE" if db.db.dialect == "postgres" else ""
    with db.transaction():
        cur = db.one("SELECT version, xp, quests, extra FROM saves WHERE user_id = ?" + lock, (g.user["id"],))
        server = {"version": cur["version"], "xp": cur["xp"], "quests": loads(cur["quests"], []), "extra": loads(cur["extra"], {})}
        client = {"xp": data.xp, "quests": data.quests, "extra": data.extra}
        if data.base_version == server["version"]:
            result, merged = client, False
        elif data.base_version > server["version"]:
            raise ApiError(409, "version_ahead", "base_version is newer than the server copy; pull first")
        else:
            result, merged = merge_saves(server, client), True
        version = server["version"] + 1
        db.execute("UPDATE saves SET version = ?, xp = ?, quests = ?, extra = ?, updated_at = ?, client_info = ? WHERE user_id = ?",
                   (version, result["xp"], dumps(result["quests"]), dumps(result["extra"]), now_iso(), data.client_info, g.user["id"]))
    return jsonify(version=version, merged=merged, xp=result["xp"], quests=result["quests"], extra=result["extra"] if merged else None)


# ====================================================================== events
@bp.post("/events")
@require_auth()
@rate_limit("write", "RATE_WRITE_PER_MIN")
def post_events():
    data = body(EventsIn)
    db, accepted = get_db(), 0
    with db.transaction():
        for e in data.events:
            cur = db.execute("INSERT INTO events (user_id, client_event_id, type, payload, client_time, created_at) VALUES (?, ?, ?, ?, ?, ?) "
                             "ON CONFLICT (user_id, client_event_id) DO NOTHING",
                             (g.user["id"], e.id, e.type, dumps(e.payload)[:4000], e.time, now_iso()))
            accepted += cur.rowcount if cur.rowcount and cur.rowcount > 0 else 0
    return jsonify(accepted=accepted, duplicates=len(data.events) - accepted)


# ====================================================================== scores & leaderboards
@bp.get("/games")
def games():
    return jsonify(games=[dict(id=k, **v) for k, v in GAMES.items()])


@bp.post("/scores")
@require_auth()
@rate_limit("write", "RATE_WRITE_PER_MIN")
def post_score():
    data = body(ScoreIn)
    problem = validate_score(data.game, data.score)
    if problem:
        audit("score_rejected", "%s=%d (%s)" % (data.game, data.score, problem), g.user["id"])
        raise ApiError(422, "score_rejected", problem)
    db = get_db()
    db.execute("INSERT INTO scores (user_id, game, score, duration_ms, created_at) VALUES (?, ?, ?, ?, ?)",
               (g.user["id"], data.game, data.score, data.duration_ms, now_iso()))
    best_fn = "MAX" if GAMES[data.game]["order"] == "desc" else "MIN"
    best = db.scalar("SELECT %s(score) AS b FROM scores WHERE user_id = ? AND game = ?" % best_fn, (g.user["id"], data.game))
    return jsonify(saved=True, personal_best=best, is_record=best == data.score), 201


def leaderboard_rows(game: str, limit: int, member_ids=None):
    rule = GAMES[game]
    fn, order = ("MAX", "DESC") if rule["order"] == "desc" else ("MIN", "ASC")
    where, params = "s.game = ?", [game]
    if member_ids is not None:
        if not member_ids:
            return []
        where += " AND s.user_id IN (%s)" % ",".join("?" * len(member_ids))
        params += list(member_ids)
    rows = get_db().all(
        "SELECT u.id AS user_id, u.display_name, %s(s.score) AS best FROM scores s JOIN users u ON u.id = s.user_id "
        "WHERE %s GROUP BY u.id, u.display_name ORDER BY best %s, u.id ASC LIMIT ?" % (fn, where, order), params + [limit])
    for i, r in enumerate(rows):
        r["rank"] = i + 1
    return rows


@bp.get("/leaderboard/<game>")
@require_auth()
def leaderboard(game):
    if game not in GAMES:
        raise ApiError(404, "unknown_game", "Unknown game")
    try:
        limit = max(1, min(50, int(request.args.get("limit", 10))))
    except ValueError:
        raise ApiError(400, "bad_request", "limit must be an integer")
    scope = request.args.get("scope", "global")
    members = None
    if scope == "class":
        members = [r["user_id"] for r in get_db().all(
            "SELECT DISTINCT m2.user_id FROM class_members m1 JOIN class_members m2 ON m2.class_id = m1.class_id WHERE m1.user_id = ?",
            (g.user["id"],))]
    elif scope != "global":
        raise ApiError(400, "bad_request", "scope must be global or class")
    rows = leaderboard_rows(game, limit, members)
    mine = leaderboard_rows(game, 100000, members)
    me_row = next((r for r in mine if r["user_id"] == g.user["id"]), None)
    return jsonify(game=game, title=GAMES[game]["title"], order=GAMES[game]["order"], unit=GAMES[game]["unit"],
                   scope=scope, top=rows, me=me_row)


@bp.get("/openapi.yaml")
def openapi():
    return send_file(Path(__file__).resolve().parents[2] / "docs" / "openapi.yaml", mimetype="text/yaml")
