"""MURAS game backend — Flask application factory."""
import json
import logging
import time
import uuid
from pathlib import Path

from flask import Flask, g, jsonify, request
from pydantic import ValidationError
from werkzeug.exceptions import HTTPException

from .config import Config
from .db import Database, close_db, get_db
from .security import ApiError, error_response

CATALOG_PATH = Path(__file__).resolve().parent.parent / "data" / "catalog.json"


def create_app(overrides: dict = None) -> Flask:
    app = Flask(__name__, instance_relative_config=False)
    cfg = {k: getattr(Config, k) for k in dir(Config) if k.isupper()}
    cfg.update(overrides or {})
    app.config.update(Config.finalize(cfg))
    app.config["MAX_CONTENT_LENGTH"] = app.config["MAX_BODY_BYTES"]
    app.json.ensure_ascii = False

    logging.basicConfig(level=app.config["LOG_LEVEL"], format="%(asctime)s %(levelname)s %(name)s %(message)s")
    log = logging.getLogger("muras")

    db = Database(app.config["DATABASE_URL"])
    app.extensions["muras_db"] = db
    app.extensions["muras_catalog"] = json.loads(CATALOG_PATH.read_text(encoding="utf-8"))
    if app.config["AUTO_MIGRATE"]:
        applied = db.migrate()
        if applied:
            log.info("migrations applied: %s", ", ".join(applied))

    # ------------------------------------------------------------------ request lifecycle
    @app.before_request
    def _start():
        g.t0 = time.perf_counter()
        g.request_id = request.headers.get("X-Request-ID") or uuid.uuid4().hex[:12]

    @app.after_request
    def _finish(resp):
        resp.headers["X-Request-ID"] = g.get("request_id", "")
        resp.headers["X-Content-Type-Options"] = "nosniff"
        resp.headers["X-Frame-Options"] = "DENY"
        resp.headers["Referrer-Policy"] = "same-origin"
        if request.path.startswith("/teacher"):
            resp.headers["Content-Security-Policy"] = "default-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:"
        if request.path != "/health":
            ms = (time.perf_counter() - g.get("t0", time.perf_counter())) * 1000
            user = g.get("user")
            log.info("%s %s %s %.1fms user=%s rid=%s", request.method, request.path, resp.status_code, ms,
                     user["id"] if user else "-", g.get("request_id"))
        return resp

    app.teardown_appcontext(close_db)

    # ------------------------------------------------------------------ errors (one JSON shape for the API)
    @app.errorhandler(ApiError)
    def _api_error(e):
        return error_response(e.status, e.code, e.message, e.details)

    @app.errorhandler(ValidationError)
    def _validation(e):
        details = [{"field": ".".join(str(p) for p in err["loc"]), "message": err["msg"]} for err in e.errors()]
        return error_response(422, "validation_error", "Request body is invalid", details)

    @app.errorhandler(HTTPException)
    def _http(e):
        if request.path.startswith("/teacher") and e.code in (404, 405):
            return e
        return error_response(e.code, e.name.lower().replace(" ", "_"), e.description or e.name)

    @app.errorhandler(Exception)
    def _unhandled(e):
        log.exception("unhandled error rid=%s", g.get("request_id"))
        return error_response(500, "internal_error", "Internal server error (request id %s)" % g.get("request_id"))

    # ------------------------------------------------------------------ routes
    from .routes.api import bp as api_bp
    from .routes.teacher_api import bp as teacher_api_bp
    from .routes.dashboard import bp as dashboard_bp
    app.register_blueprint(api_bp, url_prefix="/api/v1")
    app.register_blueprint(teacher_api_bp, url_prefix="/api/v1/teacher")
    app.register_blueprint(dashboard_bp, url_prefix="/teacher")

    @app.get("/health")
    def health():
        try:
            get_db().scalar("SELECT 1 AS ok")
            return jsonify(status="ok", db="ok", version=app.config.get("APP_VERSION", "1.0.0"))
        except Exception as e:                         # pragma: no cover - only when the DB is down
            log.error("health check failed: %s", e)
            return jsonify(status="degraded", db="error"), 503

    @app.get("/")
    def index():
        return jsonify(name="MURAS API", docs="/api/v1/openapi.yaml", health="/health", teacher="/teacher/")

    @app.cli.command("migrate")
    def migrate_cmd():
        """Apply pending database migrations."""
        print("applied:", db.migrate() or "nothing (up to date)")

    return app
