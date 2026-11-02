# Publicar AndEth3D en la VPS de Hostinger

Guía basada en la instalación comprobada el 2 de noviembre de 2026. Incluye la actualización que permite cotizar sin iniciar sesión.

## 1. Datos de la instalación

| Elemento | Valor |
| --- | --- |
| Servidor | `srv1908556` |
| Dominio | `https://andeth3d.online` |
| Dominio adicional | `www.andeth3d.online` |
| Repositorio y rama | `/var/www/andeth3d`, `main` |
| Usuario propietario | `andeth3d` |
| Servicio API | `andeth3d.service` |
| API publicada / WorkingDirectory | `/var/www/andeth3d/backend/publish` |
| Ejecutable API | `/usr/bin/dotnet /var/www/andeth3d/backend/publish/AndEth3D.Backend.dll` |
| Dirección interna API | `http://127.0.0.1:5000` |
| Carpeta que publica Nginx | `/var/www/andeth3d/frontend/dist` |
| Contenedor PostgreSQL | `andeth3d-postgres`, imagen `postgres:16` |
| SDK .NET observado | `10.0.114` |
| Node/npm usados para compilar | Node `22.23.2`, npm `10.9.8` |
| Ruta del Node compartido | Guardada en `/root/andeth3d-node-path` |

La sesión de root y la de `andeth3d` tenían versiones diferentes de Node. Por eso esta guía pasa explícitamente el `PATH` al compilar. No es necesario reinstalar Node en cada actualización.

## 2. Asegurar que la versión está en el repositorio remoto

Desde tu equipo de desarrollo, revisa los cambios, genera el commit correspondiente y súbelo a la rama `main` mediante tu flujo habitual. Un cambio guardado solo en tu equipo no llegará a la VPS con `git pull`.

Para publicar la cotización sin sesión deben estar incluidos el endpoint de visitantes, el formulario y la migración `20261102202256_GuestQuotes`.

## 3. Conectarse y preparar variables

Conéctate por SSH con la IP que aparece en Hostinger, o utiliza su terminal web:

```bash
ssh root@IP_DE_TU_VPS
```

Ejecuta los siguientes bloques en esa misma sesión de Bash como root. Si un bloque falla, no continúes con el siguiente. Los bloques críticos usan una subshell con `set -e` para detenerse ante errores sin cerrar tu conexión.

```bash
REPO=/var/www/andeth3d
API=/var/www/andeth3d/backend/publish
WEB=/var/www/andeth3d/frontend/dist
SERVICE=andeth3d
RELEASE=$(date +%Y%m%d-%H%M%S)
BACKUP="/root/backups/andeth3d-$RELEASE"

mkdir -p "$BACKUP"
chmod 700 "$BACKUP"
printf 'Respaldo: %s\n' "$BACKUP"
```

Comprueba las herramientas y el estado:

```bash
systemctl is-active "$SERVICE"
docker ps --format 'table {{.Names}}\t{{.Image}}\t{{.Status}}'
dotnet --list-sdks
command -v rsync
sudo -u andeth3d -H git -C "$REPO" status --short
sudo -u andeth3d -H git -C "$REPO" branch --show-current
```

Debe aparecer la rama `main`. Usa Git como `andeth3d` para evitar el error `dubious ownership`. No cambies todo el repositorio a root ni uses `git clean` o `git reset --hard`.

## 4. Respaldar antes de actualizar el código

Este primer respaldo conserva también los ajustes locales del servidor. Más adelante se repetirá el respaldo de datos con la API detenida.

