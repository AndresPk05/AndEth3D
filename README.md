# AndEth3D â€” Sitio web de impresiÃ³n 3D

Landing page + galerÃ­a + cotizaciones + seguimiento de pedidos, con panel de administraciÃ³n.

- **Frontend:** Astro + Tailwind CSS (`frontend/`) â€” http://localhost:4321
- **Backend:** ASP.NET Core Web API, .NET 10 (`backend/`) â€” http://localhost:5000
- **Base de datos:** PostgreSQL 16 en Docker (`docker-compose.yml`)

## CÃ³mo ejecutar

```bash
# 1. Base de datos (requiere Docker Desktop en ejecuciÃ³n)
docker compose up -d

# 2. Backend (aplica migraciones y siembra datos de ejemplo al arrancar)
cd backend
dotnet run

# 3. Frontend (en otra terminal)
cd frontend
npm install   # solo la primera vez
npm run dev
```

## Flujo de uso

1. El cliente se registra e inicia sesiÃ³n.
2. EnvÃ­a una cotizaciÃ³n desde `/cotizar` (descripciÃ³n, material, archivo opcional).
3. El admin la revisa en `/admin/cotizaciones`, responde y la **convierte en pedido** con precio.
4. El admin actualiza el estado del pedido en `/admin/pedidos` (Recibido â†’ En cola â†’ Imprimiendo â†’ Terminado â†’ Entregado).
5. El cliente sigue el estado en `/pedidos`.
6. El admin gestiona los trabajos publicados en `/admin/galeria` (sube foto, tÃ­tulo, categorÃ­a, material).

## Notas

- Las imÃ¡genes subidas se guardan en `backend/wwwroot/uploads/`.
- La clave JWT y credenciales de la BD en `appsettings.Development.json` son solo para desarrollo; cÃ¡mbialas para producciÃ³n.


## Configuración de seguridad

Antes del primer arranque, configura secretos con `dotnet user-secrets --project backend` (desarrollo) o variables de entorno (producción). No guardes contraseñas reales en archivos versionados.

| Variable | Uso |
| --- | --- |
| `POSTGRES_PASSWORD` | Contraseña requerida por Docker Compose; también debe coincidir con la conexión del backend. |
| `ConnectionStrings__Default` | Conexión PostgreSQL con host, base de datos, usuario y contraseña. |
| `Jwt__Key` | Clave aleatoria de al menos 32 bytes; usa una nueva clave para invalidar tokens anteriores. |
| `AdminSeed__Password` | Contraseña inicial de administrador, mínimo 12 caracteres y máximo 72 bytes UTF-8. Solo se usa si no existe administrador. |
| `AdminSeed__Email` | Correo del administrador inicial. |
| `Cors__Origins__0` | Origen permitido del frontend; HTTPS en producción. Añade índices para más orígenes. |
| `PUBLIC_API_URL` | URL pública de la API al compilar el frontend. En desarrollo usa localhost:5000; en producción, omitirla usa el mismo origen y requiere proxy para `/api` y `/uploads`. |

Ejemplo de los nombres para User Secrets: `dotnet user-secrets set "Jwt:Key" "<clave-aleatoria>" --project backend`. Configura también `ConnectionStrings:Default` y `AdminSeed:Password`. Los valores entre ángulos son marcadores, no secretos utilizables.

El backend exige HTTPS fuera de desarrollo. Si hay proxy TLS, configura correctamente el esquema reenviado y limita los proxies confiables en el despliegue. El servidor que aloja el frontend debe aplicar también sus encabezados de seguridad.

## Actualización de una instalación existente

- Respalda PostgreSQL y los archivos antes de desplegar. Las migraciones se aplican al arrancar.
- Revisa duplicados con `SELECT "QuoteRequestId", COUNT(*) FROM "Orders" WHERE "QuoteRequestId" IS NOT NULL GROUP BY "QuoteRequestId" HAVING COUNT(*) > 1;`. Resuélvelos según el negocio antes de actualizar: la restricción única detendrá la migración si existen; no se eliminan pedidos automáticamente.
- Rota las credenciales de BD, la clave JWT y la contraseña de cualquier administrador creado con la configuración anterior. Cambiar `AdminSeed:Password` NO cambia usuarios existentes. Los secretos previamente versionados siguen en el historial de Git.
- Los adjuntos nuevos se guardan en `backend/App_Data/attachments`, fuera de `wwwroot`. Conserva este directorio en un volumen persistente y en los respaldos.
- Los adjuntos antiguos en `wwwroot/uploads` siguen disponibles mediante descarga autenticada. No sirvas esa carpeta directamente desde Nginx, CDN u otro servidor: `/uploads` debe pasar por el backend, que solo permite imágenes de galería y bloquea referencias de cotizaciones.
- El cierre de sesión revoca todas las sesiones del usuario. Los JWT duran 30 minutos; al vencer se requiere iniciar sesión nuevamente. No se implementa renovación automática. Los tokens anteriores a esta actualización se rechazan.
- La validación de archivos verifica extensión, tamaño y firmas/estructura básica; no sustituye un antivirus ni un analizador completo de modelos 3D.

## Verificación

```bash
npm --prefix frontend test
dotnet run --project tests/SecurityChecks/SecurityChecks.csproj
dotnet build backend/AndEth3D.Backend.csproj
npm --prefix frontend run build
```

Las pruebas de seguridad no requieren paquetes externos. La compilación completa requiere restaurar las dependencias de NuGet y npm. Antes de publicar, prueba contra PostgreSQL: solicitudes simultáneas de conversión (un solo pedido), descarga por propietario/admin y rechazo de otro cliente, logout seguido de reutilización del token (401), y transición inválida (409).
