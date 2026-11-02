# Actualizar appsettings en la VPS sin perder producción

.NET carga primero `appsettings.json`, luego `appsettings.Production.json` y después las variables de entorno. Los valores más específicos sobrescriben los anteriores. No copies la configuración base completa encima del archivo de producción.

## Archivos activos

- Base: `/var/www/andeth3d/backend/publish/appsettings.json`
- Producción: `/var/www/andeth3d/backend/publish/appsettings.Production.json`
- El servicio `andeth3d` tiene `ASPNETCORE_ENVIRONMENT=Production`.

Publica primero el backend actualizado siguiendo `publicar-vps-hostinger.md`. Su comando rsync actualiza la configuración base y excluye `appsettings.Production.json`. Así aparecen las opciones nuevas sin perder conexión PostgreSQL, JWT, CORS o claves.

Si solo necesitas sincronizar el archivo base después de actualizar el repositorio y confirmar que corresponde a la API desplegada:

```bash
API=/var/www/andeth3d/backend/publish
STAMP=$(date +%Y%m%d-%H%M%S)
mkdir -p /root/backups
cp -a "$API/appsettings.json" "/root/backups/appsettings-base-$STAMP.json"
cp -a "$API/appsettings.Production.json" "/root/backups/appsettings-production-$STAMP.json"
chmod 600 "/root/backups/appsettings-base-$STAMP.json" "/root/backups/appsettings-production-$STAMP.json"
install -o andeth3d -g andeth3d -m 644 \
  /var/www/andeth3d/backend/appsettings.json "$API/appsettings.json"
```

## Habilitar diseño de visitantes y OpenRouter

Edita solo los campos necesarios en producción:

```bash
nano /var/www/andeth3d/backend/publish/appsettings.Production.json
```

Integra las siguientes propiedades en los objetos existentes. No pegues un segundo objeto `OpenRouter` ni reemplaces el resto del documento:

```json
"GuestDesign": {
  "Enabled": true,
  "MaxSessionsPerDay": 10
},
"OpenRouter": {
  "Enabled": true,
  "ApiKey": "TU_CLAVE_PRIVADA_EXISTENTE",
  "Routing": {
    "Enabled": true
  }
}
```

El ejemplo es un fragmento, no el archivo completo. Conserva una clave real que ya tengas; no guardes el marcador del ejemplo. Los modelos y límites por ruta provienen de la base nueva, salvo que producción o el entorno ya los sobrescriban. Las rutas deben caber en el presupuesto de sesión. La generación de imágenes además requiere su configuración habilitada desde administración; el laminado necesita perfiles válidos, no basta con habilitar el chat.

No es necesario repetir todas las opciones nuevas en producción: basta con mantener allí las diferencias. Si producción ya contiene `Enabled: false`, seguirá sobrescribiendo un `true` en la base. Las listas de configuración .NET se combinan por índices: al cambiar `Models`, revisa ambas fuentes para no conservar modelos residuales de una lista más larga.

Valida sintaxis sin imprimir secretos y reinicia:

```bash
python3 -m json.tool /var/www/andeth3d/backend/publish/appsettings.Production.json > /dev/null &&
systemctl restart andeth3d

systemctl status andeth3d --no-pager
journalctl -u andeth3d --since '5 minutes ago' --no-pager
```

Una sintaxis válida no garantiza que los valores sean correctos; revisa también la validación de opciones al arrancar. No publiques el contenido de estos archivos en capturas ni en Git.

## Experiencia unificada

`/crear` contiene conversación, ficha, imágenes, estimación y formulario de contacto. `/cotizar` conserva un enlace de compatibilidad y redirige a `/crear#solicitar`. El cliente puede solicitar una cotización manual aunque la IA esté pausada. El botón «Usar la ficha del asistente» lleva los detalles al formulario sin sobrescribir una descripción editada sin confirmación.

La visita sin cuenta recibe un token privado de 30 minutos, separado del login. Usa una identidad técnica con rol Guest y sin contraseña ni datos de contacto; no es una cuenta con credenciales. No puede acceder a administración, APIs de cuentas ni presentar cotizaciones de diseño sin datos de contacto. Para cotizar usa el formulario en la misma página. Las referencias generadas no se adjuntan automáticamente a esa solicitud manual; se envía la descripción y cualquier archivo que el visitante seleccione.

El límite inicial es de 10 sesiones nuevas de visitantes en 24 horas para toda la aplicación, además de las cuotas y reservas existentes de chat/imágenes. No equivale a IA ilimitada ni gratuita para el negocio. La cuenta de OpenRouter sigue asumiendo el consumo. El control de creación se serializa por proceso; para varias réplicas hace falta coordinación distribuida. No hay renovación ni recuperación de la visita al caducar, ni limpieza automática de identidades/conversaciones antiguas; definir retención antes de ampliar el volumen.
