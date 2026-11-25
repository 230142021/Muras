# Развёртывание MURAS Server

## Вариант 1. Render.com (бесплатно, публичный HTTPS-адрес для комиссии)

1. Репозиторий на GitHub должен содержать папку `Server/` и файл `render.yaml` в корне.
2. На <https://render.com> → **New → Blueprint** → выбрать репозиторий. Render создаст:
   * веб-сервис `muras-api` (Python, gunicorn) с HTTPS-адресом вида `https://muras-api.onrender.com`;
   * базу PostgreSQL `muras-db`; `DATABASE_URL` подставится автоматически;
   * случайный `SECRET_KEY`.
3. В настройках сервиса задать `TEACHER_INVITE_CODE`.
4. Проверить `https://<адрес>/health` → `{"status":"ok","db":"ok"}`.
5. В игре: Пауза → Аккаунт → поле «Сервер» → `https://<адрес>`. Чтобы адрес стоял по умолчанию в
   сборке, положить его в `Assets/Game/Resources/MURAS/server_url.txt`.

Таблицы создаются при первом старте (миграции применяются автоматически).
Бесплатный сервис засыпает после 15 минут без запросов — первый запрос после паузы идёт ~30 с.

## Вариант 2. Любой сервер с Docker

```bash
git clone <repo> && cd <repo>/Server
cp .env.example .env          # SECRET_KEY, TEACHER_INVITE_CODE, POSTGRES_PASSWORD
docker compose up -d --build
curl http://localhost:8000/health
```

HTTPS ставится обратным прокси (Caddy/nginx) перед контейнером; в `.env` тогда `TRUST_PROXY=1`.

## Вариант 3. Локально для демонстрации

См. README → «Быстрый старт». В Unity один раз выполнить **Tools → Diploma → Cloud: Allow HTTP (local server)**,
так как Unity по умолчанию запрещает незащищённые http-запросы.

## Эксплуатация

| Задача | Как |
|---|---|
| Проверка живости | `GET /health` (используется Render и `HEALTHCHECK` в Dockerfile) |
| Логи | stdout: строка на запрос — метод, путь, статус, время, id пользователя, request id (`X-Request-ID`) |
| Миграции | автоматически при старте, или `flask --app wsgi migrate`; новые изменения схемы — новый файл `migrations/<dialect>/00N_*.sql` |
| Резервная копия | PostgreSQL: `pg_dump "$DATABASE_URL" > backup.sql`; восстановление `psql "$DATABASE_URL" < backup.sql`. SQLite: копия файла `instance/muras.db` |
| Откат | Render: «Rollback» к предыдущему деплою; Docker: `git checkout <tag> && docker compose up -d --build` |
| Секреты | только в переменных окружения / `.env` (в `.gitignore`); `.env.example` — без значений |
