# Router de modelos

El router es determinista y no hace una llamada adicional para clasificar. No modifica reglas de fabricación, estimaciones, precios, geometría ni laminado. Los controllers siguen siendo adaptadores HTTP.

## Propuesta inicial

| Tarea | Modelo propuesto | Motivo |
| --- | --- | --- |
| Respuestas escalares simples | `google/gemini-3.1-flash-lite` | Extracción sencilla y menor costo |
| Interpretación habitual de requisitos | `openai/gpt-5.4-mini` | Equilibrio entre seguimiento de instrucciones y costo |
| Requisitos extensos, encajes, tolerancias o carga | `anthropic/claude-sonnet-4.6` | Reservar mayor capacidad para requisitos complejos |
| Borrador visual | `black-forest-labs/flux.2-klein-4b` | Exploración visual económica |
| Imagen detallada y edición | `google/gemini-3.1-flash-image` | Generación con referencias |
| Viabilidad, costos, STL y laminado | Código y slicer | Resultado reproducible y verificable |

Son candidatos iniciales, no un benchmark del proyecto. Antes de producción verificar JSON estricto en texto y PNG/resolución/`input_references` en `/api/v1/images/models` para imágenes; ejecutar un conjunto representativo de conversaciones e imágenes. La capacidad del modelo no constituye certificación mecánica.

Fuentes consultadas el 2 de noviembre de 2026:
- https://openrouter.ai/google/gemini-3.1-flash-lite
- https://openrouter.ai/openai/gpt-5.4-mini
- https://openrouter.ai/anthropic/claude-sonnet-4.6/pricing
- https://openrouter.ai/black-forest-labs/flux.2-klein-4b/api
- https://openrouter.ai/blog/announcements/image-api/

## Configuración de texto

`backend/appsettings.json`, sección `OpenRouter:Routing`, contiene `Simple`, `Standard`, `Complex`, cada una con `Models`, `MaxOutputTokens`, `TimeoutSeconds`, `MaxPromptUsdPerMillion`, `MaxCompletionUsdPerMillion`. Activar `OpenRouter:Routing:Enabled` con el chat y su clave ya configurados. Alternativa por entorno: `OpenRouter__Routing__Enabled=true`, `OpenRouter__Routing__Simple__Models__0=proveedor/modelo`. Reiniciar el proceso para aplicar cambios de texto. Nunca guardar la clave en el repositorio.

Desactivado conserva `OpenRouter:Models` y límites históricos. Activado usa las rutas declaradas, sin elegir modelos enviados por el cliente. Un mensaje corto escalar reconocido usa Simple; más de 700 caracteres o vocabulario de restricciones mecánicas usa Complex; el resto Standard. Es una heurística conservadora, no un clasificador semántico completo. Cambiar el modelo no requiere cambiar código; cambiar criterios sí.

Las reservas se calculan por ruta antes de llamar al proveedor. Cada ruta admite hasta tres modelos ordenados para fallback de OpenRouter, todos incluidos en la reserva conservadora. No se escala automáticamente a otra ruta ni se degrada a un modelo barato si falta presupuesto. Cada ruta debe caber en el presupuesto de sesión; configuración inválida impide arrancar. Se conserva el modelo reportado por el proveedor y consumo conocido/desconocido de cada turno. Los tokens de contexto usan la misma cota conservadora de bytes existente.

## Configuración de imágenes

En `/admin/imagenes`, activar «Modelos por tarea» y configurar borrador, detalle y edición, resolución, soporte de referencias y reserva por intento. La API administrativa existente `PUT /api/visual-settings` acepta `configuration.routing` con `enabled`, `draft`, `detailed`, `edit` y usa `expectedVersion`. La configuración está versionada en la base de datos.

Edición tiene prioridad cuando existe una propuesta padre; referencias, varios colores o personalización mecánica compleja usan detalle; el resto borrador. La selección no altera el prompt público ni envía costos/configuración interna al modelo. El worker usa la ficha y revisión congeladas de la propuesta, incluso si después cambia el modelo del panel; el interruptor global de imágenes sigue pudiendo detener trabajos. Reintentos usan la misma ruta y reservas existentes. Los valores iniciales de reserva de imagen (0.10/0.25 USD por intento) son presupuestos conservadores configurables, no tarifas garantizadas del proveedor.

## Validación

`dotnet run --project tests/ManufacturingChecks --no-restore` cubre arquitectura, HTTP, selección y payload del proveedor, reservas por ruta, rechazo por presupuesto, configuración inválida y rutas de imágenes. No realiza llamadas pagadas ni verifica calidad real del catálogo. `dotnet run --project tests/SecurityChecks --no-restore` y `npm test` en frontend complementan estas pruebas.
