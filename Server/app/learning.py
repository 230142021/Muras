"""Learning research and gameplay analytics built from the events table.

* Knowledge test (pre/post design): every player takes the same 10-question test before playing
  and again after the main quests. The gain (post - pre) is the main evidence that the game teaches.
* Activity time: how many minutes players spend in each mini-game / window.
* Quest funnel: quests started vs completed and the last step reached by those who gave up.
* Forgotten words: words answered wrongly in the dictionary self-check or in the test.

All functions take a list of user ids (a class) so teachers only ever see their own students.
"""
from collections import Counter, defaultdict

from .analytics import catalog
from .db import get_db, loads


def _events(user_ids, types):
    if not user_ids:
        return []
    marks = ",".join("?" * len(user_ids))
    tmarks = ",".join("?" * len(types))
    rows = get_db().all(
        "SELECT user_id, type, payload, created_at FROM events WHERE user_id IN (%s) AND type IN (%s) ORDER BY id" % (marks, tmarks),
        list(user_ids) + list(types))
    for r in rows:
        r["payload"] = loads(r["payload"], {}) or {}
    return rows


def _pct(part, whole):
    return round(100.0 * part / whole, 1) if whole else 0.0


def _num(v, default=0):
    try:
        return int(v)
    except (TypeError, ValueError):
        return default


# ---------------------------------------------------------------------- knowledge test
def test_results(user_ids) -> dict:
    """First pre-test and latest post-test per student (a retaken post-test replaces the old one)."""
    per_user = defaultdict(dict)
    for e in _events(user_ids, ["test_result"]):
        p = e["payload"]
        phase = p.get("phase")
        if phase not in ("pre", "post"):
            continue
        total = max(1, _num(p.get("total"), 10))
        entry = {"score": max(0, min(total, _num(p.get("score")))), "total": total, "seconds": _num(p.get("seconds")),
                 "answers": {a.get("q"): bool(a.get("ok")) for a in (p.get("answers") or []) if isinstance(a, dict) and a.get("q")},
                 "at": e["created_at"]}
        if phase == "pre" and "pre" in per_user[e["user_id"]]:
            continue
        per_user[e["user_id"]][phase] = entry
    return per_user


def test_summary(user_ids) -> dict:
    per_user = test_results(user_ids)
    questions = catalog().get("knowledge_test", [])
    pairs = [(u, r["pre"], r["post"]) for u, r in per_user.items() if "pre" in r and "post" in r]
    pre_all = [r["pre"] for r in per_user.values() if "pre" in r]
    post_all = [r["post"] for r in per_user.values() if "post" in r]

    def avg(items, key=lambda x: x["score"]):
        return round(sum(key(i) for i in items) / len(items), 2) if items else 0.0

    per_q = []
    for q in questions:
        pre_n = sum(1 for r in pre_all if q["id"] in r["answers"])
        post_n = sum(1 for r in post_all if q["id"] in r["answers"])
        per_q.append({"id": q["id"], "word": q.get("word"), "text": q.get("ru", q["id"]),
                      "pre_pct": _pct(sum(1 for r in pre_all if r["answers"].get(q["id"])), pre_n),
                      "post_pct": _pct(sum(1 for r in post_all if r["answers"].get(q["id"])), post_n)})
    gains = [post["score"] - pre["score"] for _, pre, post in pairs]
    return {
        "pre_count": len(pre_all), "post_count": len(post_all), "pairs": len(pairs),
        "avg_pre": avg(pre_all), "avg_post": avg(post_all),
        "avg_pre_paired": avg([p for _, p, _ in pairs]), "avg_post_paired": avg([p for _, _, p in pairs]),
        "avg_gain": round(sum(gains) / len(gains), 2) if gains else 0.0,
        "improved_pct": _pct(sum(1 for g in gains if g > 0), len(gains)),
        "total": questions and len(questions) or 10,
        "questions": per_q,
        "students": {u: {"pre": r.get("pre", {}).get("score"), "post": r.get("post", {}).get("score"),
                         "gain": (r["post"]["score"] - r["pre"]["score"]) if "pre" in r and "post" in r else None,
                         "pre_seconds": r.get("pre", {}).get("seconds"), "post_seconds": r.get("post", {}).get("seconds")}
                     for u, r in per_user.items()},
    }


