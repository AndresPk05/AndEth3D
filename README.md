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
