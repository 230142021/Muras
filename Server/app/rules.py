"""Game rules shared by the API: request schemas, save-merge policy and score validation."""
import re
from typing import Dict, List, Optional

from pydantic import BaseModel, Field, field_validator

USERNAME_RE = re.compile(r"^[A-Za-z0-9_.-]{3,32}$")

# ---------------------------------------------------------------------- request schemas


class RegisterIn(BaseModel):
    username: str
    password: str = Field(min_length=8, max_length=128)
    display_name: str = Field(min_length=1, max_length=48)
    role: str = "student"
    teacher_code: Optional[str] = None

    @field_validator("username")
    @classmethod
    def _username(cls, v):
        if not USERNAME_RE.match(v):
            raise ValueError("3-32 characters: latin letters, digits, _ . -")
        return v

    @field_validator("role")
    @classmethod
    def _role(cls, v):
        if v not in ("student", "teacher"):
            raise ValueError("role must be student or teacher")
        return v

    @field_validator("display_name")
    @classmethod
    def _display(cls, v):
        v = v.strip()
        if not v or "<" in v or ">" in v:
            raise ValueError("display name must not be empty or contain < >")
        return v


class LoginIn(BaseModel):
    username: str = Field(min_length=1, max_length=64)
    password: str = Field(min_length=1, max_length=128)


KEY_RE = re.compile(r"^[a-z0-9_]{1,40}$")


class SaveIn(BaseModel):
    base_version: int = Field(ge=0)
    xp: int = Field(ge=0, le=1_000_000)
    quests: List[str] = Field(default_factory=list, max_length=64)
    extra: Dict[str, int] = Field(default_factory=dict)
    client_info: Optional[str] = Field(default=None, max_length=120)

    @field_validator("quests")
    @classmethod
    def _quests(cls, v):
        for q in v:
            if not KEY_RE.match(q):
                raise ValueError("invalid quest id: %r" % q)
        return sorted(set(v))

    @field_validator("extra")
    @classmethod
    def _extra(cls, v):
        if len(v) > 2000:
            raise ValueError("too many keys (max 2000)")
        for k, val in v.items():
            if not KEY_RE.match(k):
                raise ValueError("invalid key: %r" % k)
            if not -10_000_000 <= val <= 10_000_000:
                raise ValueError("value out of range for %r" % k)
        return v


EVENT_TYPES = {"session_start", "session_end", "quest_completed", "word_unlocked", "achievement", "minigame_result", "level_up"}


class EventIn(BaseModel):
    id: str = Field(min_length=8, max_length=64)            # client-generated UUID (idempotency)
    type: str
    payload: dict = Field(default_factory=dict)
    time: Optional[str] = Field(default=None, max_length=32)

    @field_validator("type")
    @classmethod
    def _type(cls, v):
        if v not in EVENT_TYPES:
            raise ValueError("unknown event type")
        return v


class EventsIn(BaseModel):
    events: List[EventIn] = Field(min_length=1, max_length=100)


class ScoreIn(BaseModel):
    game: str
    score: int
    duration_ms: Optional[int] = Field(default=None, ge=0, le=3_600_000)


class ClassIn(BaseModel):
    name: str = Field(min_length=1, max_length=60)


class JoinIn(BaseModel):
    code: str = Field(min_length=4, max_length=12)


# ---------------------------------------------------------------------- save merge policy
# SaveData keys that may go DOWN during normal play: inventory is spent, daily tasks reset,
# the lantern is switched off. For these the newest write wins. Every other key is progress
# (words, achievements, quest stages, win counters, records) and can only grow, so concurrent
# saves from two devices are merged with max() — nothing the player earned is ever lost.
NON_MONOTONIC_PREFIXES = ("inv_", "daily_", "lantern_")


def is_monotonic(key: str) -> bool:
    return not key.startswith(NON_MONOTONIC_PREFIXES)


def merge_saves(server: dict, client: dict) -> dict:
    """Merge a stale client save into the newer server save (client wins for non-monotonic keys)."""
    extra = dict(server["extra"])
    for k, v in client["extra"].items():
        if k in extra and is_monotonic(k):
            extra[k] = max(extra[k], v)
        else:
            extra[k] = v
    return {
        "xp": max(server["xp"], client["xp"]),
        "quests": sorted(set(server["quests"]) | set(client["quests"])),
        "extra": extra,
    }


# ---------------------------------------------------------------------- mini-game scores
# order: "desc" = higher is better, "asc" = lower is better (times). max: plausibility limit (anti-cheat).
GAMES = {
    "ordo":    {"title": "Ордо",          "unit": "очков",  "order": "desc", "min": 0,     "max": 60},
    "kokboru": {"title": "Көк бөрү",      "unit": "голов",  "order": "desc", "min": 0,     "max": 20},
    "race":    {"title": "Ат чабыш",      "unit": "мс",     "order": "asc",  "min": 20000, "max": 900000},
    "komuz":   {"title": "Комуз",         "unit": "очков",  "order": "desc", "min": 0,     "max": 100000},
    "cooking": {"title": "Бешбармак",     "unit": "очков",  "order": "desc", "min": 0,     "max": 100},
    "fishing": {"title": "Балык уулоо",   "unit": "рыб",    "order": "desc", "min": 0,     "max": 500},
}


def validate_score(game: str, score: int):
    rule = GAMES.get(game)
    if rule is None:
        return "unknown game"
    if not rule["min"] <= score <= rule["max"]:
        return "score %d is outside the plausible range %d..%d" % (score, rule["min"], rule["max"])
    return None
