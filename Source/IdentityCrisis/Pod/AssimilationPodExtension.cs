using Verse;

namespace IdentityCrisis
{
    public class AssimilationPodExtension : DefModExtension
    {
        public int ticksToComplete = 60000;

        // Hemogen packs poured in per assimilation. Used only when the pod has a blood tank (Biotech).
        public int bloodPacksPerConversion = 5;
    }
}