```bash
(
  set -e
  umask 077
  test -f "$API/appsettings.Production.json"
  tar -czf "$BACKUP/api-previa.tar.gz" -C "$API" .
  tar -czf "$BACKUP/web-previa.tar.gz" -C "$WEB" .
  cp -a "$REPO/docker-compose.yml" "$BACKUP/docker-compose.yml"
  cp -a "$REPO/frontend/src/lib/api.ts" "$BACKUP/api.ts"
  if [ -f "$REPO/backend/appsettings.Production.json" ]; then
    cp -a "$REPO/backend/appsettings.Production.json" "$BACKUP/appsettings-fuente.Production.json"
  fi
  sudo -u andeth3d -H git -C "$REPO" diff > "$BACKUP/cambios-locales.patch"
  sudo -u andeth3d -H git -C "$REPO" rev-parse HEAD > "$BACKUP/commit-anterior.txt"
  systemctl cat "$SERVICE" > "$BACKUP/servicio.txt"
  nginx -T > "$BACKUP/nginx.txt" 2>&1
  echo 'Respaldo inicial completado'
)
```

Los respaldos pueden contener secretos. Consérvalos fuera de las carpetas públicas y no los subas a Git.

## 5. Actualizar el repositorio conservando los ajustes de la VPS

Los cambios locales conocidos son `docker-compose.yml` y la antigua asignación `API_URL = ""` en `frontend/src/lib/api.ts`. La nueva versión ya usa el mismo origen en producción; no necesita reaplicar esa asignación antigua.

Si `git status --short` muestra otros archivos modificados, revisa esos cambios antes de seguir. Los archivos sin seguimiento `backend/publish/` y `backend/appsettings.Production.json` deben permanecer en su sitio.

```bash
(
  set -e
  if ! sudo -u andeth3d -H git -C "$REPO" diff --quiet HEAD -- docker-compose.yml frontend/src/lib/api.ts; then
    sudo -u andeth3d -H git -C "$REPO" stash push \
      -m "Ajustes VPS $RELEASE" -- docker-compose.yml frontend/src/lib/api.ts
  fi

  sudo -u andeth3d -H git -C "$REPO" pull --ff-only origin main

  # Conserva exactamente la configuración Docker respaldada, sin aplicar un stash antiguo.
  cp -a "$BACKUP/docker-compose.yml" "$REPO/docker-compose.yml"
  sudo -u andeth3d -H git -C "$REPO" log -1 --oneline
  sudo -u andeth3d -H git -C "$REPO" rev-parse HEAD > "$BACKUP/commit-nuevo.txt"
)
```

Si el pull falla por autenticación, divergencia o archivos sin seguimiento, detente y resuelve el mensaje sin forzar el reemplazo. No ejecutes `stash pop`: el ajuste antiguo de `api.ts` no debe sobreescribir la nueva lógica. El stash permanece como respaldo adicional.

No ejecutes `docker compose down -v`: eliminaría el volumen de PostgreSQL. Esta actualización no necesita recrear el contenedor. Si una versión futura modifica intencionalmente la infraestructura Docker, habrá que comparar y combinar esos cambios con la configuración local.

## 6. Preparar Node y permisos

```bash
NODE_SHARED=$(cat /root/andeth3d-node-path)

sudo -u andeth3d -H env \
  PATH="$NODE_SHARED/bin:/usr/local/bin:/usr/bin:/bin" \
  bash -c 'node --version; npm --version'
```

Debe mostrar Node 22.23.2 y npm 10.9.8, o versiones compatibles con las dependencias de la versión que publicas. No continúes si vuelve a aparecer Node 18.

Corrige los permisos de las carpetas generadas si existen:

```bash
for folder in "$REPO/frontend/node_modules" "$REPO/frontend/.astro"; do
  if [ -d "$folder" ]; then
    chown -R andeth3d:andeth3d "$folder"
    chmod -R u+rwX "$folder"
  fi
done
```

Esto resuelve los errores observados en `node_modules/html-void-elements` y `.astro/content.d.ts`. No uses `chmod 777`.

## 7. Compilar y probar sin tocar la versión publicada

Crea una carpeta nueva en cada publicación. No reutilices la carpeta temporal de una versión anterior.

```bash
BUILD=$(mktemp -d /var/tmp/andeth3d-build.XXXXXX)
chown andeth3d:andeth3d "$BUILD"
chmod 755 "$BUILD"
printf '%s\n' "$BUILD" > "$BACKUP/build-path.txt"
echo "$BUILD"
```

Backend:

