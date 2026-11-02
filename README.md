# AndEth3D ‚Äî Sitio web de impresi√≥n 3D

Landing page + galer√≠a + cotizaciones + seguimiento de pedidos, con panel de administraci√≥n.

- **Frontend:** Astro + Tailwind CSS (`frontend/`) ‚Äî http://localhost:4321
- **Backend:** ASP.NET Core Web API, .NET 10 (`backend/`) ‚Äî http://localhost:5000
- **Base de datos:** PostgreSQL 16 en Docker (`docker-compose.yml`)

## C√≥mo ejecutar

```bash
# 1. Base de datos (requiere Docker Desktop en ejecuci√≥n)
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

1. El cliente se registra e inicia sesi√≥n.
2. Env√≠a una cotizaci√≥n desde `/cotizar` (descripci√≥n, material, archivo opcional).
3. El admin la revisa en `/admin/cotizaciones`, responde y la **convierte en pedido** con precio.
4. El admin actualiza el estado del pedido en `/admin/pedidos` (Recibido ‚Üí En cola ‚Üí Imprimiendo ‚Üí Terminado ‚Üí Entregado).
5. El cliente sigue el estado en `/pedidos`.
6. El admin gestiona los trabajos publicados en `/admin/galeria` (sube foto, t√≠tulo, categor√≠a, material).

## Notas

- Las im√°genes subidas se guardan en `backend/wwwroot/uploads/`.
- La clave JWT y credenciales de la BD en `appsettings.Development.json` son solo para desarrollo; c√°mbialas para producci√≥n.


## ConfiguraciÛn de seguridad

Antes del primer arranque, configura secretos con `dotnet user-secrets --project backend` (desarrollo) o variables de entorno (producciÛn). No guardes contraseÒas reales en archivos versionados.

| Variable | Uso |
| --- | --- |
| `POSTGRES_PASSWORD` | ContraseÒa requerida por Docker Compose; tambiÈn debe coincidir con la conexiÛn del backend. |
| `ConnectionStrings__Default` | ConexiÛn PostgreSQL con host, base de datos, usuario y contraseÒa. |
| `Jwt__Key` | Clave aleatoria de al menos 32 bytes; usa una nueva clave para invalidar tokens anteriores. |
| `AdminSeed__Password` | ContraseÒa inicial de administrador, mÌnimo 12 caracteres y m·ximo 72 bytes UTF-8. Solo se usa si no existe administrador. |
| `AdminSeed__Email` | Correo del administrador inicial. |
| `Cors__Origins__0` | Origen permitido del frontend; HTTPS en producciÛn. AÒade Ìndices para m·s orÌgenes. |
| `PUBLIC_API_URL` | URL p˙blica de la API al compilar el frontend. En desarrollo usa localhost:5000; en producciÛn, omitirla usa el mismo origen y requiere proxy para `/api` y `/uploads`. |

Ejemplo de los nombres para User Secrets: `dotnet user-secrets set "Jwt:Key" "<clave-aleatoria>" --project backend`. Configura tambiÈn `ConnectionStrings:Default` y `AdminSeed:Password`. Los valores entre ·ngulos son marcadores, no secretos utilizables.

El backend exige HTTPS fuera de desarrollo. Si hay proxy TLS, configura correctamente el esquema reenviado y limita los proxies confiables en el despliegue. El servidor que aloja el frontend debe aplicar tambiÈn sus encabezados de seguridad.

## ActualizaciÛn de una instalaciÛn existente

- Respalda PostgreSQL y los archivos antes de desplegar. Las migraciones se aplican al arrancar.
- Revisa duplicados con `SELECT "QuoteRequestId", COUNT(*) FROM "Orders" WHERE "QuoteRequestId" IS NOT NULL GROUP BY "QuoteRequestId" HAVING COUNT(*) > 1;`. ResuÈlvelos seg˙n el negocio antes de actualizar: la restricciÛn ˙nica detendr· la migraciÛn si existen; no se eliminan pedidos autom·ticamente.
- Rota las credenciales de BD, la clave JWT y la contraseÒa de cualquier administrador creado con la configuraciÛn anterior. Cambiar `AdminSeed:Password` NO cambia usuarios existentes. Los secretos previamente versionados siguen en el historial de Git.
- Los adjuntos nuevos se guardan en `backend/App_Data/attachments`, fuera de `wwwroot`. Conserva este directorio en un volumen persistente y en los respaldos.
- Los adjuntos antiguos en `wwwroot/uploads` siguen disponibles mediante descarga autenticada. No sirvas esa carpeta directamente desde Nginx, CDN u otro servidor: `/uploads` debe pasar por el backend, que solo permite im·genes de galerÌa y bloquea referencias de cotizaciones.
- El cierre de sesiÛn revoca todas las sesiones del usuario. Los JWT duran 30 minutos; al vencer se requiere iniciar sesiÛn nuevamente. No se implementa renovaciÛn autom·tica. Los tokens anteriores a esta actualizaciÛn se rechazan.
- La validaciÛn de archivos verifica extensiÛn, tamaÒo y firmas/estructura b·sica; no sustituye un antivirus ni un analizador completo de modelos 3D.

## VerificaciÛn

```bash
npm --prefix frontend test
dotnet run --project tests/SecurityChecks/SecurityChecks.csproj
dotnet build backend/AndEth3D.Backend.csproj
npm --prefix frontend run build
```

Las pruebas de seguridad no requieren paquetes externos. La compilaciÛn completa requiere restaurar las dependencias de NuGet y npm. Antes de publicar, prueba contra PostgreSQL: solicitudes simult·neas de conversiÛn (un solo pedido), descarga por propietario/admin y rechazo de otro cliente, logout seguido de reutilizaciÛn del token (401), y transiciÛn inv·lida (409).

## Fabricaci√≥n y precios (fase 1)

- Panel: `/admin/fabricacion`, exclusivo para administradores. Configura equipos FDM, dimensiones nominales en mm, margen, boquilla instalada, pared m√≠nima, colores simult√°neos y materiales compatibles.
- El volumen √∫til previsto resta dos m√°rgenes en ancho/fondo y uno en altura; no sustituye una validaci√≥n geom√©trica ni de orientaci√≥n.
- Registra costo por kilogramo en COP, materiales habilitados y colores disponibles. Un material se ofrece √∫nicamente si est√° habilitado, tiene alg√∫n color disponible y existe un equipo activo compatible.
- Configura categor√≠as, tarifas por hora de m√°quina/dise√±o/acabado, pedido m√≠nimo y margen bruto. La hora de m√°quina incluye electricidad, mantenimiento y depreciaci√≥n. Esta fase almacena tarifas; a√∫n no calcula presupuestos.
- La configuraci√≥n inicial propone una A1 y materiales PLA/PETG, todos desactivados. Los costos empiezan en cero y deben completarse con valores reales antes de guardar. No se asumen stock ni accesorios multicolor.
- Cada guardado publica una revisi√≥n completa e inmutable. El n√∫mero de versi√≥n evita sobrescrituras de administradores concurrentes; las revisiones tienen √≠ndice √∫nico en PostgreSQL. Se pueden consultar las √∫ltimas 100 versiones desde el panel y cualquier versi√≥n por su n√∫mero desde la API.
- Cada nueva cotizaci√≥n referencia la revisi√≥n vigente al recibirse. Las tarifas hist√≥ricas no cambian y las cotizaciones previas a la configuraci√≥n conservan una referencia nula. El administrador puede consultar las condiciones desde cada cotizaci√≥n. Los pedidos mantienen su v√≠nculo con la cotizaci√≥n existente.
- El cat√°logo p√∫blico usa una lista expl√≠cita de campos: materiales, colores disponibles y categor√≠as. No devuelve impresoras, marca/modelo, costos, tarifas ni identificadores de revisiones. Las condiciones hist√≥ricas solo son accesibles para administradores.
- `/cotizar` consume el cat√°logo y el backend rechaza materiales deshabilitados o inexistentes. La opci√≥n de asesor√≠a sigue disponible aunque no haya cat√°logo o falle su carga.
- La migraci√≥n `20261102000002_ManufacturingConfiguration` agrega la tabla de revisiones y una FK nullable con borrado restringido. No altera datos ni precios existentes. Se aplica mediante el arranque habitual; respaldar antes de actualizar producci√≥n.
- No incluye chat, OpenRouter, generaci√≥n de im√°genes, selecci√≥n de color en cotizaciones ni estimaciones autom√°ticas: corresponden a las siguientes fases. Otras tecnolog√≠as requieren su propio c√°lculo antes de habilitarlas.

Comprobaciones adicionales:

```powershell
dotnet run --project tests/ManufacturingChecks/ManufacturingChecks.csproj
dotnet run --project tests/SecurityChecks/SecurityChecks.csproj
npm --prefix frontend test
```

Las pruebas de integraci√≥n usan ASP.NET TestServer y SQLite en memoria para permisos, validaci√≥n, cat√°logo y conservaci√≥n de condiciones. Tambi√©n comparan el snapshot con el modelo PostgreSQL y generan el SQL de migraci√≥n sin conectarse a una base de datos. No sustituyen probar la migraci√≥n y escrituras concurrentes contra PostgreSQL antes de desplegar.