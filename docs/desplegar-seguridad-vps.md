# Publicar la actualización de seguridad de AndEth3D en Hostinger

Ejecuta los bloques en orden en **una misma sesión Bash como root** en `srv1908556`. Detente si alguno falla. La API sigue en servicio hasta el paso 5. Necesitas el SHA completo del commit revisado y subido a `origin/main`, con las correcciones de seguridad y el nuevo `frontend/package-lock.json`. El servidor no puede recuperar cambios que solo existen en tu PC.

## 1. Preparar la versión y el respaldo

```bash
set -euo pipefail
REPO=/var/www/andeth3d
API="$REPO/backend/publish"
WEB="$REPO/frontend/dist"
BACKUP="/root/backups/andeth3d-seguridad-$(date +%Y%m%d-%H%M%S)"
install -d -m 700 "$BACKUP"
BUILD=$(mktemp -d /var/tmp/andeth3d-release.XXXXXX)
chown andeth3d:andeth3d "$BUILD"
runapp() { sudo -u andeth3d -H "$@"; }
read -rp 'SHA completo del commit que quieres publicar: ' REV
[[ "$REV" =~ ^[0-9a-fA-F]{40}$ ]]
runapp git -C "$REPO" fetch origin main
runapp git -C "$REPO" merge-base --is-ancestor "$REV" FETCH_HEAD
runapp mkdir "$BUILD/src"
runapp git -C "$REPO" archive "$REV" | runapp tar -x -C "$BUILD/src"
test -f "$BUILD/src/backend/Services/Application/DailyAiBudget.cs"
test -s "$API/appsettings.Production.json"
systemctl is-active andeth3d
printf '%s\n' "$REV" > "$BACKUP/commit"
printf '%s\n' "$BUILD" > "$BACKUP/build-path"
cp -a /root/andeth3d-node-path "$BACKUP/node-path"
df -h "$BUILD" "$BACKUP" "$REPO"
```

La compilación usa una copia limpia del commit. Así se conservan el `docker-compose.yml`, los cambios locales y los archivos de producción de la VPS. Comprueba que hay espacio para la compilación, una copia de la aplicación y el respaldo de PostgreSQL.

## 2. Instalar Node para el usuario que compila

La última versión observada en esta VPS era **22.23.2**. El proyecto actualizado requiere **22.23.4 o posterior**. Este bloque selecciona la última versión publicada de la rama 22 desde el índice oficial, verifica el archivo con su SHA-256 e instala una copia separada. Necesita `curl`, `python3`, `xz` y acceso a `nodejs.org`.

```bash
case "$(uname -m)" in x86_64) ARCH=x64 ;; aarch64) ARCH=arm64 ;; *) echo "Arquitectura no prevista"; exit 1 ;; esac
V=$(curl -fsSL https://nodejs.org/dist/index.json | python3 -c 'import sys,json; print(next(r["version"] for r in json.load(sys.stdin) if r["version"].startswith("v22.")))')
[[ "$V" =~ ^v22\.[0-9]+\.[0-9]+$ ]]
dpkg --compare-versions "${V#v}" ge 22.23.4
FILE="node-$V-linux-$ARCH.tar.xz"
NODE_SHARED=$(mktemp -d /opt/andeth3d-node.XXXXXX)
(
  cd "$BACKUP"
  curl -fsSLO "https://nodejs.org/dist/$V/$FILE"
  curl -fsSLo SHASUMS256.txt "https://nodejs.org/dist/$V/SHASUMS256.txt"
  awk -v name="$FILE" '$2 == name' SHASUMS256.txt > node.sha256
  test -s node.sha256
  sha256sum -c node.sha256
  tar -xJf "$FILE" --strip-components=1 -C "$NODE_SHARED"
)
chmod 755 "$NODE_SHARED"
appnode() { runapp env PATH="$NODE_SHARED/bin:/usr/local/bin:/usr/bin:/bin" ASTRO_TELEMETRY_DISABLED=1 "$@"; }
appnode node -v
appnode npm -v
```

