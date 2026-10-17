# Nivel 02 — Elysium

Abrir `Assets/00_Scenes/Level02.unity` y pulsar Play, o usar **Selección de niveles → NIVEL 02 · ELYSIUM** desde Menu.

## Recorrido

La partida comienza en el acceso de la discoteca, con un bate junto al jugador. Hay 14 guardias entre la pista, las terrazas, las barras, el pasillo y la zona de camerinos. El maletín está en camerinos; recogerlo abre la puerta de seguridad y activa siete refuerzos que buscan al jugador: cuatro en backstage y tres junto a la salida. Hay que eliminar a todos y volver a la salida de la entrada. Se reutilizan los controles y sistemas de Level01, incluida su velocidad de movimiento real (10), cámara, sigilo, armas, puntuación, música, pausa, guardado y rebobinado.

## Escenario y mantenimiento

- FBX y materiales originales conservados. La escena usa una copia recortada con variantes propias en `Assets/00_Scenes/Level02_Data`.
- Arquitectura con MeshCollider y muebles con BoxCollider. Marcos y escaleras conservan el hueco y los peldaños; una caja sobre esas piezas impediría pasar. Botellas, luces y efectos pequeños son decoración sin colisión.
- Los pisos superiores no jugables y techos se han retirado de esta copia. Las paredes mantienen su textura y una altura que permite ver el combate; la entreplanta tiene una abertura sobre el pasillo.
- Rampas con descansillos conectan las terrazas. Umbrales y losas de respaldo cubren separaciones entre las mallas importadas.
- Materiales URP con texturas originales, normales, emisión y brillo moderado; iluminación de ambiente, luces de club animadas, tiras neón, seis cables colgantes con bombillas y luces rosa/cian, bloom, ACES y viñeta suave.

**No ejecutar TopDown → Construir nivel para reconstruir Level02:** ese constructor corresponde a Level01.

Herramientas propias en **TopDown → Nivel 02**:

1. **Construir y verificar**: reconstruye la escena desde su definición, hornea navegación, conecta el menú y valida referencias/rutas. Sobrescribe cambios manuales guardados de Level02; editar también las herramientas si se quiere conservar un ajuste al reconstruir.
2. **Verificar escena**: comprueba sistemas, suelo físico, velocidad, referencias de enemigos y rutas completas desde el inicio.
3. **Capturas**: escribe vistas con el ángulo del juego en `Temp/Level02-*.png`.
4. **Prueba completa en Play Mode**: recorre físicamente las terrazas, escenario y camerinos; prueba combate, guardado/carga, refuerzos, rebobinado, victoria y pausa. Preserva la partida guardada y el récord previos.

La navegación del nivel se hornea con escalón máximo 0.25 m, altura 1.8 m y radio 0.45 m, conservando los ajustes globales del proyecto. **Usar el constructor del nivel para hornear**, porque el Bake estándar del inspector retomaría el escalón global de 0.75 m, superior al máximo de 0.3 m del jugador.

## Verificación realizada — 2026-10-17

Unity 6000.5.6f1. Validación de escena y rutas completa, más 34 comprobaciones correctas en Play Mode, incluida la ruta de regreso con la puerta abierta. Resultado en `docs/level02-validation.txt`. La escena guarda 39 MeshCollider y 200 BoxCollider, además de los colliders de los prefabs. Se revisaron capturas con la cámara del juego. La prueba física aísla el combate para hacerlo reproducible; la dificultad frente a toda la IA activa queda para la prueba de juego del equipo.

No se cambiaron Level01, los prefabs compartidos, paquetes ni versión del editor. No se creó ningún commit.

Ajuste posterior: GroundProbe reconoce pisos MeshCollider y BoxCollider. La sangre sigue su normal y reduce su tamaño junto a desniveles; se verifican alturas negativas, pendientes y bordes con CheckBloodSurfaces. Resultado en docs/blood-surface-validation.txt.

Los Hanging Neon tienen postes metálicos, bases y abrazaderas en cada anclaje (18 soportes). Se ajustó la separación de los cables para mantener todos los extremos sobre superficies existentes. Herramienta: TopDown → Nivel 02 → Ajustar anclajes de neón.
