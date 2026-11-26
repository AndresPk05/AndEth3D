# Desplegar AndEth3D en Hostinger

1. Publica en `origin/main` el commit que incluye `scripts/deploy-vps.sh`.
2. Conéctate por SSH como root y ejecuta el siguiente bloque. Este mismo bloque descarga la versión más reciente del script en cada despliegue.

   ```bash
   cd /var/www/andeth3d &&
     sudo -u andeth3d -H git fetch origin main &&
     sudo -u andeth3d -H git show FETCH_HEAD:scripts/deploy-vps.sh > /root/andeth3d-deploy.sh &&
     bash /root/andeth3d-deploy.sh
   ```

Usa la instalación descrita en [publicar-vps-hostinger.md](publicar-vps-hostinger.md), incluido el archivo `/root/andeth3d-node-path` con una versión de Node compatible con `frontend/package.json`. PostgreSQL debe estar en el contenedor `andeth3d-postgres` y servir la base que usa la API. Programa el despliegue en un momento de poco tráfico. El script compila con la API activa y la detiene para hacer el respaldo final y publicar.

El script construye desde el commit remoto en una carpeta temporal, conserva los cambios locales del repositorio y mantiene el `appsettings.Production.json` y los archivos de la aplicación. Ejecuta las pruebas del frontend y comprueba la respuesta de la API y del sitio después de publicar.

Si falla al publicar, intenta recuperar los archivos anteriores. Las migraciones que PostgreSQL haya aplicado permanecen. Si la API anterior no arranca con el esquema actualizado, revisa `journalctl -u andeth3d.service` y evalúa la recuperación con el respaldo indicado en `/root/backups`. Los respaldos contienen datos privados.
