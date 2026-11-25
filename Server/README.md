# MURAS Server

Backend игры **MURAS**: аккаунты учеников и учителей, облачное сохранение, журнал игровых событий,
таблицы рекордов мини-игр и веб-панель учителя с аналитикой класса.

| | |
|---|---|
| Язык / фреймворк | Python 3.10+, Flask 3 |
| База данных | SQLite (локально) · PostgreSQL 16 (развёртывание) |
| Аутентификация | JWT (HS256) для игры · сессия + CSRF для панели учителя |
| Пароли | PBKDF2-SHA256, 240 000 итераций, соль 16 байт |
| Документация API | [`docs/openapi.yaml`](docs/openapi.yaml) (OpenAPI 3.0), также по адресу `/api/v1/openapi.yaml` |
| Тесты | pytest, 35 тестов (`tests/`) |

Подробнее: [архитектура](docs/ARCHITECTURE.md) · [развёртывание](docs/DEPLOYMENT.md).

## Быстрый старт (локально, без Docker)

```bash
cd Server
python3 -m venv .venv
source .venv/bin/activate            # Windows: .venv\Scripts\activate
pip install -r requirements-dev.txt
export TEACHER_INVITE_CODE=teach2027 # код, по которому регистрируются учителя
python wsgi.py                       # http://127.0.0.1:8000
```

База `instance/muras.db` и таблицы создаются автоматически (миграции применяются при старте).

* Проверка: <http://127.0.0.1:8000/health> → `{"status": "ok", "db": "ok"}`
* Панель учителя: <http://127.0.0.1:8000/teacher/> → «Регистрация по коду приглашения»
* В игре: Пауза → **Аккаунт** → сервер `http://127.0.0.1:8000` → Регистрация

## Тесты

```bash
pip install -r requirements-dev.txt
python -m pytest -v
```

Тесты поднимают приложение на временной SQLite-базе и проверяют: регистрацию и вход, валидацию,
подделанные и просроченные токены, ограничение частоты запросов, слияние сохранений с двух устройств,
идемпотентность событий, анти-чит для рекордов, права учителя (чужой класс → 404, ученик → 403),
CSRF в панели, защиту CSV от формул, экспорт и удаление аккаунта.

## Запуск как в продакшене (Docker + PostgreSQL)

```bash
cp .env.example .env    # заполнить SECRET_KEY, TEACHER_INVITE_CODE, POSTGRES_PASSWORD
docker compose up --build
```

## Переменные окружения

| Переменная | По умолчанию | Назначение |
|---|---|---|
| `DATABASE_URL` | `sqlite:///instance/muras.db` | `sqlite:///…` или `postgresql://user:pass@host:5432/db` |
| `SECRET_KEY` | случайный (только dev) | подпись JWT и cookie; **обязателен** при `MURAS_ENV=production` |
| `MURAS_ENV` | `development` | `production` включает Secure-cookie и требует SECRET_KEY |
| `TEACHER_INVITE_CODE` | пусто (регистрация учителей выключена) | код приглашения для учителей |
| `TRUST_PROXY` | `0` | `1` за обратным прокси (Render, nginx) — брать IP из `X-Forwarded-For` |
| `JWT_TTL_HOURS` | `336` | срок жизни токена (14 дней) |
| `RATE_AUTH_PER_MIN` / `RATE_WRITE_PER_MIN` | `10` / `120` | лимиты запросов в минуту с одного IP |
| `AUTO_MIGRATE` | `1` | применять миграции при старте (`flask --app wsgi migrate` — вручную) |

## Структура

```
Server/
├── app/
│   ├── __init__.py      фабрика приложения, логирование, обработка ошибок, /health
│   ├── config.py        настройки из переменных окружения
│   ├── db.py            слой БД (SQLite/PostgreSQL), миграции
│   ├── security.py      пароли, JWT, роли, лимиты запросов, журнал аудита
│   ├── rules.py         схемы запросов (pydantic), правила слияния сохранений, анти-чит
│   ├── analytics.py     аналитика класса для учителя
│   ├── routes/          api.py (игра) · teacher_api.py (JSON для учителя) · dashboard.py (HTML-панель)
│   ├── templates/       страницы панели учителя
│   └── static/          стили панели
├── migrations/          версионированные SQL-миграции (sqlite/, postgres/)
├── data/catalog.json    слова, достижения и задания (выгружены из игры)
├── tests/               pytest
├── docs/                openapi.yaml, ARCHITECTURE.md, DEPLOYMENT.md
├── Dockerfile, docker-compose.yml, .env.example
└── wsgi.py              точка входа
```
