# AndEth3D — Sitio web de impresión 3D

Landing page + galería + cotizaciones + seguimiento de pedidos, con panel de administración.

- **Frontend:** Astro + Tailwind CSS (`frontend/`) — http://localhost:4321
- **Backend:** ASP.NET Core Web API, .NET 10 (`backend/`) — http://localhost:5000
- **Base de datos:** PostgreSQL 16 en Docker (`docker-compose.yml`)

## Cómo ejecutar

```bash
# 1. Base de datos (requiere Docker Desktop en ejecución)
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

1. El cliente se registra e inicia sesión.
2. Envía una cotización desde `/cotizar` (descripción, material, archivo opcional).
3. El admin la revisa en `/admin/cotizaciones`, responde y la **convierte en pedido** con precio.
4. El admin actualiza el estado del pedido en `/admin/pedidos` (Recibido → En cola → Imprimiendo → Terminado → Entregado).
5. El cliente sigue el estado en `/pedidos`.
6. El admin gestiona los trabajos publicados en `/admin/galeria` (sube foto, título, categoría, material).

## Notas

- Las imágenes subidas se guardan en `backend/wwwroot/uploads/`.
- La clave JWT y credenciales de la BD en `appsettings.Development.json` son solo para desarrollo; cámbialas para producción.


## Configuraci�n de seguridad

Antes del primer arranque, configura secretos con `dotnet user-secrets --project backend` (desarrollo) o variables de entorno (producci�n). No guardes contrase�as reales en archivos versionados.

| Variable | Uso |
| --- | --- |
| `POSTGRES_PASSWORD` | Contrase�a requerida por Docker Compose; tambi�n debe coincidir con la conexi�n del backend. |
| `ConnectionStrings__Default` | Conexi�n PostgreSQL con host, base de datos, usuario y contrase�a. |
| `Jwt__Key` | Clave aleatoria de al menos 32 bytes; usa una nueva clave para invalidar tokens anteriores. |
| `AdminSeed__Password` | Contrase�a inicial de administrador, m�nimo 12 caracteres y m�ximo 72 bytes UTF-8. Solo se usa si no existe administrador. |
| `AdminSeed__Email` | Correo del administrador inicial. |
| `Cors__Origins__0` | Origen permitido del frontend; HTTPS en producci�n. A�ade �ndices para m�s or�genes. |
| `PUBLIC_API_URL` | URL p�blica de la API al compilar el frontend. En desarrollo usa localhost:5000; en producci�n, omitirla usa el mismo origen y requiere proxy para `/api` y `/uploads`. |

Ejemplo de los nombres para User Secrets: `dotnet user-secrets set "Jwt:Key" "<clave-aleatoria>" --project backend`. Configura tambi�n `ConnectionStrings:Default` y `AdminSeed:Password`. Los valores entre �ngulos son marcadores, no secretos utilizables.

El backend exige HTTPS fuera de desarrollo. Si hay proxy TLS, configura correctamente el esquema reenviado y limita los proxies confiables en el despliegue. El servidor que aloja el frontend debe aplicar tambi�n sus encabezados de seguridad.

## Actualizaci�n de una instalaci�n existente

- Respalda PostgreSQL y los archivos antes de desplegar. Las migraciones se aplican al arrancar.
- Revisa duplicados con `SELECT "QuoteRequestId", COUNT(*) FROM "Orders" WHERE "QuoteRequestId" IS NOT NULL GROUP BY "QuoteRequestId" HAVING COUNT(*) > 1;`. Resu�lvelos seg�n el negocio antes de actualizar: la restricci�n �nica detendr� la migraci�n si existen; no se eliminan pedidos autom�ticamente.
- Rota las credenciales de BD, la clave JWT y la contrase�a de cualquier administrador creado con la configuraci�n anterior. Cambiar `AdminSeed:Password` NO cambia usuarios existentes. Los secretos previamente versionados siguen en el historial de Git.
- Los adjuntos nuevos se guardan en `backend/App_Data/attachments`, fuera de `wwwroot`. Conserva este directorio en un volumen persistente y en los respaldos.
- Los adjuntos antiguos en `wwwroot/uploads` siguen disponibles mediante descarga autenticada. No sirvas esa carpeta directamente desde Nginx, CDN u otro servidor: `/uploads` debe pasar por el backend, que solo permite im�genes de galer�a y bloquea referencias de cotizaciones.
- El cierre de sesi�n revoca todas las sesiones del usuario. Los JWT duran 30 minutos; al vencer se requiere iniciar sesi�n nuevamente. No se implementa renovaci�n autom�tica. Los tokens anteriores a esta actualizaci�n se rechazan.
- La validaci�n de archivos verifica extensi�n, tama�o y firmas/estructura b�sica; no sustituye un antivirus ni un analizador completo de modelos 3D.

## Verificaci�n

```bash
npm --prefix frontend test
dotnet run --project tests/SecurityChecks/SecurityChecks.csproj
dotnet build backend/AndEth3D.Backend.csproj
npm --prefix frontend run build
```

Las pruebas de seguridad no requieren paquetes externos. La compilaci�n completa requiere restaurar las dependencias de NuGet y npm. Antes de publicar, prueba contra PostgreSQL: solicitudes simult�neas de conversi�n (un solo pedido), descarga por propietario/admin y rechazo de otro cliente, logout seguido de reutilizaci�n del token (401), y transici�n inv�lida (409).

## Fabricación y precios (fase 1)

- Panel: `/admin/fabricacion`, exclusivo para administradores. Configura equipos FDM, dimensiones nominales en mm, margen, boquilla instalada, pared mínima, colores simultáneos y materiales compatibles.
- El volumen útil previsto resta dos márgenes en ancho/fondo y uno en altura; no sustituye una validación geométrica ni de orientación.
- Registra costo por kilogramo en COP, materiales habilitados y colores disponibles. Un material se ofrece únicamente si está habilitado, tiene algún color disponible y existe un equipo activo compatible.
- Configura categorías, tarifas por hora de máquina/diseño/acabado, pedido mínimo y margen bruto. La hora de máquina incluye electricidad, mantenimiento y depreciación. Esta fase almacena tarifas; aún no calcula presupuestos.
- La configuración inicial propone una A1 y materiales PLA/PETG, todos desactivados. Los costos empiezan en cero y deben completarse con valores reales antes de guardar. No se asumen stock ni accesorios multicolor.
- Cada guardado publica una revisión completa e inmutable. El número de versión evita sobrescrituras de administradores concurrentes; las revisiones tienen índice único en PostgreSQL. Se pueden consultar las últimas 100 versiones desde el panel y cualquier versión por su número desde la API.
- Cada nueva cotización referencia la revisión vigente al recibirse. Las tarifas históricas no cambian y las cotizaciones previas a la configuración conservan una referencia nula. El administrador puede consultar las condiciones desde cada cotización. Los pedidos mantienen su vínculo con la cotización existente.
- El catálogo público usa una lista explícita de campos: materiales, colores disponibles y categorías. No devuelve impresoras, marca/modelo, costos, tarifas ni identificadores de revisiones. Las condiciones históricas solo son accesibles para administradores.
- `/cotizar` consume el catálogo y el backend rechaza materiales deshabilitados o inexistentes. La opción de asesoría sigue disponible aunque no haya catálogo o falle su carga.
- La migración `20261102000002_ManufacturingConfiguration` agrega la tabla de revisiones y una FK nullable con borrado restringido. No altera datos ni precios existentes. Se aplica mediante el arranque habitual; respaldar antes de actualizar producción.
- No incluye chat, OpenRouter, generación de imágenes, selección de color en cotizaciones ni estimaciones automáticas: corresponden a las siguientes fases. Otras tecnologías requieren su propio cálculo antes de habilitarlas.

Comprobaciones adicionales:

```powershell
dotnet run --project tests/ManufacturingChecks/ManufacturingChecks.csproj
dotnet run --project tests/SecurityChecks/SecurityChecks.csproj
npm --prefix frontend test
```

Las pruebas de integración usan ASP.NET TestServer y SQLite en memoria para permisos, validación, catálogo y conservación de condiciones. También comparan el snapshot con el modelo PostgreSQL y generan el SQL de migración sin conectarse a una base de datos. No sustituyen probar la migración y escrituras concurrentes contra PostgreSQL antes de desplegar.
## Asistente de diseño (fase 2)

- Ruta `/crear`: chat autenticado con historial por cliente, ficha lateral, preguntas progresivas y respuestas sugeridas. Disponible desde navegación y desde Cotizar. Se conservan objeto, uso, categoría, medidas en mm, cantidad, material, colores, personalización y referencias descriptivas.
- El agente extrae cambios estructurados; las preguntas, las restricciones y el estado se calculan en el backend. Los valores no modificados se conservan. Los nombres de equipos, marca/modelo, claves y tarifas no se envían al modelo ni a las respuestas públicas. No existen herramientas del agente para modificar configuración o precios.
- Solo el propietario accede a su conversación (también se aplica a cuentas administradoras). El historial permanece disponible si se deshabilita el proveedor. Se pagina en grupos de 30 conversaciones.
- El catálogo se revalida en cada turno y lectura. Las dimensiones se comparan con cada alternativa de fabricación por separado, contemplando permutación de ejes como comprobación preliminar; no garantiza orientación imprimible. Las categorías/materiales/colores no disponibles y las combinaciones de tamaño/color se marcan para revisión. La ficha completa siempre requiere revisión técnica posterior.
- Referencias en esta fase: texto. No se descargan URLs ni se interpretan archivos/imágenes. No incluye imágenes generadas, precios ni conversión automática de la conversación en cotización (fases posteriores).

### Activar OpenRouter

El chat viene desactivado. Configura secretos mediante User Secrets o variables de entorno, nunca en archivos versionados:

| Variable | Uso |
| --- | --- |
| `OpenRouter__Enabled` | `true` para habilitar nuevas conversaciones y mensajes. |
| `OpenRouter__ApiKey` | Clave privada, solo en backend. |
| `OpenRouter__Models__0` | Identificador del modelo conversacional compatible con JSON Schema estricto. |
| `OpenRouter__Models__1`, `OpenRouter__Models__2` | Alternativas opcionales compatibles; OpenRouter gestiona el fallback. |
| `OpenRouter__SessionBudgetUsd` | Presupuesto conservador máximo por conversación (USD). |
| `OpenRouter__MaxPromptUsdPerMillion` | Tope de precio de entrada por millón de tokens. |
| `OpenRouter__MaxCompletionUsdPerMillion` | Tope de precio de salida por millón de tokens. |

Configura antes el catálogo de fabricación con equipos activos, costos reales y materiales/colores disponibles. El servidor valida la configuración de OpenRouter al arrancar cuando está habilitado. Si ningún proveedor/modelo cumple el esquema y los topes, el turno falla conservando la ficha previa. El endpoint está fijado a HTTPS de OpenRouter y no admite URLs del cliente.

Los límites adicionales se encuentran en `appsettings.json`: timeout, tokens de salida, tamaño máximo del prompt, turnos por conversación, solicitudes y conversaciones por día, e intervalo mínimo. Los límites diarios usan UTC.

Cada solicitud reserva presupuesto y cuota en la base de datos **antes** de contactar al proveedor. La reserva usa el límite de bytes del prompt más holgura para tokens de control, tokens de salida y topes de precio; se multiplica por la cantidad de modelos configurados para cubrir alternativas. Es una política conservadora, no una factura de consumo. También se registra `usage.cost` cuando está disponible; si excede la reserva, se bloquean nuevas llamadas en esa conversación. Los fallos, timeouts y llamadas con resultado incierto conservan la reserva. Para un límite global de facturación, configura además el límite de la clave en OpenRouter.

Un identificador por mensaje evita repetir llamadas en reintentos. La reserva usa versiones de concurrencia en conversación y cuota de usuario; una solicitud en curso bloquea otro turno. Si el proceso se interrumpe, el bloqueo expira después del timeout más 30 segundos; el mensaje aparece como fallido y puede enviarse otro. El resultado tardío nunca sobrescribe una ficha más reciente. El frontend conserva el identificador pendiente en sessionStorage por usuario/conversación cuando falla la conexión.

La migración `20261102183204_DesignChat` agrega conversaciones, turnos y cuotas; se aplica al arrancar junto con las migraciones previas. No altera cotizaciones existentes.

Validación de fase 2: `dotnet run --project tests/ManufacturingChecks/ManufacturingChecks.csproj` incluye TestServer, agente simulado, aislamiento de usuarios, límites, reintentos, concurrencia, conservación de ficha y contrato HTTP de OpenRouter con un handler simulado. Se genera el SQL PostgreSQL y se compara el snapshot; las operaciones se prueban en SQLite en memoria. Las llamadas reales requieren configurar credenciales y un modelo, y la migración debe verificarse en una instancia PostgreSQL antes de desplegar.