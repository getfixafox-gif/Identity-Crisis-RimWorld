using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    public class IdentityCrisisSettings : ModSettings
    {
        public float convincedSlaveMinSuppression = 0.7f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref convincedSlaveMinSuppression, "convincedSlaveMinSuppression", 0.7f);
        }
    }

    public class IdentityCrisisMod : Mod
    {
        public static IdentityCrisisSettings Settings;

        public IdentityCrisisMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<IdentityCrisisSettings>();
        }

        public override string SettingsCategory()
        {
            return "Identity Crisis";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            float value = listing.SliderLabeled(
                "IC_SettingsConvincedSuppression".Translate(Settings.convincedSlaveMinSuppression.ToStringPercent()),
                Settings.convincedSlaveMinSuppression, 0f, 1f, 0.6f,
                "IC_SettingsConvincedSuppressionDesc".Translate());
            Settings.convincedSlaveMinSuppression = Mathf.Round(value * 100f) / 100f;
            listing.End();
        }
    }
}
