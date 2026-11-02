# Evaluación independiente de generación 3D libre

Esta prueba está fuera de la API, del chat, de los trabajadores y de la confirmación de pedidos. No se conecta a un proveedor ni genera modelos. Permite evaluar muestras de candidatos antes de considerar una integración de producción. Los tests usan geometrías sintéticas; no demuestran la calidad de un proveedor real.

## Protocolo de piloto

1. Selecciona un conjunto representativo de cajas, organizadores, marcos y geometrías fuera de las plantillas. Conserva prompt, requisitos, dimensiones, proveedor/modelo, versión, fecha, costo y tiempo por generación. Repite requisitos para observar consistencia. No excluyas intentos fallidos del informe.
2. Exporta STL binario. Compara las medidas contra requisitos fijados antes de generar; no cambies la tolerancia después de ver el resultado.
3. Ejecuta el evaluador sobre el original. Si requiere reparación, hazla con una herramienta revisada, conserva ambos archivos y repite con `--repaired`. Registra tiempo y cambios de geometría. El evaluador no ejecuta código del archivo ni realiza reparación automática.
4. Revisa por separado autointersecciones, espesor mínimo, apariencia, orientación, soportes, resistencia esperada y compatibilidad con el perfil. Lamina en el entorno aislado de pruebas y contrasta con fabricación física. No envíes G-code generado libremente a producción.
5. Registra evidencia humana ligada al SHA-256 del archivo final. El resultado máximo del programa es `ReadyForHumanReview`; nunca habilita producción. La decisión de integrar un proveedor requiere revisar el conjunto completo y sus fallos, costo y trabajo manual, no solo un STL válido.

```text
python experiments/free3d/evaluate.py candidate.stl --expected-mm 100 80 60 --tolerance-mm 0.5 --output report.json
python experiments/free3d/evaluate.py candidate.stl --expected-mm 100 80 60 --tolerance-mm 0.5 --repaired repaired.stl --evidence evidence.json --output reviewed-report.json
python -m unittest discover -s experiments/free3d -p "test_*.py"
```

La evidencia usa `modelSha256`, `reviewer`, `reference`, `selfIntersectionsChecked`, `minimumWallChecked`, `slicingChecked`, `physicalPrintPassed` y, si hubo reparación, `repairedArtifactReviewed`. Los indicadores deben ser booleanos; son declaraciones del operador, no certificaciones automáticas.

Límites: STL binario de hasta 50.000 triángulos, coordenadas finitas acotadas, longitud exacta, análisis de dimensiones por eje, aristas, orientación, componentes, degeneración y volumen firmado. No detecta todas las autointersecciones ni prueba resistencia. No descarga URLs ni procesa formatos que contienen código. Los reportes se crean sin sobrescribir archivos existentes. Usa un entorno de evaluación separado y archivos de prueba sin datos personales.

El proyecto no contiene evidencia real del piloto: la habilitación de generación libre permanece pendiente de esas mediciones y revisión.
