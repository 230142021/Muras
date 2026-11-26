"""Teacher web dashboard (server-rendered HTML). Session cookie + CSRF token on every form."""
import csv
import io
import secrets
from functools import wraps

from flask import Blueprint, Response, abort, current_app, g, redirect, render_template, request, session, url_for

from ..analytics import catalog, class_analytics, class_students, new_join_code, owned_class, student_detail
from ..db import get_db, now_iso
from ..learning import activity_summary, class_learning, test_results, test_summary
from ..rules import GAMES, USERNAME_RE
from ..security import ApiError, audit, hash_password, limiter, verify_password

bp = Blueprint("dashboard", __name__, template_folder="../templates", static_folder="../static", static_url_path="/static")


def csrf_token() -> str:
    if "csrf" not in session:
        session["csrf"] = secrets.token_urlsafe(24)
    return session["csrf"]


@bp.app_context_processor
def _inject():
    return {"csrf_token": csrf_token, "teacher": g.get("teacher")}


def check_csrf():
    sent = request.form.get("csrf", "")
    if not sent or not secrets.compare_digest(sent, session.get("csrf", "")):
        abort(400, "CSRF token missing or invalid")


def teacher_required(fn):
    @wraps(fn)
    def wrapper(*args, **kwargs):
        uid = session.get("uid")
        user = get_db().one("SELECT id, username, display_name, role FROM users WHERE id = ?", (uid,)) if uid else None
        if user is None or user["role"] != "teacher":
            session.pop("uid", None)
            return redirect(url_for("dashboard.login", next=request.path))
        g.teacher = user
        return fn(*args, **kwargs)
    return wrapper


def limited(bucket, key):
    try:
        limiter.check(bucket, current_app.config[key])
        return None
    except ApiError as e:
        return e.message


# ---------------------------------------------------------------------- auth pages
@bp.route("/login", methods=["GET", "POST"])
def login():
    error = None
    if request.method == "POST":
        check_csrf()
        error = limited("dash_auth", "RATE_AUTH_PER_MIN")
        if not error:
            db = get_db()
            user = db.one("SELECT id, role, password_hash FROM users WHERE LOWER(username) = LOWER(?)", (request.form.get("username", ""),))
            if user and user["role"] == "teacher" and verify_password(request.form.get("password", ""), user["password_hash"]):
                session.clear()
                session["uid"] = user["id"]
                session.permanent = True
                audit("dashboard_login", "", user["id"])
                nxt = request.args.get("next", "")
                return redirect(nxt if nxt.startswith("/teacher") else url_for("dashboard.home"))
            audit("dashboard_login_failed", request.form.get("username", "")[:40])
            error = "Неверный логин или пароль (нужна учётная запись учителя)."
    return render_template("login.html", error=error)


@bp.route("/register", methods=["GET", "POST"])
def register():
    error = None
    if request.method == "POST":
        check_csrf()
        error = limited("dash_auth", "RATE_AUTH_PER_MIN")
        f = request.form
        invite = current_app.config["TEACHER_INVITE_CODE"]
        if not error and (not invite or f.get("invite", "") != invite):
            error = "Неверный код приглашения учителя."
        elif not error and not USERNAME_RE.match(f.get("username", "")):
            error = "Логин: 3–32 символа, латиница, цифры, _ . -"
        elif not error and len(f.get("password", "")) < 8:
            error = "Пароль должен быть не короче 8 символов."
        elif not error and not f.get("display_name", "").strip():
            error = "Укажите имя."
        if not error:
            db = get_db()
            if db.one("SELECT id FROM users WHERE LOWER(username) = LOWER(?)", (f["username"],)):
                error = "Такой логин уже занят."
            else:
                uid = db.scalar("INSERT INTO users (username, password_hash, display_name, role, created_at) VALUES (?, ?, ?, 'teacher', ?) RETURNING id",
                                (f["username"], hash_password(f["password"]), f["display_name"].strip()[:48], now_iso()))
                db.execute("INSERT INTO saves (user_id, version, xp, quests, extra, updated_at) VALUES (?, 0, 0, '[]', '{}', ?)", (uid, now_iso()))
                audit("register", "teacher (dashboard)", uid)
                session.clear()
                session["uid"] = uid
                return redirect(url_for("dashboard.home"))
    return render_template("register.html", error=error)


@bp.post("/logout")
def logout():
    check_csrf()
    session.clear()
    return redirect(url_for("dashboard.login"))


# ---------------------------------------------------------------------- pages
@bp.get("/")
@teacher_required
def home():
    rows = get_db().all(
        "SELECT c.id, c.name, c.join_code, c.created_at, COUNT(m.user_id) AS students FROM classes c "
        "LEFT JOIN class_members m ON m.class_id = c.id WHERE c.teacher_id = ? GROUP BY c.id, c.name, c.join_code, c.created_at ORDER BY c.id",
        (g.teacher["id"],))
    return render_template("home.html", classes=rows)


