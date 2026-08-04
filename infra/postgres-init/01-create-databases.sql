-- Gitea uses its own role and database alongside the Skill Suite app DB
-- (which Postgres creates from POSTGRES_DB on first boot).
CREATE ROLE gitea WITH LOGIN PASSWORD 'gitea';
CREATE DATABASE gitea OWNER gitea;
GRANT ALL PRIVILEGES ON DATABASE gitea TO gitea;
