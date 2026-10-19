# Dirección visual neón y reflejos

Objetivo: mejorar los cuatro niveles, el menú principal y las pausas sin reconstruir escenarios ni cambiar jugabilidad.

## Dirección

- Noche azul tinta #070A16, fucsia #FF267F, cian #23D8EF, violeta #7855F5, texto blanco hielo #EDF5FF. Conservar arte del protagonista y su tipografía; dar color a sus tubos y reflejos y añadir OVERDRIVE como rótulo.
- Botones del menú a la izquierda, paneles secundarios y pausa centrados, texto legible y foco visible. Brillo ambiental lento y barridos al seleccionar, sin destellos rápidos. El neón domina en el club; en la casa y el loft se reserva a luces y materiales concretos.
- Nivel 1 se denomina **La casa**, de acuerdo con su escenario e historia.

## Implementación

1. Inspeccionar las escenas, materiales, luces y configuración URP reales; capturar referencia visual.
2. Materiales de escena derivados que conservan texturas, normales y transparencia. Mejorar respuesta a luz y rugosidad en pisos y metales; mantener materiales de personajes/efectos y originales importados.
3. Reflection Probes de entorno por zonas, iluminación y postprocesado moderados. Reflejos estáticos horneados; no prometer reflejos de personajes en movimiento. Presupuesto acotado para la autopista y las luces adicionales.
4. Material UI neón y tema compartido para menús existentes; preservar botones, eventos, navegación, pausa, guardado, audio y pantalla completa. Hooks en constructores para conservar el acabado al regenerar.
5. Verificar escena y shaders, ejecutar pruebas Play Mode de menús, pausa y escenas, y revisar capturas. Medir tiempos de fotograma con y sin el acabado bajo condiciones equivalentes; reportar límites de la medición del editor.

## Ampliación solicitada: HUD y controles

- Mantener los datos reales de GameHUD y ChaseHUD: vida, munición, puntuación, enemigos, rebobinados, oleadas y distancia. Compartir paneles oscuros con acentos cian/fucsia y barras segmentadas, incluido aviso de vida baja.
- Guía de controles persistente en cada nivel; controles de conducción distintos de combate. Tras la revisión del usuario, todos los datos del HUD usan Liberation Sans para lectura rápida; Horrendo se conserva en los rótulos del suelo y el título del menú.
- Reservar espacio entre la guía y los diálogos. Escalar por ancho y alto, sin interferir con pausa ni pantallas de victoria.
- Todos los rótulos 3D usan Horrendo; copiar el atlas TMP a un recurso local para preservar el original importado.

No actualizar paquetes, hacer commits ni publicar. Documentar controles del acabado y fuentes de verdad en docs y memoria.
