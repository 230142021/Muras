import uuid

from conftest import register


def ev(t="word_unlocked", **payload):
    return {"id": uuid.uuid4().hex, "type": t, "payload": payload, "time": "2026-11-25T10:00:00Z"}


def test_events_batch_and_idempotency(client, student):
    h, _ = student
    batch = [ev(id="salam"), ev(id="rahmat"), ev("quest_completed", id="yurt", xp=50)]
    r = client.post("/api/v1/events", headers=h, json={"events": batch})
    assert r.get_json() == {"accepted": 3, "duplicates": 0}
    r = client.post("/api/v1/events", headers=h, json={"events": batch + [ev(id="kosh")]})   # retry after a lost response
    assert r.get_json() == {"accepted": 1, "duplicates": 3}


def test_events_validation(client, student):
    h, _ = student
    assert client.post("/api/v1/events", headers=h, json={"events": []}).status_code == 422
    assert client.post("/api/v1/events", headers=h, json={"events": [ev("hack_the_planet")]}).status_code == 422
    assert client.post("/api/v1/events", headers=h, json={"events": [ev() for _ in range(101)]}).status_code == 422


def test_score_validation_and_audit(app, client, student):
    h, _ = student
    assert client.post("/api/v1/scores", headers=h, json={"game": "ordo", "score": 12}).status_code == 201
    r = client.post("/api/v1/scores", headers=h, json={"game": "ordo", "score": 9999})
    assert r.status_code == 422 and r.get_json()["error"]["code"] == "score_rejected"
    assert client.post("/api/v1/scores", headers=h, json={"game": "tetris", "score": 1}).status_code == 422
    with app.app_context():
        from app.db import get_db
        assert get_db().scalar("SELECT COUNT(*) AS n FROM audit_log WHERE action = 'score_rejected'") == 2


def test_leaderboard_order_desc_and_asc(client):
    players = [register(client, "player%d" % i, name="Игрок %d" % i)[0] for i in range(3)]
    for h, ordo, race in zip(players, (10, 25, 18), (64000, 58000, 71000)):
        client.post("/api/v1/scores", headers=h, json={"game": "ordo", "score": ordo})
        client.post("/api/v1/scores", headers=h, json={"game": "ordo", "score": ordo - 5})        # worse attempt ignored
        client.post("/api/v1/scores", headers=h, json={"game": "race", "score": race})
    o = client.get("/api/v1/leaderboard/ordo", headers=players[0]).get_json()
    assert [r["best"] for r in o["top"]] == [25, 18, 10]
    assert o["me"]["rank"] == 3
    rc = client.get("/api/v1/leaderboard/race", headers=players[0]).get_json()
    assert [r["best"] for r in rc["top"]] == [58000, 64000, 71000]                                # lower time is better
    assert client.get("/api/v1/leaderboard/tetris", headers=players[0]).status_code == 404
    assert client.get("/api/v1/leaderboard/ordo?scope=world", headers=players[0]).status_code == 400
    assert client.get("/api/v1/leaderboard/ordo?limit=abc", headers=players[0]).status_code == 400


def test_personal_best(client, student):
    h, _ = student
    a = client.post("/api/v1/scores", headers=h, json={"game": "kokboru", "score": 3}).get_json()
    b = client.post("/api/v1/scores", headers=h, json={"game": "kokboru", "score": 1}).get_json()
    assert a["is_record"] and not b["is_record"] and b["personal_best"] == 3
