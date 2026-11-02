# Modelos paramétricos y laminado (fase 5)

La API genera STL deterministas para cajas abiertas, placas rectangulares lisas y organizadores abiertos con divisiones ortogonales. El cliente acepta explícitamente la plantilla y revisa el STL real antes de solicitar cotización. No se interpretan instrucciones libres como código ni se ejecutan modelos o perfiles del cliente. Personalizaciones, varios colores y categorías no soportadas requieren revisión manual.

## Despliegue del trabajador

El laminado viene desactivado. La generación y descarga de modelos y la solicitud de revisión manual funcionan sin Docker.

1. Valida el MVP y prepara un perfil **PrusaSlicer 2.x FFF en INI** para cada combinación de equipo/material. Exporta la configuración completa, verifica dimensiones de cama, boquilla, densidad, paredes, relleno, velocidades, soportes y G-code personalizado. Los parámetros de geometría se validan también contra fabricación. El adaptador no admite perfiles 3.x JSON ni salida binaria BGCODE.
2. Compila `workers/slicer/Dockerfile` con una imagen Debian aprobada fijada por digest (`BASE_IMAGE`) y una versión exacta del paquete `prusa-slicer` (`PRUSA_PACKAGE_VERSION`). Publica la imagen en tu registro y usa su digest, nunca una etiqueta mutable. El ejecutable debe devolver exactamente la versión configurada al ejecutar `prusa-slicer --version`.
3. Prueba el contenedor con modelos conocidos, compara su resultado con el laminador de escritorio y con impresiones reales. Verifica específicamente si los gramos reportados incluyen las purgas del G-code inicial. `TotalsVerified` expresa esa revisión del operador; **no** es una certificación automática. `UnreportedPurgeGrams` solo corrige purga comprobada que no esté incluida. Déjalo en cero cuando el total ya la incluya. Soportes, extrusores y torre no se vuelven a sumar al total.
4. Guarda el INI privado en el servidor, calcula SHA-256 sobre sus bytes UTF-8 exactos y configura `Parametric:Profiles`. Los caminos y perfiles nunca se reciben del cliente ni se envían a OpenRouter. Solo se copia el INI verificado al directorio temporal del trabajo; no se monta la carpeta original.
5. Configura `Parametric:Enabled=true` en API y trabajador. Habilita `Parametric:RunWorker=true` **solo en una instancia dedicada** con acceso local a Docker y PostgreSQL. Las réplicas API mantienen `RunWorker=false` y no necesitan acceso al socket Docker. La cola y los STL viven en PostgreSQL; no dependen del almacenamiento temporal del contenedor. La instancia dedicada usa el mismo arranque de backend, secretos de aplicación y configuración que las otras instancias, con su API accesible solo por la red interna.

Ejemplo de configuración privada (reemplaza todas las referencias; no se entrega un perfil de producción sin validar):

```json
{
  "Parametric": {
    "Enabled": true,
    "RunWorker": false,
    "TimeoutSeconds": 120,
    "MaxPerConversation": 10,
    "MaxPerUserDay": 10,
    "Profiles": [{
      "Id": "perfil-validado-v1",
      "PrinterId": "identificador-del-catalogo",
      "MaterialId": "pla",
      "ProfilePath": "/etc/andeth/profiles/validated.ini",
      "ProfileSha256": "SHA256_DE_64_CARACTERES",
      "Image": "registro/andeth-slicer@sha256:DIGEST_DE_64_CARACTERES",
      "EngineVersion": "SALIDA_EXACTA_DE_PRUSASLICER_2_X",
      "TotalsVerified": true,
      "UnreportedPurgeGrams": 0,
      "DesignHours": 0,
      "FinishingHours": 0
    }]
  }
}
```

Docker debe usar un daemon local: los bind mounts son rutas del host del trabajador, no de un daemon remoto. El contenedor corre sin red, como usuario sin privilegios, sin capacidades, con raíz de solo lectura y sin socket Docker. Límites fijos: una CPU, 768 MiB RAM sin swap adicional, 64 procesos, tmpfs de 256 MiB y salida G-code de hasta 128 MB. El host limita tiempo a 30–240 segundos y stdout/stderr a 8192 caracteres. El proceso interno tiene también timeout para finalizar contenedores huérfanos si el host cae. Al finalizar se elimina el contenedor y los dos archivos temporales exactos; no se hacen borrados recursivos sobre rutas del usuario.

La carga de STL se hace solo desde la plantilla del servidor. Los perfiles rechazan hooks `post_process`, host de impresión y claves de impresión no vacíos. El G-code no se entrega ni se manda a una impresora; solo se extraen métricas. Los errores no exponen trazas ni perfiles al cliente.

## Versiones, cotización y validación

- `orthogonal-v1` genera una malla cerrada de superficies de la unión de celdas, sin caras internas. Los tests comprueban aristas compartidas, orientación, volumen, dimensiones y bytes reproducibles de cajas, placas y organizadores. No equivale a certificar resistencia o tolerancias de fabricación.
- Se conservan ficha, parámetros, STL/hash, versión de plantilla, INI/hash, imagen por digest, versión de motor, identidad de configuración efectiva y revisión de fabricación. La identidad reproducible incluye esos artefactos; no garantiza que tiempos de pared ni G-code con timestamps sean idénticos. Solo comparamos estadísticas del mismo motor y configuración.
- Las dimensiones se validan en orientación fija (base abajo); no se rota automáticamente una caja para hacerla caber. Una pieza por laminado: el costo multiplica consumo y tiempo por cantidad, sin descuentos de lote. El perfil determina horas de diseño (una vez) y acabado (por pieza). La estimación aplica tarifas y margen del catálogo, rango comercial ±15% y mínimo de pedido. No es un intervalo estadístico. Envío e impuestos adicionales requieren revisión.
- La cola usa versiones de concurrencia y leases. Solicitudes repetidas con el mismo ID no generan otra llamada. Un timeout o lease vencido deriva a revisión, sin repetir un trabajo incierto. La ficha o configuración obsoleta bloquea la creación de una nueva cotización desde ese modelo. Las cotizaciones ya enviadas conservan el modelo original.
- `/admin/laminado` muestra estado, perfiles aprobados, identidad de reproducción y comparación de gramos/segundos reales. Cada resultado admite una medición inmutable con autor, fecha y notas. La confirmación de precio sigue siendo manual; no existe confirmación automática pendiente de evidencias.
- Migración: `20261102192506_ParametricSlicing`. Agrega trabajos, cuotas y referencia opcional desde cotizaciones. Los archivos son privados, con autorización por propietario (administrador en revisión) y `no-store`. Incluye los STL en respaldos y dimensionamiento de PostgreSQL; las cuotas acotan nuevas generaciones, pero la retención histórica debe planificarse.

## Comprobaciones

```text
dotnet run --project tests/ManufacturingChecks/ManufacturingChecks.csproj
dotnet run --project tests/SecurityChecks/SecurityChecks.csproj
npm --prefix frontend test
python -m unittest discover -s workers/slicer -p "test_*.py"
```

La suite usa un laminador simulado y SQLite; valida también el snapshot y genera SQL PostgreSQL. El parser usa fixtures del formato G-code 2.x. Estas pruebas no sustituyen ejecutar el contenedor aprobado con el perfil real, migrar PostgreSQL de prueba y contrastar impresiones antes del piloto comercial.