```bash
sudo -u andeth3d -H dotnet publish \
  "$REPO/backend/AndEth3D.Backend.csproj" \
  -c Release -o "$BUILD/api"
```

Frontend:

```bash
sudo -u andeth3d -H env \
  PATH="$NODE_SHARED/bin:/usr/local/bin:/usr/bin:/bin" \
  bash -c '
    set -e
    cd /var/www/andeth3d/frontend
    npm ci
    npm test
    PUBLIC_API_URL="" npm run build -- --outDir "$1/web"
  ' bash "$BUILD"
```

`PUBLIC_API_URL=""` hace que las llamadas usen `https://andeth3d.online/api/...`. Nginx continúa enviándolas al puerto interno 5000.

Comprueba los resultados:

```bash
ls -lh "$BUILD/api/AndEth3D.Backend.dll" "$BUILD/web/index.html"
```

No continúes si fallaron pruebas o compilación, aunque existan archivos parciales en la carpeta temporal. Hasta aquí la web activa sigue funcionando.

## 8. Detener la API y respaldar los datos finales

A partir de aquí habrá una interrupción breve. Hazlo en un momento de poco tráfico; evita interrumpir generaciones o laminados en curso.

```bash
(
  set -e
  umask 077
  test -f "$BUILD/api/AndEth3D.Backend.dll"
  test -f "$BUILD/web/index.html"
  command -v rsync
  systemctl stop "$SERVICE"

  tar -czf "$BACKUP/api-final.tar.gz" -C "$API" .
  tar -czf "$BACKUP/web-final.tar.gz" -C "$WEB" .

  docker exec andeth3d-postgres sh -c \
    'pg_dump -U "$POSTGRES_USER" -d "${POSTGRES_DB:-$POSTGRES_USER}" -Fc' \
    > "$BACKUP/database-final.dump"

  docker exec -i andeth3d-postgres pg_restore --list \
    < "$BACKUP/database-final.dump" > "$BACKUP/database-list.txt"

  echo 'Respaldo final completado'
)
```

El comando usa el usuario y base definidos al crear el contenedor. Si la conexión de la API apunta a otra base, ajusta `pg_dump` a esa base antes de continuar. `pg_restore --list` comprueba que el índice del respaldo es legible; no sustituye una prueba completa de restauración.

Si este bloque falla, todavía no se han reemplazado los archivos. Recupera la versión anterior con `systemctl start andeth3d` y resuelve el respaldo.

## 9. Publicar y arrancar el backend

```bash
(
  set -e
  rsync -a --chown=andeth3d:andeth3d \
    --exclude='/appsettings.Production.json' \
    --exclude='/App_Data/' \
    --exclude='/wwwroot/uploads/' \
    --exclude='/publish/' \
    "$BUILD/api/" "$API/"

  systemctl start "$SERVICE"
)
```

Se conservan la configuración de producción, los archivos privados y las imágenes existentes. Se actualiza el `appsettings.json` base. La carpeta `publish/` anidada se excluye para no copiar restos de publicaciones anteriores.

La API ejecuta `Database.MigrateAsync()` al arrancar. No es necesario ejecutar además `dotnet ef database update`. La migración `GuestQuotes` permite cotizaciones/pedidos sin usuario y agrega sus contactos; las cotizaciones existentes conservan su propietario.

Revisa el arranque:

```bash
systemctl status "$SERVICE" --no-pager
journalctl -u "$SERVICE" --since '5 minutes ago' --no-pager
```

Espera a que termine el arranque y comprueba que permanezca `active (running)`. Si reinicia repetidamente o aparecen errores de configuración/migración, no publiques todavía el frontend.

No hace falta `daemon-reload` porque no cambió la unidad systemd. Solo úsalo si editas el archivo del servicio o sus drop-ins.

## 10. Publicar el frontend

```bash
rsync -a --chown=andeth3d:andeth3d \
  --chmod=D755,F644 \
  "$BUILD/web/" "$WEB/"
```

No se usa `--delete`: se conservan recursos antiguos para pestañas abiertas. Las páginas retiradas en futuras versiones requieren una limpieza revisada por separado.

