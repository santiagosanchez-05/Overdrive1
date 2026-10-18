using UnityEngine;

namespace Overdrive.Story
{
    [CreateAssetMenu(menuName = "Overdrive/Contenido del final")]
    public sealed class EndingContent : ScriptableObject
    {
        [TextArea(2, 5)] public string[] epilogue;
        [Min(1f)] public float cardSeconds = 6f;
        [TextArea(12, 30)] public string credits;
        [Min(10f)] public float minimumCreditsSeconds = 28f;
        public AudioClip music;
    }
}
