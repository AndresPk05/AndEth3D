# Cotizaciones sin iniciar sesión

`/cotizar` permite enviar una solicitud manual con nombre, correo, descripción, material y referencia opcional. Si existe sesión, conserva el flujo de cotización asociado a la cuenta. Sin sesión usa `POST /api/quotes/guest` y confirma la recepción sin devolver identificadores, datos de contacto ni rutas privadas. No envía correos automáticamente: el equipo responde usando los datos disponibles en administración.

Las cotizaciones y pedidos admiten un propietario nulo y conservan nombre/correo de contacto. Administración puede revisar, actualizar y convertir solicitudes de visitantes sin crear cuentas. Nunca se vinculan a una cuenta por coincidencia de correo, porque ese correo no ha sido verificado. El historial y las descargas siguen requiriendo propietario autenticado o administrador. El chat IA usa una sesión privada, que ahora también puede iniciarse como visitante desde `/crear`. Véase `configuracion-produccion.md`.

Límites de visitantes: 5 MB por archivo, 3 solicitudes por correo normalizado en 24 horas, 100 solicitudes globales en 24 horas y 60 peticiones al endpoint por hora/proceso antes de procesar el formulario. Las cuotas diarias se consultan en base de datos y las creaciones se serializan dentro del proceso. El limitador horario se reinicia al reiniciar la API. Si se escala a múltiples réplicas, sustituir la sincronización local por una cuota distribuida/transaccional. No hay CAPTCHA ni verificación de correo en esta versión.

## Actualización del VPS

Publicar backend y frontend juntos con el procedimiento de respaldo existente. Al arrancar, la API aplica `20261102202256_GuestQuotes`: hace nullable `UserId` en cotizaciones/pedidos y agrega los contactos. Los registros existentes conservan su propietario. No requiere claves ni configuración adicional. No desplegar únicamente el frontend, porque necesita el nuevo endpoint.

La reversión de la migración se bloquea si existen solicitudes o pedidos de visitantes, para evitar asignaciones ficticias o pérdida silenciosa de contactos.

## Verificación

ManufacturingChecks cubre validación, envío anónimo con archivo, cuotas, privacidad ante cuentas con correo coincidente, consulta administrativa y conversión a pedido; incluye comprobaciones de arquitectura y snapshot PostgreSQL. SecurityChecks y las pruebas frontend existentes complementan la validación. Falta comprobar visualmente el formulario en navegador y ejecutar la migración contra PostgreSQL real; el build Astro local está bloqueado porque la dependencia no está instalada en este entorno.
