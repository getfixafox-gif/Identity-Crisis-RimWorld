using Verse;

namespace IdentityCrisis
{
    // Severity drifts down every tick interval but never drops below the floor.
    // A floor of 0 lets the hediff disappear; forcedFloor applies to forced conversions.
    public class HediffCompProperties_FadeToFloor : HediffCompProperties
    {
        public float severityPerDay = -0.05f;
        public float floor;
        public float forcedFloor = -1f;

        public HediffCompProperties_FadeToFloor()
        {
            compClass = typeof(HediffComp_FadeToFloor);
        }

        public float FloorFor(ConversionType type)
        {
            return type == ConversionType.Forced && forcedFloor >= 0f ? forcedFloor : floor;
        }
    }

    public class HediffComp_FadeToFloor : HediffComp
    {
        public ConversionType conversionType;
        public float durationMultiplier = 1f;

        public HediffCompProperties_FadeToFloor Props => (HediffCompProperties_FadeToFloor)props;

        public float Floor => Props.FloorFor(conversionType);

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            float target = parent.Severity + severityAdjustment + Props.severityPerDay * delta / (60000f * UnityEngine.Mathf.Clamp(durationMultiplier, 0.25f, 4f));
            if (target < Floor)
            {
                target = Floor;
            }
            severityAdjustment = target - parent.Severity;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref conversionType, "conversionType", ConversionType.Voluntary);
            Scribe_Values.Look(ref durationMultiplier, "durationMultiplier", 1f);
        }
    }
}