@bp.post("/classes")
@teacher_required
def create_class():
    check_csrf()
    name = request.form.get("name", "").strip()[:60]
    if name:
        db = get_db()
        cid = db.scalar("INSERT INTO classes (name, join_code, teacher_id, created_at) VALUES (?, ?, ?, ?) RETURNING id",
                        (name, new_join_code(), g.teacher["id"], now_iso()))
        audit("class_created", "%d %s" % (cid, name), g.teacher["id"])
        return redirect(url_for("dashboard.class_page", class_id=cid))
    return redirect(url_for("dashboard.home"))


@bp.get("/class/<int:class_id>")
@teacher_required
def class_page(class_id):
    try:
        cls = owned_class(class_id, g.teacher["id"])
    except ApiError:
        abort(404)
    _, students = class_students(class_id)
    sort = request.args.get("sort", "name")
    keys = {"xp": lambda s: -s["xp"], "words": lambda s: -s["words"], "seen": lambda s: s["last_seen_at"] or "", "name": lambda s: s["display_name"].lower()}
    students.sort(key=keys.get(sort, keys["name"]), reverse=(sort == "seen"))
    ids = [s["user_id"] for s in students]
    learning = class_learning(ids)
    for s in students:                                   # test columns in the students table
        s["test"] = learning["tests"]["students"].get(s["user_id"]) or {"pre": None, "post": None, "gain": None}
    return render_template("class.html", cls=cls, students=students, a=class_analytics(class_id), l=learning, sort=sort)


@bp.get("/class/<int:class_id>/student/<int:user_id>")
@teacher_required
def student_page(class_id, user_id):
    try:
        cls = owned_class(class_id, g.teacher["id"])
        st = student_detail(class_id, user_id)
    except ApiError:
        abort(404)
    tests = test_results([user_id]).get(user_id, {})
    questions = catalog().get("knowledge_test", [])
    return render_template("student.html", cls=cls, s=st, games=GAMES, achievements=catalog()["achievements"],
                           tests=tests, questions=questions, activity=activity_summary([user_id]))


@bp.post("/class/<int:class_id>/student/<int:user_id>/remove")
@teacher_required
def remove_student(class_id, user_id):
    check_csrf()
    try:
        owned_class(class_id, g.teacher["id"])
    except ApiError:
        abort(404)
    get_db().execute("DELETE FROM class_members WHERE class_id = ? AND user_id = ?", (class_id, user_id))
    audit("student_removed", "class %d user %d" % (class_id, user_id), g.teacher["id"])
    return redirect(url_for("dashboard.class_page", class_id=class_id))


@bp.get("/class/<int:class_id>/export.csv")
@teacher_required
def export_csv(class_id):
    try:
        cls = owned_class(class_id, g.teacher["id"])
    except ApiError:
        abort(404)
    _, students = class_students(class_id)
    buf = io.StringIO()
    w = csv.writer(buf)
    w.writerow(["display_name", "username", "xp", "words", "words_total", "quests", "achievements", "last_seen_at"])
    for s in students:
        # prefix cells that start with formula characters (CSV injection protection for Excel)
        name = s["display_name"]
        if name[:1] in ("=", "+", "-", "@"):
            name = "'" + name
        w.writerow([name, s["username"], s["xp"], s["words"], s["words_total"], s["quests"], s["achievements"], s["last_seen_at"] or ""])
    return Response("﻿" + buf.getvalue(), mimetype="text/csv",
                    headers={"Content-Disposition": "attachment; filename=class_%d.csv" % cls["id"]})


@bp.get("/class/<int:class_id>/tests.csv")
@teacher_required
def export_tests_csv(class_id):
    """Pre/post knowledge test results, one row per student, one column per question (for the thesis statistics)."""
    try:
        cls = owned_class(class_id, g.teacher["id"])
    except ApiError:
        abort(404)
    _, students = class_students(class_id)
    results = test_results([s["user_id"] for s in students])
    questions = catalog().get("knowledge_test", [])
    buf = io.StringIO()
    w = csv.writer(buf)
    w.writerow(["display_name", "username", "pre_score", "post_score", "gain", "pre_seconds", "post_seconds"]
               + ["pre_" + q["id"] for q in questions] + ["post_" + q["id"] for q in questions])
    for s in students:
        r = results.get(s["user_id"], {})
        pre, post = r.get("pre"), r.get("post")
        name = s["display_name"]
        if name[:1] in ("=", "+", "-", "@"):
            name = "'" + name
        cell = lambda t, q: "" if t is None or q["id"] not in t["answers"] else int(t["answers"][q["id"]])
        w.writerow([name, s["username"], pre["score"] if pre else "", post["score"] if post else "",
                    (post["score"] - pre["score"]) if pre and post else "", pre["seconds"] if pre else "", post["seconds"] if post else ""]
                   + [cell(pre, q) for q in questions] + [cell(post, q) for q in questions])
    return Response("\ufeff" + buf.getvalue(), mimetype="text/csv",
                    headers={"Content-Disposition": "attachment; filename=class_%d_tests.csv" % cls["id"]})
