# Fase 6: operación y calibración

La página `/admin/operacion` permite registrar consumos reales de pedidos terminados o entregados, consultar métricas y promover muestras revisadas a una nueva calibración. Aplicar la migración `20261102193926_ProductionOperations` antes de desplegar. El registro es inmutable e idempotente por solicitud, uno por pedido; gramos, horas y costos corresponden al lote completo. No cambia el precio acordado. Solo Admin accede a estos datos.

El tablero muestra conversión por conversación, posible abandono por siete días de inactividad sin cotización ni trabajos pendientes, desviación estimada/confirmada, consumo IA reportado separado de reservas y valores desconocidos, regeneraciones y errores por categoría. El margen usa costos registrados, no cobros ni utilidad contable. Las definiciones se muestran en pantalla; la inactividad no demuestra abandono.

Calibración requiere evidencia de producción, dimensiones completas y revisión de fabricación vigente. Normaliza material/tiempo por unidad, conserva diseño por lote y evita duplicar evidencia. La promoción es manual y crea una revisión; pedidos históricos no se recalculan.

El router interno de laminado elige perfiles compatibles por prioridad (menor primero), trabajos activos por equipo, volumen útil y nombre de perfil. Verifica hash del archivo local antes de seleccionar; prioridad y ruta local no cambian identidad técnica histórica. Añade plantilla `frame` para categoría `marcos`: debe habilitarse en el catálogo. Materiales adicionales se habilitan mediante configuración y perfiles verificados, sin inventar inventario.

`experiments/free3d` contiene un evaluador offline de STL binario, evidencia humana vinculada por hash y protocolo de piloto. No integra un proveedor 3D ni habilita fabricación libre. Falta ejecutar un piloto físico con muestras reales para evaluar esa viabilidad.

Pruebas: suite ManufacturingChecks (incluye arquitectura e integración), SecurityChecks, pruebas frontend y cuatro pruebas Python del evaluador. El entorno no permite completar build de producción por resolución NuGet fallida y Astro ausente. No se ejecutó PostgreSQL real, Docker/slicer, proveedores pagados ni impresión física.
