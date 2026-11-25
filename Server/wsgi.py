"""WSGI entry point: `gunicorn wsgi:app` (production) or `python wsgi.py` (local development)."""
import os

from app import create_app

app = create_app()

if __name__ == "__main__":
    app.run(host=os.environ.get("HOST", "127.0.0.1"), port=int(os.environ.get("PORT", "8000")), debug=False)
