import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent))
from app import create_app                      # noqa: E402
from app.security import limiter                 # noqa: E402

TEACHER_CODE = "TEACH-2027"


@pytest.fixture
def app(tmp_path):
    app = create_app({
        "DATABASE_URL": "sqlite:///" + str(tmp_path / "test.db"),
        "ENV": "test", "SECRET_KEY": "test-secret-key-0123456789abcdef-xyz", "TEACHER_INVITE_CODE": TEACHER_CODE,
        "RATE_AUTH_PER_MIN": 1000, "RATE_WRITE_PER_MIN": 10000, "LOG_LEVEL": "WARNING",
    })
    limiter.reset()
    yield app
    limiter.reset()


@pytest.fixture
def client(app):
    return app.test_client()


def register(client, username="aibek", password="password1", name="Айбек", role="student", code=None):
    body = {"username": username, "password": password, "display_name": name, "role": role}
    if code:
        body["teacher_code"] = code
    r = client.post("/api/v1/auth/register", json=body)
    assert r.status_code == 201, r.get_json()
    return {"Authorization": "Bearer " + r.get_json()["token"]}, r.get_json()["user"]


@pytest.fixture
def student(client):
    return register(client)


@pytest.fixture
def teacher(client):
    return register(client, "mugalim", "password1", "Мугалим", "teacher", TEACHER_CODE)
