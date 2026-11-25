import re

from conftest import register


def test_export_and_delete_account(client, student):
    h, user = student
    client.put("/api/v1/progress", headers=h, json={"base_version": 0, "xp": 5, "quests": [], "extra": {"w_salam": 1}})
    client.post("/api/v1/scores", headers=h, json={"game": "ordo", "score": 7})
    data = client.get("/api/v1/me/export", headers=h).get_json()
    assert data["save"]["extra"] == {"w_salam": 1} and data["scores"][0]["score"] == 7
    assert client.delete("/api/v1/me", headers=h, json={"password": "wrong"}).status_code == 403
    assert client.delete("/api/v1/me", headers=h, json={"password": "password1"}).status_code == 204
    assert client.post("/api/v1/auth/login", json={"username": "aibek", "password": "password1"}).status_code == 401
    assert client.get("/api/v1/progress", headers=h).status_code == 401


def csrf(page_html):
    return re.search(r'name="csrf" value="([^"]+)"', page_html).group(1)


def dash_login(client, username="mugalim", password="password1"):
    page = client.get("/teacher/login").get_data(as_text=True)
    return client.post("/teacher/login", data={"csrf": csrf(page), "username": username, "password": password})


def test_dashboard_requires_login(client):
    r = client.get("/teacher/")
    assert r.status_code == 302 and "/teacher/login" in r.headers["Location"]


def test_dashboard_csrf_protection(client, teacher):
    r = client.post("/teacher/login", data={"username": "mugalim", "password": "password1"})
    assert r.status_code == 400


def test_dashboard_student_cannot_log_in(client, student):
    r = dash_login(client, "aibek", "password1")
    assert r.status_code == 200 and "Неверный логин" in r.get_data(as_text=True)


def test_dashboard_full_flow(client, teacher):
    assert dash_login(client).status_code == 302
    home = client.get("/teacher/").get_data(as_text=True)
    r = client.post("/teacher/classes", data={"csrf": csrf(home), "name": "8А"})
    assert r.status_code == 302
    class_url = r.headers["Location"]
    page = client.get(class_url).get_data(as_text=True)
    code = re.search(r'class="code">([A-Z0-9]{6})<', page).group(1)
    h, _ = register(client, "eq_student", name="=HYPERLINK(1)")       # name starting with '=' (CSV injection attempt)
    client.post("/api/v1/classes/join", headers=h, json={"code": code})
    page = client.get(class_url).get_data(as_text=True)
    assert "=HYPERLINK(1)" in page
    csv_text = client.get(class_url + "/export.csv").get_data(as_text=True)
    assert "'=HYPERLINK(1)" in csv_text


def test_dashboard_other_teachers_class_is_404(client, teacher):
    th, _ = teacher
    cid = client.post("/api/v1/teacher/classes", headers=th, json={"name": "secret"}).get_json()["id"]
    register(client, "intruder", role="teacher", code="TEACH-2027")
    dash_login(client, "intruder", "password1")
    assert client.get("/teacher/class/%d" % cid).status_code == 404


def test_security_headers(client):
    r = client.get("/teacher/login")
    assert r.headers["X-Frame-Options"] == "DENY" and "Content-Security-Policy" in r.headers
    assert r.headers["X-Content-Type-Options"] == "nosniff"
