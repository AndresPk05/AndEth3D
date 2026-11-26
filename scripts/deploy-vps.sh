#!/usr/bin/env bash
set -euo pipefail
umask 077

repo=/var/www/andeth3d
api=$repo/backend/publish
web=$repo/frontend/dist
service=andeth3d.service
app_user=andeth3d
postgres=andeth3d-postgres
domain=https://andeth3d.online
catalog=/api/manufacturing/catalog

if [[ $EUID != 0 ]]; then
  echo "Ejecuta este script como root." >&2
  exit 1
fi

as_app() { sudo -u "$app_user" -H "$@"; }

exec 9>/run/lock/andeth3d-deploy.lock
flock -n 9 || { echo "Ya hay un despliegue en curso." >&2; exit 1; }
test -s "$api/appsettings.Production.json"
test -s "$web/index.html"
systemctl is-active --quiet "$service"
command -v rsync >/dev/null
node_dir=$(cat /root/andeth3d-node-path)
test -x "$node_dir/bin/node"

build=$(mktemp -d /var/tmp/andeth3d-build.XXXXXX)
backup=
api_stopped=0
api_changed=0
web_changed=0
api_excludes=(
  --exclude=/appsettings.Production.json
  --exclude=/App_Data/
  --exclude=/wwwroot/uploads/
  --exclude=/publish/
)

cleanup() {
  local status=$?
  trap - EXIT
  set +e
  if (( status != 0 )); then
    echo "Despliegue interrumpido." >&2
    if (( web_changed )); then
      rsync -a --delete "$backup/web/" "$web/" ||
        echo "Error al recuperar el frontend." >&2
    fi
    if (( api_changed )); then
      if ! systemctl stop "$service" ||
        ! rsync -a --delete "${api_excludes[@]}" "$backup/api/" "$api/"; then
        echo "Error al recuperar los archivos de la API. Revisa $backup." >&2
        api_stopped=0
      fi
      echo "PostgreSQL conserva las migraciones aplicadas. Respaldo: $backup/database.dump" >&2
    fi
    if (( api_stopped )); then
      systemctl start "$service" ||
        echo "Revisa journalctl -u $service." >&2
    fi
  fi
  rm -rf -- "$build"
  exit "$status"
}
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

chown "$app_user:$app_user" "$build"
chmod 750 "$build"

# Compilar desde el commit remoto, conservando los ajustes locales de la VPS.
as_app git -C "$repo" fetch origin main
revision=$(as_app git -C "$repo" rev-parse --verify 'FETCH_HEAD^{commit}')
as_app mkdir "$build/source"
as_app git -C "$repo" archive "$revision" | as_app tar -xf - -C "$build/source"
echo "Compilando $revision"

as_app dotnet publish "$build/source/backend/AndEth3D.Backend.csproj" -c Release -o "$build/api"
as_app env PATH="$node_dir/bin:/usr/local/bin:/usr/bin:/bin" PUBLIC_API_URL="" bash -c '
  set -e
  cd "$1/source/frontend"
  npm ci
  npm test
  npm run build -- --outDir "$1/web"
' bash "$build"
test -s "$build/api/AndEth3D.Backend.dll"
test -s "$build/web/index.html"

mkdir -p /root/backups
backup=$(mktemp -d "/root/backups/andeth3d-$(date +%Y%m%d-%H%M%S).XXXXXX")
printf '%s\n' "$revision" > "$backup/revision.txt"
echo "Respaldando en $backup"

api_stopped=1
systemctl stop "$service"
rsync -a "$api/" "$backup/api/"
rsync -a "$web/" "$backup/web/"
docker exec "$postgres" sh -c 'pg_dump -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" -Fc' > "$backup/database.dump"
docker exec -i "$postgres" pg_restore --list < "$backup/database.dump" > "$backup/database-list.txt"

api_changed=1
rsync -a --delete --chown="$app_user:$app_user" --chmod=D755,F644 "${api_excludes[@]}" "$build/api/" "$api/"
systemctl start "$service"

for attempt in {1..30}; do
  if systemctl is-active --quiet "$service" &&
    curl -fsS --max-time 3 "http://127.0.0.1:5000$catalog" >/dev/null; then
    break
  fi
  if (( attempt == 30 )); then
    echo "La API no pasó la comprobación de arranque." >&2
    exit 1
  fi
  sleep 2
done

web_changed=1
rsync -a --chown="$app_user:$app_user" --chmod=D755,F644 "$build/web/" "$web/"
curl -fsS --max-time 10 "$domain/" >/dev/null
curl -fsS --max-time 10 "$domain$catalog" >/dev/null

echo "Publicado $revision en $domain"
echo "Respaldo: $backup"
