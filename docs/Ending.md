# Final de Overdrive

Flujo: Level04 (victoria en el piso franco) → Espacio/Enter → Ending (epílogo → créditos → agradecimiento) → Menu.

- El cierre es una escena aparte: `Assets/00_Scenes/Ending.unity`. Se conserva el diálogo final de Nina y la jugabilidad de la persecución.
- El epílogo tiene tres tarjetas de seis segundos. Los créditos se desplazan automáticamente y respetan el volumen de música existente. La pantalla final espera al jugador.
- Espacio/Enter avanza; Escape pasa del epílogo a los créditos y de los créditos al agradecimiento. Hay botones para ver/finalizar créditos y volver al menú. Al salir se funden imagen y música.
- El checkpoint de victoria permite usar CONTINUAR para volver al final si se cierra antes de verlo. Al regresar al menú desde el cierre se elimina el checkpoint de la campaña completada; los récords y ajustes se conservan.

## Editar textos y nombres

Seleccionar `Assets/00_Scenes/Ending_Data/EndingContent.asset` en el inspector. Contiene epílogo, duración de tarjetas, créditos con etiquetas TextMesh Pro, duración mínima y música. Cambiar los textos no requiere reconstruir la escena. El equipo aparece en el orden proporcionado por el usuario y sin asignar roles individuales no especificados. Agradecimientos: sus chatcitos, Claudes y Antigravity.

## Herramientas

- **TopDown → Final → Crear o actualizar cierre y créditos**: reconstruye únicamente Ending, mantiene su asset de contenido existente, conecta el destino de Level04 y añade Ending a Build Settings. Guardar las escenas antes de usarlo.
- **TopDown → Final → Probar victoria, cierre y regreso al menú**: prueba real en Play Mode. Conserva/restaura el guardado del usuario y vuelve a la escena anterior. Resultado en `Temp/Ending.playtest.txt`.
- `EndingBuilder.Verify()` verifica conexión y lógica de temporización/saltos sin jugar la campaña.

## Comprobaciones

Se reprodujo inicialmente la ausencia del cierre (Level04 todavía apuntaba a Menu). Tras implementarlo, verificación de la secuencia y 25 comprobaciones en Play Mode correctas. Capturas a 1600×900 y 1280×960; reporte durable `docs/ending-validation.txt`. No se reconstruyeron los niveles ni se cambiaron paquetes. Los recursos audiovisuales existentes mantienen sus archivos originales.

La tipografía del título usa Ending_Data/EndingTitle.asset, copia local del recurso existente para aislar las actualizaciones del atlas TMP. El resto de pantallas conserva su fuente original.
