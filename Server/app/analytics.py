"""Class analytics for teachers: who plays, what they learned, which words are hardest."""
import secrets
from datetime import datetime, timedelta, timezone

from flask import current_app

from .db import get_db, loads
from .security import ApiError

CODE_ALPHABET = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"     # no 0/O/1/I — easy to dictate in class


def catalog():
    return current_app.extensions["muras_catalog"]


def new_join_code() -> str:
    db = get_db()
    for _ in range(20):
        code = "".join(secrets.choice(CODE_ALPHABET) for _ in range(6))
        if not db.one("SELECT id FROM classes WHERE join_code = ?", (code,)):
            return code
    raise ApiError(500, "code_generation_failed", "Could not generate a unique class code")


def owned_class(class_id: int, teacher_id: int) -> dict:
    cls = get_db().one("SELECT id, name, join_code, created_at FROM classes WHERE id = ? AND teacher_id = ?", (class_id, teacher_id))
    if cls is None:
        # 404 (not 403) so teachers cannot probe which class ids exist
        raise ApiError(404, "class_not_found", "Class not found")
    return cls


def student_summary(row: dict) -> dict:
    extra = loads(row.get("extra"), {}) or {}
    words = sum(1 for k, v in extra.items() if k.startswith("w_") and v == 1)
    ach = sum(1 for k, v in extra.items() if k.startswith("a_") and v == 1)
    quests = loads(row.get("quests"), []) or []
    return {
        "user_id": row["user_id"], "display_name": row["display_name"], "username": row["username"],
        "xp": row.get("xp") or 0, "words": words, "words_total": len(catalog()["words"]),
        "achievements": ach, "quests": len(quests), "quests_total": len(catalog()["quests"]),
        "last_seen_at": row.get("last_seen_at"), "joined_at": row.get("joined_at"),
    }


def class_students(class_id: int):
    rows = get_db().all(
        "SELECT u.id AS user_id, u.display_name, u.username, u.last_seen_at, m.joined_at, s.xp, s.quests, s.extra "
        "FROM class_members m JOIN users u ON u.id = m.user_id LEFT JOIN saves s ON s.user_id = u.id "
        "WHERE m.class_id = ? ORDER BY u.display_name", (class_id,))
    return rows, [student_summary(r) for r in rows]


def class_analytics(class_id: int) -> dict:
    rows, students = class_students(class_id)
    n = len(students)
    cat = catalog()
    extras = [loads(r.get("extra"), {}) or {} for r in rows]
    questsets = [set(loads(r.get("quests"), []) or []) for r in rows]

    words = []
    for w in cat["words"]:
        known = sum(1 for e in extras if e.get("w_" + w["id"]) == 1)
        words.append({"id": w["id"], "ky": w["ky"], "ru": w["ru"], "en": w["en"], "cat": w["cat"],
                      "known": known, "pct": round(100.0 * known / n, 1) if n else 0.0})
    # hardest = known by the fewest students (only words someone could already have met)
    hardest = sorted(words, key=lambda x: (x["pct"], x["id"]))[:10]
    names = cat.get("quest_names", {})
    quests = [{"id": q, "name": names.get(q, q), "done": sum(1 for s in questsets if q in s),
               "pct": round(100.0 * sum(1 for s in questsets if q in s) / n, 1) if n else 0.0} for q in cat["quests"]]

    ids = [s["user_id"] for s in students]
    activity = []
    if ids:
        since = (datetime.now(timezone.utc) - timedelta(days=13)).strftime("%Y-%m-%d")
        marks = ",".join("?" * len(ids))
        per_day = get_db().all(
            "SELECT SUBSTR(created_at, 1, 10) AS day, COUNT(*) AS events, COUNT(DISTINCT user_id) AS players FROM events "
            "WHERE user_id IN (%s) AND created_at >= ? GROUP BY SUBSTR(created_at, 1, 10) ORDER BY day" % marks, ids + [since])
        by_day = {r["day"]: r for r in per_day}
        for i in range(14):
            d = (datetime.now(timezone.utc) - timedelta(days=13 - i)).strftime("%Y-%m-%d")
            r = by_day.get(d)
            activity.append({"day": d, "events": r["events"] if r else 0, "players": r["players"] if r else 0})

    avg = lambda key: round(sum(s[key] for s in students) / n, 1) if n else 0.0
    return {
        "students": n,
        "avg_xp": avg("xp"), "avg_words": avg("words"), "avg_quests": avg("quests"),
        "words_total": len(cat["words"]),
        "hardest_words": hardest, "words": words, "quests": quests, "activity": activity,
    }


def student_detail(class_id: int, user_id: int) -> dict:
    db = get_db()
    row = db.one(
        "SELECT u.id AS user_id, u.display_name, u.username, u.last_seen_at, m.joined_at, s.xp, s.quests, s.extra, s.updated_at "
        "FROM class_members m JOIN users u ON u.id = m.user_id LEFT JOIN saves s ON s.user_id = u.id "
        "WHERE m.class_id = ? AND u.id = ?", (class_id, user_id))
    if row is None:
        raise ApiError(404, "student_not_found", "Student is not in this class")
    extra = loads(row.get("extra"), {}) or {}
    cat = catalog()
    known = [w for w in cat["words"] if extra.get("w_" + w["id"]) == 1]
    unknown = [w for w in cat["words"] if extra.get("w_" + w["id"]) != 1]
    ach = [a for a in cat["achievements"] if extra.get("a_" + a["id"]) == 1]
    events = db.all("SELECT type, payload, created_at FROM events WHERE user_id = ? ORDER BY id DESC LIMIT 30", (user_id,))
    scores = db.all("SELECT game, MAX(score) AS best_max, MIN(score) AS best_min, COUNT(*) AS plays FROM scores WHERE user_id = ? GROUP BY game",
                    (user_id,))
    return dict(student_summary(row), save_updated_at=row.get("updated_at"),
                known_words=known, unknown_words=unknown, achievements_list=ach,
                quests_list=loads(row.get("quests"), []) or [],
                recent_events=[dict(e, payload=loads(e["payload"], {})) for e in events], scores=scores)