Nginx lee los archivos desde la misma ruta; no necesita reiniciarse. Si cambiaste su configuración, valida antes de recargar:

```bash
nginx -t && systemctl reload nginx
```

Mantén `/uploads` pasando por el backend. No lo conviertas en un alias de archivos estáticos: la API aplica los controles de acceso de los adjuntos.

## 11. Verificar la publicación

```bash
curl -sS -o /dev/null -w 'Inicio: HTTP %{http_code}\n' https://andeth3d.online/
curl -sS -o /dev/null -w 'Cotizar: HTTP %{http_code}\n' https://andeth3d.online/cotizar
curl -sS -o /dev/null -w 'Catalogo API: HTTP %{http_code}\n' https://andeth3d.online/api/manufacturing/catalog
systemctl is-active "$SERVICE"
```

Comprueba en el navegador:

1. En una ventana privada, abre `/cotizar`: no debe redirigir a login.
2. Envía una solicitud de prueba con nombre y un correo tuyo. Debe confirmar recepción; no envía un correo automático.
3. En administración, comprueba que aparezca con su nombre y correo. Prueba descargar una referencia si la adjuntaste.
4. Verifica que puedes convertirla en pedido sin crear una cuenta para el visitante.
5. Comprueba inicio de sesión, galería y pedidos anteriores.

Los visitantes tienen un máximo de 5 MB por archivo y 3 solicitudes por correo en 24 horas. Hay límites globales adicionales. El chat con IA permite visitas temporales sin cuenta; consulta `configuracion-produccion.md` para habilitar OpenRouter y ajustar el límite global de visitas. La publicación no configura automáticamente una clave privada.

## 12. Qué hacer si falla

| Problema | Acción |
| --- | --- |
| `dubious ownership` | Ejecuta Git con `sudo -u andeth3d -H git -C /var/www/andeth3d ...`. |
| Node 18 / `EBADENGINE` | Usa el `PATH` del Node compartido descrito en el paso 6. |
| `EACCES` en `.astro` o `node_modules` | Corrige propietario/permisos de esas carpetas y repite el build como `andeth3d`. |
| Error durante el build | No detengas la versión activa; corrige y recompila en staging. |
| Error de respaldo, antes de copiar | Inicia de nuevo la API anterior y revisa el error. |
| HTTP 502 | Revisa `systemctl status andeth3d` y `journalctl -u andeth3d`. |
| Error de configuración | Revisa localmente `backend/publish/appsettings.Production.json`; no compartas secretos. |
| Cambios no visibles | Comprueba el commit desplegado, la ruta de Nginx y recarga con Ctrl+F5. |

Si falla después de copiar archivos, conserva logs y respaldos. La restauración del código debe considerar la versión de la base de datos: cambiar DLL no revierte migraciones. No restaures automáticamente la base si ya recibió solicitudes nuevas, porque perderías esos datos. La migración `GuestQuotes` bloquea su reversión cuando existen registros de visitantes.

No extraigas respaldos encima de una aplicación en ejecución. Para una recuperación completa, detén la API, prepara una carpeta limpia con la versión respaldada y determina primero si la base actual es compatible o requiere restauración coordinada. No borres la instalación fallida hasta cerrar el diagnóstico.

## 13. Si se cierra la terminal

Las variables de Bash no persisten. Vuelve a definir `REPO`, `API`, `WEB` y `SERVICE` del paso 3, pero elige el respaldo de la publicación que estabas realizando en lugar de crear uno nuevo:

```bash
ls -ld /root/backups/andeth3d-*

# Sustituye FECHA-HORA por el directorio correcto de la lista.
BACKUP=/root/backups/andeth3d-FECHA-HORA
BUILD=$(cat "$BACKUP/build-path.txt")
NODE_SHARED=$(cat /root/andeth3d-node-path)
```

Verifica esas rutas antes de reanudar desde el paso que quedó pendiente. Conserva al menos el último respaldo comprobado y registra el commit desplegado. Los cambios de este documento por sí solos no publican la aplicación ni hacen commit/push.
