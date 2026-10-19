# Acabado neón, menús y HUD

Aplicado a Menu y a Level01–Level04 sin reconstruir los niveles. Se conserva URP 17.5, Forward+ y el SSAO existente. No se modificaron paquetes, NavMesh, colliders ni el flujo hacia Ending.

## Acabado de los niveles

Cada escena tiene un objeto **Visual Finish** con:

- Materiales derivados para pisos, madera, metales y paredes compatibles. Conservan shader, texturas base, normales y emisión del material original. La máscara de rugosidad da variación al acabado de los pisos; las alfombras, personajes y efectos conservan sus materiales.
- Capturas de reflejos de entorno a 128 px por cara: 6 en la casa, 5 en Elysium, 4 nuevas en el loft y 6 en la autopista. El loft conserva también su sonda original.
- Un perfil global con Bloom moderado, ACES, contraste, saturación y viñeta suaves. La casa y el loft tienen luces de relleno sin sombras.

Desactivar **Visual Finish** restaura los materiales originales y desactiva sus sondas, luces y volumen para comparar. Los ajustes de los materiales compartidos de vehículos son independientes de ese interruptor.

Los reflejos son capturas estáticas del entorno: no muestran personajes o coches moviéndose en tiempo real. Se guardan como EXR y se asignan mediante **Reflection Probe / Custom / Cubemap**, para conservarlos al volver a abrir la escena.

Recursos en `Assets/04_Materials/NeonFinish`: perfiles `LevelXX_Grade.asset`, materiales derivados en `Materials`, cubemaps en `Probes`, máscara `FloorRoughness.asset` y fuentes locales. Se mantienen los materiales importados originales. Los materiales propios de coches `M_PlayerCar`, `M_Hilux` y `M_HiluxBlindada` reciben brillo moderado.

## Menús

El menú conserva el arte del protagonista, ahora con luces y reflejos cian/fucsia y movimiento ambiental lento. El rótulo OVERDRIVE usa una copia local de Horrendo. Botones, selección de niveles, ajustes y pausas comparten paleta y foco visible. Se conservan eventos, navegación, audio y pantalla completa.

El nivel 1 se llama **La casa**. Su nombre se edita en `MenuBuilder.LevelEntries` y en la etiqueta correspondiente de MainMenuController si se ajusta directamente la escena.

El color de cada botón se puede ajustar en **NeonMenuButton / Colores**. El ambiente usa `MenuArtwork.mat`, `MenuAtmosphere.mat` y el shader `Overdrive/UI/Neon Atmosphere`. `NeonMenuAtmosphere` crea y libera su propia instancia de material y anima con tiempo no escalado, también en pausa.

## HUD y tipografía

GameHUD conserva la vida real del jugador, arma, munición, puntuación, enemigos y rebobinados. ChaseHUD conserva estado del coche, oleadas, bajas y distancia. Comparten paneles y barras segmentadas; por debajo del 25 % la barra pasa a fucsia. Las guías de controles permanecen visibles y el diálogo reserva espacio encima de ellas.

- Combate: WASD, ratón, clic izquierdo, E, clic derecho, espacio, Ctrl, Shift, V y Esc. T se indica junto al contador de rebobinados; R se indica al morir.
- Conducción: A/D, W/S, ratón, clic izquierdo y Esc.

Los rótulos del suelo y demás textos 3D usan **Horrendo**: TextMesh emplea `horrendo.ttf`; TextMeshPro emplea la copia local `WorldHorrendo.asset`. Se conserva el área de cada cartel y se permite ajustar el tamaño de TMP al texto. Todos los datos del HUD usan **Liberation Sans**, por petición del usuario después de probar su legibilidad. La guía de teclas también usa una fuente sencilla.

El contenido y dibujo compartido están en `NeonHudGraphics`; el HUD de combate y conducción sigue en `GameHUD` y `ChaseHUD`. `objectiveHint` en GameHUD permite editar el objetivo inicial por escena.

## Herramientas y verificación

Menú de Unity **TopDown → Acabado neón**:

- **Aplicar a los cuatro niveles y menús**: vuelve a generar el acabado, incluidos los reflejos. Requiere escenas guardadas y Play Mode detenido.
- **Actualizar HUD, tipografía y menús**: actualiza únicamente esa interfaz, sin rehacer reflejos.
- **Verificar escenas y tipografía**: comprueba referencias, materiales, capturas persistentes, fuentes y shader UI.
- **Probar HUD, pausa y menús**: prueba las cuatro escenas, navegación, daño real al 20 %, pausa y animación con el tiempo detenido. Conserva/restaura partida, preferencia de cámara y TimeManager. Desactiva temporalmente IA para aislar la interfaz; no sustituye una partida completa.

Los constructores de niveles y menús reaplican el tema si los recursos ya están instalados. No es necesario reconstruir los niveles para probar este cambio.

Capturas y resultados de ejecución: `Temp/NeonShots`, `Temp/NeonPolish.validation.txt` y `Temp/NeonPolish.playtest.txt`. La comparación de rendimiento usa fotogramas del editor con la cámara quieta en Level02 y 90 muestras por estado tras calentamiento; no equivale a una medición independiente de GPU ni a un benchmark de un ejecutable final.

## Nina y guardado de campaña

Nina en Level02 queda a unos **1,3 metros** del jugador, a un lado del pasillo inicial. Su cápsula queda separada de las paredes y de la reja de seguridad; el centro del corredor permanece libre. La posición se mantiene también al regenerar la historia con StoryFactory. **TopDown → Nivel 02 → Acercar a Nina al pasillo de entrada** aplica únicamente este ajuste.

El guardado de campaña funciona por **inicio de nivel**. JUGAR y los botones de selección escriben un checkpoint del nivel elegido, y cada victoria escribe el siguiente. CONTINUAR empieza ese nivel con su estado inicial; no se ofrece guardado manual de la partida a mitad del combate. La victoria de Level04 guarda Ending, y volver al menú desde el cierre elimina el checkpoint de la campaña terminada.

Se corrigió un caso donde JUGAR cargaba Level01 pero conservaba un checkpoint anterior: salir al menú podía hacer que CONTINUAR regresara a otra campaña. El checkpoint se actualiza ahora en la misma acción que carga el nivel.

**TopDown → Pruebas → Campaña, guardado y Continuar** prueba el archivo real y los botones del menú, las victorias de los tres primeros niveles, refuerzos, ascensor y fases del Jefe, victoria de la autopista, créditos y limpieza final. También recorre físicamente el paso junto a Nina. Usa muertes y desplazamientos controlados para aislar las transiciones; no equivale a jugar toda la campaña con teclado. Restaura la partida, récords, preferencia de cámara, TimeManager y escena previos. Resultado: `Temp/CampaignSave.playtest.txt`.

Validación del 18 de octubre de 2026: **23 comprobaciones de UI y 59 de campaña/guardado correctas**, sin errores registrados durante las pruebas. Informes conservados en `docs/neon-ui-validation.txt` y `docs/campaign-save-validation.txt`. Validación adicional de las cuatro escenas, materiales, shader y tipografía correcta; Nina a 1,29 m. Se revisaron capturas de HUD normal/vida baja, diálogos, pausas, menú y selección 4:3. Los colliders de los niveles conservan sus cantidades originales: 389, 292, 131 y 552.
