# Reglas de arquitectura de AndEth3D

- Los controllers son únicamente adaptadores HTTP: rutas, autorización declarativa, binding/validación de entrada y delegación a un servicio de aplicación. Cada acción debe delegar su caso de uso y transformar el resultado a HTTP.
- No agregar reglas de negocio, consultas EF/LINQ, transacciones, acceso a archivos, llamadas a proveedores, cálculos, creación/modificación de entidades ni orquestación de casos de uso dentro de controllers.
- Los casos de uso y sus reglas viven en servicios. Los DTO de entrada/salida viven en Contracts; no declarar contratos dentro de controllers.
- Los servicios de aplicación no dependen de controllers ni de resultados MVC. Usar AppResult para resultados de casos de uso y la abstracción ICurrentActor para la identidad. La conversión a códigos HTTP y archivos pertenece a Http.
- Mantener autorización por propietario, versionado, idempotencia, cuotas y reservas en los casos de uso, incluso cuando un endpoint tenga atributos de autorización.
- Toda funcionalidad nueva debe respetar estas fronteras. Ejecutar las comprobaciones de arquitectura e integración al modificar endpoints o servicios.
- La configuración, tokens, nombres de equipos y costos internos no deben llegar a respuestas públicas ni al contexto de los modelos.