## 3. Auditar, probar y compilar antes de detener el servicio

```bash
export DOTNET_CLI_TELEMETRY_OPTOUT=1
appnet() { runapp env DOTNET_CLI_TELEMETRY_OPTOUT=1 dotnet "$@"; }
cd "$BUILD/src/frontend"
appnode npm ci
appnode npm audit --audit-level=moderate
appnode npm test
appnode env PUBLIC_API_URL="" npm run build -- --outDir "$BUILD/web"

cd "$BUILD/src"
PROJECT=backend/AndEth3D.Backend.csproj
appnet restore "$PROJECT" -p:NuGetAudit=true -p:NuGetAuditMode=all '-p:WarningsAsErrors=NU1900%3BNU1903%3BNU1904%3BNU1905'
appnet list "$PROJECT" package --vulnerable --include-transitive --no-restore
appnet run --project tests/SecurityChecks
appnet run --project tests/ManufacturingChecks
appnet publish "$PROJECT" -c Release --no-restore -o "$BUILD/api"
test -s "$BUILD/api/AndEth3D.Backend.dll"
test -s "$BUILD/web/crear/index.html" || test -s "$BUILD/web/crear.html"
```

Revisa el informe de NuGet aunque el comando termine con éxito. Ante un aviso alto/crítico, o si npm o NuGet no pueden consultar su registro, detén el despliegue. Corrige y prueba las dependencias en el repositorio y publica el nuevo commit. Si el código salta varias versiones, revisa también las migraciones pendientes antes de seguir.

## 4. Revisar el proxy y el presupuesto

Localiza **el archivo activo** que atiende al dominio. El volcado puede contener configuración privada.

```bash
nginx -T > "$BACKUP/nginx-antes.txt" 2>&1
grep -nE '^# configuration file|server_name .*andeth3d|proxy_pass .*5000|proxy_set_header X-Forwarded-For' "$BACKUP/nginx-antes.txt"
read -erp 'Ruta del archivo de Nginx con el location /api: ' SITE
SITE=$(realpath -e "$SITE")
[[ "$SITE" == /etc/nginx/* && -f "$SITE" ]]
cp -a "$SITE" "$BACKUP/nginx-site.conf"
printf '%s\n' "$SITE" > "$BACKUP/nginx-site-path"
nano "$SITE"
```

En el bloque HTTPS que **ya** reenvía `/api` a `127.0.0.1:5000`, conserva sus directivas y establece esta cabecera una sola vez. Para esta instalación con Nginx directamente expuesto a Internet, sobrescribir la cabecera descarta direcciones enviadas por el visitante. Si hay un CDN delante, configura primero sus rangos de confianza o el límite agrupará a los clientes por IP del CDN.

```nginx
proxy_set_header X-Forwarded-For $remote_addr;
```

```bash
nginx -t && systemctl reload nginx
```

El nuevo `appsettings.json` base establece **USD 10 diarios en reservas compartidas de chat e imágenes**. Puedes conservar ese valor sin editar producción. Si deseas otro importe, respalda el archivo activo y cambia **solo** la propiedad numérica `DailyGlobalBudgetUsd` dentro del objeto `OpenRouter` existente:

```bash
cp -a "$API/appsettings.Production.json" "$BACKUP/production-original.json"
# Opcional: nano "$API/appsettings.Production.json"
python3 -m json.tool "$API/appsettings.Production.json" >/dev/null
```

No dupliques la sección ni reemplaces las claves. Si usas un override de systemd llamado `OpenRouter__DailyGlobalBudgetUsd`, ese valor tiene prioridad. Este presupuesto corresponde a reservas de la aplicación. El límite de gasto efectivo de la clave se configura en OpenRouter.

## 5. Respaldar y activar

Hazlo en una ventana de poco tráfico. Comprueba que el contenedor indicado es la base usada por la API. Si falla **durante el respaldo, antes de copiar archivos**, arranca la versión existente con `systemctl start andeth3d`. Si falla después, usa el paso 7.