# ---------------------------------------------------------------------- activity time
def activity_summary(user_ids) -> list:
    names = catalog().get("activities", {})
    seconds, players = Counter(), defaultdict(set)
    for e in _events(user_ids, ["activity_time"]):
        a = str(e["payload"].get("activity", ""))[:32]
        s = _num(e["payload"].get("seconds"))
        if not a or s <= 0 or s > 6 * 3600:            # ignore garbage (clock jumps, > 6 hours in one piece)
            continue
        seconds[a] += s
        players[a].add(e["user_id"])
    total = sum(seconds.values())
    rows = [{"id": a, "name": names.get(a, a), "minutes": round(s / 60.0, 1), "players": len(players[a]),
             "avg_minutes": round(s / 60.0 / len(players[a]), 1), "share": _pct(s, total)}
            for a, s in seconds.items()]
    return sorted(rows, key=lambda r: -r["minutes"])


# ---------------------------------------------------------------------- quest funnel
def quest_funnel(user_ids) -> list:
    names = catalog().get("quest_names", {})
    started, done = defaultdict(set), defaultdict(set)
    last_step = {}
    for e in _events(user_ids, ["quest_started", "quest_step", "quest_completed"]):
        qid = str(e["payload"].get("id", ""))[:40]
        if not qid:
            continue
        if e["type"] == "quest_completed":
            done[qid].add(e["user_id"])
            started[qid].add(e["user_id"])
        else:
            started[qid].add(e["user_id"])
            if e["type"] == "quest_step" and e["payload"].get("step"):
                last_step[(qid, e["user_id"])] = str(e["payload"]["step"])[:90]
    rows = []
    for qid, users in started.items():
        left = users - done[qid]
        stuck = Counter(last_step[(qid, u)] for u in left if (qid, u) in last_step).most_common(1)
        rows.append({"id": qid, "name": names.get(qid, qid), "started": len(users), "completed": len(done[qid]),
                     "abandoned": len(left), "abandon_pct": _pct(len(left), len(users)),
                     "stuck_at": stuck[0][0] if stuck else None})
    return sorted(rows, key=lambda r: (-r["abandon_pct"], r["name"]))


# ---------------------------------------------------------------------- forgotten words
def forgotten_words(user_ids, limit=10) -> list:
    words = {w["id"]: w for w in catalog()["words"]}
    q_word = {q["id"]: q.get("word") for q in catalog().get("knowledge_test", [])}
    tries, wrong = Counter(), Counter()
    for e in _events(user_ids, ["word_review", "test_result"]):
        p = e["payload"]
        if e["type"] == "word_review":
            wid = p.get("id")
            if wid in words:
                tries[wid] += 1
                wrong[wid] += 0 if p.get("ok") else 1
        else:
            for a in p.get("answers") or []:
                wid = q_word.get(a.get("q")) if isinstance(a, dict) else None
                if wid in words:
                    tries[wid] += 1
                    wrong[wid] += 0 if a.get("ok") else 1
    rows = [{"id": w, "ky": words[w]["ky"], "ru": words[w]["ru"], "tries": tries[w], "wrong": wrong[w], "wrong_pct": _pct(wrong[w], tries[w])}
            for w in tries if wrong[w] > 0]
    return sorted(rows, key=lambda r: (-r["wrong_pct"], -r["tries"], r["id"]))[:limit]


# ---------------------------------------------------------------------- tutorial
def tutorial_summary(user_ids) -> dict:
    steps, finished, skipped = {}, set(), set()
    for e in _events(user_ids, ["tutorial_step", "tutorial_done"]):
        if e["type"] == "tutorial_done":
            (skipped if e["payload"].get("skipped") else finished).add(e["user_id"])
        else:
            steps[e["user_id"]] = max(steps.get(e["user_id"], 0), _num(e["payload"].get("n")))
    return {"started": len(set(steps) | finished | skipped), "finished": len(finished), "skipped": len(skipped)}


def class_learning(user_ids) -> dict:
    return {"tests": test_summary(user_ids), "activity": activity_summary(user_ids),
            "quests": quest_funnel(user_ids), "forgotten": forgotten_words(user_ids), "tutorial": tutorial_summary(user_ids)}
