import time

import jwt

from conftest import TEACHER_CODE, register


def test_health(client):
    assert client.get("/health").get_json() == {"status": "ok", "db": "ok", "version": "1.0.0"}


def test_register_and_login(client):
    headers, user = register(client, "Asel", "password1", "Асель")
    assert user["role"] == "student" and user["username"] == "Asel"
    r = client.post("/api/v1/auth/login", json={"username": "asel", "password": "password1"})   # case-insensitive login
    assert r.status_code == 200 and r.get_json()["user"]["id"] == user["id"]
    assert client.get("/api/v1/me", headers=headers).get_json()["user"]["display_name"] == "Асель"


def test_duplicate_username_is_rejected_case_insensitive(client):
    register(client, "nurlan")
    r = client.post("/api/v1/auth/register", json={"username": "NURLAN", "password": "password1", "display_name": "x"})
    assert r.status_code == 409 and r.get_json()["error"]["code"] == "username_taken"


def test_register_validation(client):
    bad = [
        {"username": "ab", "password": "password1", "display_name": "x"},             # too short username
        {"username": "кириллица", "password": "password1", "display_name": "x"},      # non-latin username
        {"username": "valid_name", "password": "short", "display_name": "x"},         # weak password
        {"username": "valid_name", "password": "password1", "display_name": "<b>x</b>"},  # markup in name
        {"username": "valid_name", "password": "password1", "display_name": "x", "role": "admin"},  # role escalation
    ]
    for b in bad:
        r = client.post("/api/v1/auth/register", json=b)
        assert r.status_code == 422, b
        assert r.get_json()["error"]["code"] == "validation_error"


def test_non_json_body(client):
    r = client.post("/api/v1/auth/login", data="hello", content_type="text/plain")
    assert r.status_code == 400


def test_wrong_password_and_unknown_user_look_the_same(client):
    register(client, "bolot")
    a = client.post("/api/v1/auth/login", json={"username": "bolot", "password": "nope-nope"})
    b = client.post("/api/v1/auth/login", json={"username": "nobody", "password": "nope-nope"})
    assert a.status_code == b.status_code == 401
    assert a.get_json() == b.get_json()


def test_passwords_are_hashed(app, client):
    register(client, "ermek", "password1")
    with app.app_context():
        from app.db import get_db
        stored = get_db().scalar("SELECT password_hash FROM users WHERE username = 'ermek'")
    assert stored.startswith("pbkdf2_sha256$") and "password1" not in stored


def test_protected_routes_need_valid_token(app, client):
    assert client.get("/api/v1/progress").status_code == 401
    assert client.get("/api/v1/progress", headers={"Authorization": "Bearer garbage"}).status_code == 401
    forged = jwt.encode({"sub": "1", "role": "teacher", "exp": int(time.time()) + 60}, "wrong-secret-key-0123456789abcdef-xyz", algorithm="HS256")
    assert client.get("/api/v1/progress", headers={"Authorization": "Bearer " + forged}).status_code == 401
    expired = jwt.encode({"sub": "1", "role": "student", "exp": int(time.time()) - 5}, "test-secret-key-0123456789abcdef-xyz", algorithm="HS256")
    r = client.get("/api/v1/progress", headers={"Authorization": "Bearer " + expired})
    assert r.status_code == 401 and r.get_json()["error"]["code"] == "token_expired"


def test_teacher_registration_requires_invite_code(client):
    r = client.post("/api/v1/auth/register", json={"username": "fake_teacher", "password": "password1", "display_name": "x", "role": "teacher"})
    assert r.status_code == 403
    r = client.post("/api/v1/auth/register", json={"username": "fake_teacher", "password": "password1", "display_name": "x",
                                                   "role": "teacher", "teacher_code": "WRONG"})
    assert r.status_code == 403
    _, user = register(client, "real_teacher", role="teacher", code=TEACHER_CODE)
    assert user["role"] == "teacher"


def test_login_rate_limit(app, client):
    app.config["RATE_AUTH_PER_MIN"] = 5
    codes = [client.post("/api/v1/auth/login", json={"username": "x", "password": "y"}).status_code for _ in range(7)]
    assert codes[:5] == [401] * 5 and codes[5:] == [429, 429]
