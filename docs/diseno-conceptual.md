# Diseño guiado y vista preliminar

El cliente solo necesita describir el objeto. El extractor de OpenRouter ahora propone los datos que falten (uso, medidas, cantidad, material, colores y personalización) y devuelve `suggestedFields`. La ficha conserva esa procedencia en su JSON; el frontend muestra «Sugerido» y la copia al formulario mantiene esa etiqueta. Los valores suministrados explícitamente no pueden ser sustituidos por una propuesta del modelo. Una corrección o confirmación explícita elimina la marca del campo correspondiente.

La clasificación de procedencia depende de la interpretación del modelo; las pruebas verifican el contrato y las reglas de mezcla, no la precisión semántica de proveedores reales. No se deben presentar estas medidas como medidas verificadas del objeto original ni como una escala exacta sin referencia.

La página `/crear` muestra tres pasos: describir, mirar/ajustar y solicitar revisión. La ficha técnica y las opciones avanzadas se pueden desplegar; no bloquean la exploración. Se conserva el formulario manual si la IA está deshabilitada.

## Imágenes

Una descripción de objeto basta para solicitar una vista conceptual, incluso sin medidas completas ni catálogo técnico configurado. Esto no cambia los controles de fabricación, estimación ni envío de propuestas técnicas. El prompt indica que se trata de una interpretación conceptual sin garantía de imprimibilidad. Si no hay categoría o material disponible, no se inventa disponibilidad comercial.

Con imágenes habilitadas, el navegador solicita una vista preliminar al recibir una ficha nueva. Si existe una imagen completada y el modelo admite referencias, la siguiente usa esa imagen como referencia. La API reutiliza la propuesta de la misma ficha para solicitudes automáticas, y mantiene bloqueos, reservas, cuotas, propiedad y reintentos acotados. Una imagen fallida no se regenera automáticamente en bucle: se puede reintentar manualmente. La creación automática requiere que la página esté abierta; el trabajo ya encolado continúa en el worker.

Las imágenes siguen necesitando clave OpenRouter, configuración habilitada en `/admin/imagenes` y worker operativo. Si falta esa configuración, el sistema muestra que la imagen no está disponible; no muestra imágenes falsas ni promete que una llamada al proveedor haya tenido éxito. Cada generación consume el presupuesto del negocio.

## Despliegue y verificación

Publicar backend y frontend. La migración `20261102205455_ConceptualPreviews` permite que una propuesta visual no tenga revisión de fabricación cuando todavía no existe catálogo. Los registros con una revisión previa la conservan. `suggestedFields` se guarda dentro del JSON existente, sin otra columna.

ManufacturingChecks incluye: preservar medidas explícitas, confirmar una sugerencia, crear un concepto incompleto con catálogo vacío, deduplicar la vista automática y ejecutar el worker sin confundir preview con validación técnica. Las pruebas usan proveedores simulados. Verificar con modelos reales la utilidad de las sugerencias y continuidad visual antes de ampliar cuotas. La compilación/inspección visual completa de Astro sigue pendiente en el entorno local donde no está instalada esa dependencia.
