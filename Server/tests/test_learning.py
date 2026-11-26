"""Knowledge test (pre/post), activity time, quest funnel and forgotten words on the teacher dashboard."""
import re
import uuid

from conftest import TEACHER_CODE, register
from test_privacy_dashboard import csrf, dash_login


def ev(t, **payload):
    return {"id": uuid.uuid4().hex, "type": t, "payload": payload, "time": "2026-11-26T10:00:00Z"}


def answers(correct):
    """10 answers, the first `correct` of them right."""
    return [{"q": "q%d" % i, "word": None, "ok": i <= correct} for i in range(1, 11)]


def make_class(client):
    register(client, "mugalim", "password1", "Мугалим", "teacher", TEACHER_CODE)
    assert dash_login(client).status_code == 302
    home = client.get("/teacher/").get_data(as_text=True)
    r = client.post("/teacher/classes", data={"csrf": csrf(home), "name": "7А"})
    class_id = int(r.headers["Location"].rstrip("/").split("/")[-1])
    page = client.get("/teacher/class/%d" % class_id).get_data(as_text=True)
    code = re.search(r'class="code">([A-Z0-9]{6})<', page).group(1)
    return class_id, code


def join(client, code, username, name):
    h, user = register(client, username, name=name)
    assert client.post("/api/v1/classes/join", headers=h, json={"code": code}).status_code == 200
    return h, user


def test_pre_post_gain_and_csv(client):
    class_id, code = make_class(client)
    a, _ = join(client, code, "aibek", "Айбек")
    b, _ = join(client, code, "aigul", "Айгүл")
    c, _ = join(client, code, "nurlan", "Нурлан")
    client.post("/api/v1/events", headers=a, json={"events": [ev("test_result", phase="pre", score=4, total=10, seconds=90, answers=answers(4)),
                                                              ev("test_result", phase="post", score=8, total=10, seconds=70, answers=answers(8))]})
    client.post("/api/v1/events", headers=b, json={"events": [ev("test_result", phase="pre", score=6, total=10, answers=answers(6)),
                                                              ev("test_result", phase="pre", score=10, total=10, answers=answers(10)),   # retake of pre is ignored
                                                              ev("test_result", phase="post", score=7, total=10, answers=answers(7))]})
    client.post("/api/v1/events", headers=c, json={"events": [ev("test_result", phase="pre", score=3, total=10, answers=answers(3))]})

    with client.application.app_context():
        from app.learning import test_summary
        from app.db import get_db
        ids = [r["id"] for r in get_db().all("SELECT id FROM users WHERE role = 'student' ORDER BY id")]
        t = test_summary(ids)
    assert t["pre_count"] == 3 and t["post_count"] == 2 and t["pairs"] == 2
    assert t["avg_pre_paired"] == 5.0 and t["avg_post_paired"] == 7.5
    assert t["avg_gain"] == 2.5 and t["improved_pct"] == 100.0
    q1 = next(q for q in t["questions"] if q["id"] == "q1")
    q5 = next(q for q in t["questions"] if q["id"] == "q5")
    assert q1["pre_pct"] == 100.0 and q5["pre_pct"] == round(100 / 3, 1) and q5["post_pct"] == 100.0

    page = client.get("/teacher/class/%d" % class_id).get_data(as_text=True)
    assert "Тест знаний: до и после игры" in page and "+2.50" in page
    csv_text = client.get("/teacher/class/%d/tests.csv" % class_id).get_data(as_text=True)
    lines = csv_text.strip().splitlines()
    assert lines[0].lstrip("﻿").startswith("display_name,username,pre_score,post_score,gain")
    row = next(l for l in lines if l.startswith("Айбек"))
    assert ",4,8,4," in row


def test_activity_quests_and_forgotten_words(client):
    class_id, code = make_class(client)
    a, _ = join(client, code, "aibek", "Айбек")
    b, _ = join(client, code, "aigul", "Айгүл")
    client.post("/api/v1/events", headers=a, json={"events": [
        ev("activity_time", activity="komuz", seconds=300), ev("activity_time", activity="komuz", seconds=120),
        ev("activity_time", activity="ordo", seconds=60), ev("activity_time", activity="komuz", seconds=999999),   # garbage ignored
        ev("quest_started", id="yurt"), ev("quest_step", id="yurt", step="Найди уук"), ev("quest_completed", id="yurt", xp=50),
        ev("quest_started", id="headphones"), ev("quest_step", id="headphones", step="Опроси свидетелей"),
        ev("word_review", id="tunduk", ok=False), ev("word_review", id="tunduk", ok=True), ev("word_review", id="salam", ok=True),
        ev("tutorial_step", step="move", n=1), ev("tutorial_done", skipped=False, seconds=80)]})
    client.post("/api/v1/events", headers=b, json={"events": [
        ev("activity_time", activity="komuz", seconds=180),
        ev("quest_started", id="yurt"), ev("quest_step", id="yurt", step="Найди уук"),
        ev("word_review", id="tunduk", ok=False), ev("tutorial_done", skipped=True, seconds=5)]})

    with client.application.app_context():
        from app.learning import class_learning
        from app.db import get_db
        ids = [r["id"] for r in get_db().all("SELECT id FROM users WHERE role = 'student' ORDER BY id")]
        l = class_learning(ids)
    komuz = l["activity"][0]
    assert komuz["id"] == "komuz" and komuz["minutes"] == 10.0 and komuz["players"] == 2 and komuz["avg_minutes"] == 5.0
    yurt = next(q for q in l["quests"] if q["id"] == "yurt")
    assert (yurt["started"], yurt["completed"], yurt["abandoned"], yurt["stuck_at"]) == (2, 1, 1, "Найди уук")
    hp = next(q for q in l["quests"] if q["id"] == "headphones")
    assert hp["abandon_pct"] == 100.0 and hp["stuck_at"] == "Опроси свидетелей"
    assert l["forgotten"][0]["id"] == "tunduk" and l["forgotten"][0]["wrong"] == 2 and l["forgotten"][0]["tries"] == 3
    assert all(w["id"] != "salam" for w in l["forgotten"])                       # never answered wrong
    assert l["tutorial"] == {"started": 2, "finished": 1, "skipped": 1}

    page = client.get("/teacher/class/%d" % class_id).get_data(as_text=True)
    assert "Где бросают задания" in page and "Опроси свидетелей" in page and "Комуз" in page


def test_new_event_types_accepted_and_unknown_rejected(client, student):
    h, _ = student
    good = [ev(t) for t in ("test_result", "quest_started", "quest_step", "activity_time", "word_review", "word_heard", "tutorial_step", "tutorial_done")]
    assert client.post("/api/v1/events", headers=h, json={"events": good}).get_json()["accepted"] == 8
    assert client.post("/api/v1/events", headers=h, json={"events": [ev("cheat_xp")]}).status_code == 422


def test_teacher_sees_only_own_class_tests(client):
    class_id, _ = make_class(client)
    other = client.application.test_client()
    assert other.get("/teacher/class/%d/tests.csv" % class_id).status_code in (302, 303)   # not logged in → login page
