using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    public class AssimilationPodExtension : DefModExtension
    {
        public int ticksToComplete = 60000;

        // Hemogen packs poured in per assimilation. Used only when the pod has a blood tank (Biotech).
        public int bloodPacksPerConversion = 5;

        // Consumers should snapshot these values when a procedure starts, not read
        // live settings every tick. Do not mutate the shared XML def to apply settings.
        public int GetTicksToComplete(IdentityCrisisSettings settings)
        {
            settings?.Sanitize();
            return Mathf.Max(1, Mathf.RoundToInt(ticksToComplete * (settings?.assimilationDurationMultiplier ?? 1f)));
        }

        // Only call for a pod with a blood tank; DLC-free pods still cost no blood.
        public int GetBloodPacksPerConversion(IdentityCrisisSettings settings)
        {
            settings?.Sanitize();
            return Mathf.Max(0, Mathf.CeilToInt(bloodPacksPerConversion * (settings?.bloodCostMultiplier ?? 1f)));
        }
    }
}