```bash
systemctl stop andeth3d
tar -czf "$BACKUP/api.tgz" -C "$API" .
tar -czf "$BACKUP/web.tgz" -C "$WEB" .
docker exec andeth3d-postgres sh -ec 'pg_dump -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" -Fc' > "$BACKUP/postgres.dump"
docker exec -i andeth3d-postgres pg_restore --list < "$BACKUP/postgres.dump" > "$BACKUP/postgres-index.txt"
rsync -a --chown=andeth3d:andeth3d --exclude='/appsettings.Production.json' \
  --exclude='/App_Data/' --exclude='/wwwroot/uploads/' --exclude='/publish/' "$BUILD/api/" "$API/"
systemctl start andeth3d
curl -fsS --retry 10 --retry-all-errors --retry-delay 2 https://andeth3d.online/api/chat/status
rsync -a --chown=andeth3d:andeth3d --chmod=D755,F644 "$BUILD/web/" "$WEB/"
printf '%s\n' "$NODE_SHARED" > /root/andeth3d-node-path
```

## 6. Verificar el resultado

- Abre `https://andeth3d.online/crear` en una ventana privada con la pestaña **Red** abierta. Cargar la página no debe hacer `POST /api/design-guest`. Al enviar el primer mensaje sí debe hacerlo. Esa prueba puede consumir presupuesto de IA.
- En registro, tanto un correo nuevo como uno existente llevan al login. No se inicia sesión automáticamente. Prueba con una cuenta tuya.
- Los límites son 5 registros por hora, 15 intentos de login cada 10 minutos y 3 sesiones nuevas de diseño por hora, por IP. Las pruebas de integración del paso 3 verifican la denegación a invitados en rutas de cotización protegidas, incluso con barra final. Evita agotar cuotas de clientes en producción.
- Comprueba el frontend, el catálogo y los registros de arranque:

```bash
curl -fsS -o /dev/null https://andeth3d.online/crear/
curl -fsS -o /dev/null https://andeth3d.online/api/manufacturing/catalog
systemctl is-active andeth3d
journalctl -u andeth3d --since '10 minutes ago' --no-pager
```

## 7. Volver a la versión anterior

Si solo falló el cambio de Nginx, restaura su archivo y recarga con `cp -a "$BACKUP/nginx-site.conf" "$SITE" && nginx -t && systemctl reload nginx`.

Para revertir la aplicación, **confirma antes que la base actual sea compatible con la versión anterior**. Este cambio de seguridad no incorpora migraciones propias, pero al arrancar la API se aplican todas las pendientes. Si entraron pedidos después del despliegue, restaurar el dump completo descartaría esos datos.

Si perdiste la sesión SSH, vuelve a definir `BACKUP` con la ruta exacta de este despliegue y recupera `SITE` desde `"$BACKUP/nginx-site-path"`. Define de nuevo `API=/var/www/andeth3d/backend/publish` y `WEB=/var/www/andeth3d/frontend/dist`. Para una base compatible:

```bash
set -euo pipefail
test -s "$BACKUP/api.tgz" && test -s "$BACKUP/web.tgz"
systemctl stop andeth3d
ROLL=$(mktemp -d /var/tmp/andeth3d-rollback.XXXXXX)
mkdir "$ROLL/api" "$ROLL/web"
tar -xzf "$BACKUP/api.tgz" -C "$ROLL/api"
tar -xzf "$BACKUP/web.tgz" -C "$ROLL/web"
rsync -a --delete --exclude='/App_Data/' --exclude='/wwwroot/uploads/' --exclude='/publish/' "$ROLL/api/" "$API/"
cp -a "$BACKUP/production-original.json" "$API/appsettings.Production.json"
rsync -a --delete "$ROLL/web/" "$WEB/"
cp -a "$BACKUP/node-path" /root/andeth3d-node-path
cp -a "$BACKUP/nginx-site.conf" "$SITE"
nginx -t && systemctl reload nginx
systemctl start andeth3d
curl -fsS --retry 10 --retry-all-errors https://andeth3d.online/api/manufacturing/catalog >/dev/null
```
