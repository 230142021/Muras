"""Teacher API (JSON): manage classes and read class analytics. Role 'teacher' required."""
from flask import Blueprint, g, jsonify

from ..analytics import class_analytics, class_students, new_join_code, owned_class, student_detail
from ..db import get_db, now_iso
from ..rules import ClassIn
from ..security import audit, rate_limit, require_auth
from .api import body

bp = Blueprint("teacher_api", __name__)


@bp.get("/classes")
@require_auth("teacher")
def list_classes():
    rows = get_db().all(
        "SELECT c.id, c.name, c.join_code, c.created_at, COUNT(m.user_id) AS students FROM classes c "
        "LEFT JOIN class_members m ON m.class_id = c.id WHERE c.teacher_id = ? GROUP BY c.id, c.name, c.join_code, c.created_at ORDER BY c.id",
        (g.user["id"],))
    return jsonify(classes=rows)


@bp.post("/classes")
@require_auth("teacher")
@rate_limit("write", "RATE_WRITE_PER_MIN")
def create_class():
    data = body(ClassIn)
    db = get_db()
    code = new_join_code()
    cid = db.scalar("INSERT INTO classes (name, join_code, teacher_id, created_at) VALUES (?, ?, ?, ?) RETURNING id",
                    (data.name.strip(), code, g.user["id"], now_iso()))
    audit("class_created", "%d %s" % (cid, data.name), g.user["id"])
    return jsonify(id=cid, name=data.name.strip(), join_code=code), 201


@bp.get("/classes/<int:class_id>")
@require_auth("teacher")
def class_overview(class_id):
    cls = owned_class(class_id, g.user["id"])
    _, students = class_students(class_id)
    return jsonify(dict(cls, students=students))


@bp.get("/classes/<int:class_id>/analytics")
@require_auth("teacher")
def analytics(class_id):
    cls = owned_class(class_id, g.user["id"])
    return jsonify(dict(cls, **class_analytics(class_id)))


@bp.get("/classes/<int:class_id>/students/<int:user_id>")
@require_auth("teacher")
def student(class_id, user_id):
    owned_class(class_id, g.user["id"])
    return jsonify(student_detail(class_id, user_id))


@bp.delete("/classes/<int:class_id>/students/<int:user_id>")
@require_auth("teacher")
def remove_student(class_id, user_id):
    owned_class(class_id, g.user["id"])
    get_db().execute("DELETE FROM class_members WHERE class_id = ? AND user_id = ?", (class_id, user_id))
    audit("student_removed", "class %d user %d" % (class_id, user_id), g.user["id"])
    return "", 204
