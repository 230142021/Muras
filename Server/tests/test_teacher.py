from conftest import register


def make_class(client, th, name="7Б"):
    r = client.post("/api/v1/teacher/classes", headers=th, json={"name": name})
    assert r.status_code == 201
    return r.get_json()


def test_student_cannot_use_teacher_api(client, student):
    h, _ = student
    r = client.get("/api/v1/teacher/classes", headers=h)
    assert r.status_code == 403 and r.get_json()["error"]["code"] == "forbidden"
    assert client.post("/api/v1/teacher/classes", headers=h, json={"name": "x"}).status_code == 403


def test_class_flow_and_analytics(client, teacher):
    th, _ = teacher
    cls = make_class(client, th)
    assert len(cls["join_code"]) == 6
    s1, _ = register(client, "asan", name="Асан")
    s2, _ = register(client, "aiza", name="Айза")
    for h in (s1, s2):
        assert client.post("/api/v1/classes/join", headers=h, json={"code": cls["join_code"].lower()}).status_code == 200
    client.post("/api/v1/classes/join", headers=s1, json={"code": cls["join_code"]})        # joining twice is harmless
    client.put("/api/v1/progress", headers=s1, json={"base_version": 0, "xp": 100, "quests": ["yurt"], "extra": {"w_salam": 1, "w_rahmat": 1}})
    client.put("/api/v1/progress", headers=s2, json={"base_version": 0, "xp": 40, "quests": [], "extra": {"w_salam": 1}})
    ov = client.get("/api/v1/teacher/classes/%d" % cls["id"], headers=th).get_json()
    assert sorted(s["words"] for s in ov["students"]) == [1, 2]
    a = client.get("/api/v1/teacher/classes/%d/analytics" % cls["id"], headers=th).get_json()
    assert a["students"] == 2 and a["avg_xp"] == 70.0 and a["avg_words"] == 1.5
    salam = next(w for w in a["words"] if w["id"] == "salam")
    rahmat = next(w for w in a["words"] if w["id"] == "rahmat")
    assert salam["pct"] == 100.0 and rahmat["pct"] == 50.0
    assert all(w["pct"] <= rahmat["pct"] for w in a["hardest_words"])
    yurt = next(q for q in a["quests"] if q["id"] == "yurt")
    assert yurt["pct"] == 50.0
    assert len(a["activity"]) == 14


def test_teacher_cannot_see_other_teachers_class(client, teacher):
    th, _ = teacher
    cls = make_class(client, th)
    other, _ = register(client, "other_t", role="teacher", code="TEACH-2027")
    for path in ("/api/v1/teacher/classes/%d", "/api/v1/teacher/classes/%d/analytics"):
        r = client.get(path % cls["id"], headers=other)
        assert r.status_code == 404
    assert client.get("/api/v1/teacher/classes", headers=other).get_json()["classes"] == []


def test_join_errors(client, teacher, student):
    th, _ = teacher
    h, _ = student
    assert client.post("/api/v1/classes/join", headers=h, json={"code": "ZZZZZZ"}).status_code == 404
    cls = make_class(client, th)
    assert client.post("/api/v1/classes/join", headers=th, json={"code": cls["join_code"]}).status_code == 403   # teachers don't join


def test_student_detail_and_removal(client, teacher):
    th, _ = teacher
    cls = make_class(client, th)
    h, user = register(client, "dilnaz", name="Дилназ")
    client.post("/api/v1/classes/join", headers=h, json={"code": cls["join_code"]})
    client.put("/api/v1/progress", headers=h, json={"base_version": 0, "xp": 10, "quests": [], "extra": {"w_salam": 1, "a_word1": 1}})
    d = client.get("/api/v1/teacher/classes/%d/students/%d" % (cls["id"], user["id"]), headers=th).get_json()
    assert [w["id"] for w in d["known_words"]] == ["salam"] and d["achievements"] == 1
    assert client.delete("/api/v1/teacher/classes/%d/students/%d" % (cls["id"], user["id"]), headers=th).status_code == 204
    assert client.get("/api/v1/teacher/classes/%d/students/%d" % (cls["id"], user["id"]), headers=th).status_code == 404
