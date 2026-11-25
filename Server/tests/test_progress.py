from conftest import register
from app.rules import merge_saves


def put(client, h, **kw):
    body = {"base_version": 0, "xp": 0, "quests": [], "extra": {}}
    body.update(kw)
    return client.put("/api/v1/progress", headers=h, json=body)


def test_new_player_has_empty_save(client, student):
    h, _ = student
    s = client.get("/api/v1/progress", headers=h).get_json()
    assert (s["version"], s["xp"], s["quests"], s["extra"]) == (0, 0, [], {})


def test_fast_forward_save(client, student):
    h, _ = student
    r = put(client, h, base_version=0, xp=120, quests=["yurt", "greeting", "yurt"], extra={"w_salam": 1})
    assert r.status_code == 200 and r.get_json()["merged"] is False and r.get_json()["version"] == 1
    s = client.get("/api/v1/progress", headers=h).get_json()
    assert s["xp"] == 120 and s["quests"] == ["greeting", "yurt"] and s["extra"] == {"w_salam": 1}


def test_concurrent_saves_are_merged(client, student):
    h, _ = student
    put(client, h, base_version=0, xp=200, quests=["yurt"], extra={"w_salam": 1, "race_wins": 3, "inv_alma": 5, "hq": 2})
    # second device still thinks the server is at version 0
    r = put(client, h, base_version=0, xp=90, quests=["chuko"], extra={"w_rahmat": 1, "race_wins": 1, "inv_alma": 2, "hq": 4})
    body = r.get_json()
    assert body["merged"] is True and body["version"] == 2
    assert body["xp"] == 200                                   # progress never goes down
    assert body["quests"] == ["chuko", "yurt"]                 # union
    assert body["extra"]["w_salam"] == 1 and body["extra"]["w_rahmat"] == 1
    assert body["extra"]["race_wins"] == 3                     # monotonic counter: max
    assert body["extra"]["hq"] == 4                            # quest stage: max
    assert body["extra"]["inv_alma"] == 2                      # inventory: newest write wins


def test_merge_rules_unit():
    server = {"xp": 10, "quests": ["a"], "extra": {"w_x": 1, "daily_p0": 3, "lantern_on": 1}}
    client = {"xp": 5, "quests": ["b"], "extra": {"daily_p0": 0, "lantern_on": 0, "fish": 2}}
    m = merge_saves(server, client)
    assert m == {"xp": 10, "quests": ["a", "b"], "extra": {"w_x": 1, "daily_p0": 0, "lantern_on": 0, "fish": 2}}


def test_version_ahead_is_rejected(client, student):
    h, _ = student
    r = put(client, h, base_version=7)
    assert r.status_code == 409 and r.get_json()["error"]["code"] == "version_ahead"


def test_save_validation(client, student):
    h, _ = student
    assert put(client, h, xp=-1).status_code == 422
    assert put(client, h, extra={"Bad Key!": 1}).status_code == 422
    assert put(client, h, extra={"ok": 10**9}).status_code == 422
    assert put(client, h, quests=["DROP TABLE"]).status_code == 422
    assert put(client, h, extra={("k%d" % i): 1 for i in range(2001)}).status_code == 422


def test_saves_are_isolated_between_players(client, student):
    h1, _ = student
    h2, _ = register(client, "second")
    put(client, h1, xp=500, extra={"w_salam": 1})
    s2 = client.get("/api/v1/progress", headers=h2).get_json()
    assert s2["xp"] == 0 and s2["extra"] == {}


def test_body_size_limit(client, student):
    h, _ = student
    r = client.put("/api/v1/progress", headers=dict(h, **{"Content-Type": "application/json"}), data="x" * (300 * 1024))
    assert r.status_code == 413
