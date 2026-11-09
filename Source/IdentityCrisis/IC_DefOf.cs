using RimWorld;
using Verse;

namespace IdentityCrisis
{
    [DefOf]
    public static class IC_DefOf
    {
        public static ThingDef IC_AssimilationPod;

        public static HediffDef IC_ConversionRecord;
        public static HediffDef IC_PhantomDiscomfort;
        public static HediffDef IC_FormWashShock;
        public static HediffDef IC_GenderShift;
        public static HediffDef IC_AssimilationRecovery;

        public static ThoughtDef IC_PhantomDiscomfortMood;
        public static ThoughtDef IC_FormWashShockMood;
        public static ThoughtDef IC_GenderShiftMood;
        public static ThoughtDef IC_PurebloodDisdain;
        public static ThoughtDef IC_GenderLowered;
        public static ThoughtDef IC_GenderRaised;
        public static ThoughtDef IC_PartnerGenderChanged;

        public static TraitDef IC_Converted;

        public static JobDef IC_EnterAssimilationPod;
        public static JobDef IC_CarryToAssimilationPod;

        static IC_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(IC_DefOf));
        }
    }
}
