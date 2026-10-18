namespace Overdrive.Story
{
    /// <summary>
    /// Nivel 4: la huida en coche. El protagonista conduce con Nina de copiloto y el dinero del Elysium en el maletero;
    /// los dealers los persiguen por la autopista hasta el piso franco. Nina habla durante toda la persecución.
    /// </summary>
    public static class ChaseLines
    {
        public static readonly string[] Intro =
        {
            "¡Arranca, arranca! Los del Elysium ya tienen nuestras matrículas.",
            "Tú conduce y dispara, yo te voy diciendo por dónde vienen. Apunta con el ratón y aprieta el gatillo.",
            "Se van a poner a los lados, por los carriles de fuera, y se te echarán encima. No les dejes acercarse.",
            "El piso franco está al final de la autopista. No pares por nada.",
        };

        /// <summary>Al empezar cada oleada (una línea por oleada, en orden).</summary>
        public static readonly string[] WaveStart =
        {
            "Ahí vienen las primeras. Dos camionetas, por detrás.",
            "Más luces en el retrovisor... tres esta vez.",
            "Esos son los de la nave del puerto. Se pegan mucho, ojo con los volantazos.",
            "Cuatro. Una a cada lado y otra detrás. Dispara a la que se acerque.",
            "Han avisado a todo el barrio. No dejes que te encierren contra el quitamiedos.",
            "Cinco. Cinco camionetas. Esto ya es personal.",
            "Esa blindada es la del jefe. Aguanta muchas balas: no dejes de dispararle.",
        };

        /// <summary>Al acabar con cada oleada.</summary>
        public static readonly string[] WaveClear =
        {
            "¡Ja! Así se hace. Sigue, sigue.",
            "Limpio. Respira, que esto no ha acabado.",
            "Buena puntería. Por cosas así te dejo conducir a ti.",
            "Esos no vuelven a vender nada en mi esquina.",
            "Vale, vale... me estás asustando un poco. Pero sigue.",
            "Ya casi no les quedan camionetas. Ni ganas.",
            "Se acabó. Sin el jefe no hay quien pague a nadie. Al piso franco.",
        };

        public static readonly string[] LowHealth =
        {
            "¡El coche no aguanta mucho más! Deja de recibir golpes.",
        };

        public static readonly string[] Finish =
        {
            "Ya está. Aparca ahí detrás, que no nos vean desde la calle.",
            "La mitad es mía, como quedamos. La otra mitad... úsala para que no tengan con qué volver.",
        };

        public static readonly string[] Wrecked =
        {
            "¡No! El motor... ¡sal del coche, sal!",
        };
    }
}
