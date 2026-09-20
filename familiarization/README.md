# Familiarization

A deliberately tiny HTTP service you use before the real session to prove the competition
infrastructure works for you: your Git repository, the CI build, the container registry, a
container the platform starts under your own name, and your MSSQL database.

It answers two routes:

* `GET /` returns `{"competitor":"<your username>","service":"familiarization-api","time":"<now>"}`
* `GET /healthz` returns `{"status":"ok"}`

It needs one environment variable, `COMPETITOR_NAME`, and refuses to start without it.

## Run it locally

```
cp .env.example .env
docker compose up --build
curl http://localhost:8080/
```

Without docker: `COMPETITOR_NAME=alice npm start`. Node 22 or newer, no dependencies to install.

## Run the SQL script

`sql/familiarization.sql` creates `dbo.FamiliarizationCheck`, seeds two rows the first time, and
selects them back. It is idempotent, so running it twice is harmless. Run it against your own
session database (`{session}-{your username}`), not against `master`.

```
sqlcmd -S mssql.<domain>,1433 -U <your login> -d <your database> -C -i sql/familiarization.sql
```

Azure Data Studio works just as well; connect to `mssql.<domain>,1433` with
`TrustServerCertificate=True` (that is what `-C` does for sqlcmd), open the script and run it.

## How the platform runs it

Nothing starts automatically. Push to `main`, let CI publish the image, then in the Skill Suite UI
pull it under Docker images and add it to your session as a docker service with the environment
entry:

```
COMPETITOR_NAME={{competitor.username}}
```

The platform substitutes your user name per competitor when it starts the container, so `GET /`
should come back with your own name in it.

## CI

`.gitea/workflows/publish-image.yml` builds `Dockerfile` and pushes it to the Gitea container
registry on every push to `main`, on `v*` tags, and on demand.

Required secrets: `REGISTRY_USERNAME`, `REGISTRY_TOKEN` (a personal access token with package read
and write). Optional variables: `REGISTRY_HOST`, `IMAGE_PLATFORMS`, `BUILDX_NETWORK`.

This folder lives inside the platform repository, so it becomes a Gitea repository of its own through
a subtree split. From the platform repository root:

```
git subtree split --prefix=familiarization -b familiarization-split
git clone --branch familiarization-split . ../familiarization
git -C ../familiarization branch -m main
scripts/gitea-push.sh --url http://localhost:3000 --token "$GITEA_TOKEN" --path ../familiarization --name familiarization
```

`gitea-push.sh` creates the repository, pushes `main`, and stores the two registry secrets on it.
Add `--owner <organisation>` to publish under an organisation instead of the token's user. Repeat the
split and a fast-forward push of the clone whenever this folder changes.
