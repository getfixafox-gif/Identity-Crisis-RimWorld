using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    public class IdentityCrisisSettings : ModSettings
    {
        public const float MinDurationMultiplier = 0.25f;
        public const float MaxDurationMultiplier = 4f;
        // The vanilla mod tank holds ten packs; its default conversion cost is five.
        public const float MaxBloodCostMultiplier = 2f;
        public const float MaxPodEffectIntensity = 2f;

        public float convincedSlaveMinSuppression = 0.7f;
        public float assimilationDurationMultiplier = 1f;
        public float bloodCostMultiplier = 1f;
        public float recoveryDurationMultiplier = 1f;
        public bool allowRejuvenation = true;
        public bool allowRegrowBodyParts = true;
        public bool allowCureDiseases = true;
        public float podEffectIntensity = 1f;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref convincedSlaveMinSuppression, "convincedSlaveMinSuppression", 0.7f);
            Scribe_Values.Look(ref assimilationDurationMultiplier, "assimilationDurationMultiplier", 1f);
            Scribe_Values.Look(ref bloodCostMultiplier, "bloodCostMultiplier", 1f);
            Scribe_Values.Look(ref recoveryDurationMultiplier, "recoveryDurationMultiplier", 1f);
            Scribe_Values.Look(ref allowRejuvenation, "allowRejuvenation", true);
            Scribe_Values.Look(ref allowRegrowBodyParts, "allowRegrowBodyParts", true);
            Scribe_Values.Look(ref allowCureDiseases, "allowCureDiseases", true);
            Scribe_Values.Look(ref podEffectIntensity, "podEffectIntensity", 1f);
            Sanitize();
        }

        public void Sanitize()
        {
            convincedSlaveMinSuppression = SafeClamp(convincedSlaveMinSuppression, 0f, 1f, 0.7f);
            assimilationDurationMultiplier = SafeClamp(assimilationDurationMultiplier, MinDurationMultiplier, MaxDurationMultiplier, 1f);
            bloodCostMultiplier = SafeClamp(bloodCostMultiplier, 0f, MaxBloodCostMultiplier, 1f);
            recoveryDurationMultiplier = SafeClamp(recoveryDurationMultiplier, MinDurationMultiplier, MaxDurationMultiplier, 1f);
            podEffectIntensity = SafeClamp(podEffectIntensity, 0f, MaxPodEffectIntensity, 1f);
        }

        private static float SafeClamp(float value, float min, float max, float fallback)
        {
            return float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        }

        public void ResetToDefaults()
        {
            convincedSlaveMinSuppression = 0.7f;
            assimilationDurationMultiplier = 1f;
            bloodCostMultiplier = 1f;
            recoveryDurationMultiplier = 1f;
            allowRejuvenation = true;
            allowRegrowBodyParts = true;
            allowCureDiseases = true;
            podEffectIntensity = 1f;
        }
    }

    public class IdentityCrisisMod : Mod
    {
        public static IdentityCrisisSettings Settings;
        private Vector2 settingsScrollPosition;
        private float settingsContentHeight = 1100f;

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
            Settings.Sanitize();
            Rect viewRect = new Rect(0f, 0f, Mathf.Max(1f, inRect.width - 20f), settingsContentHeight);
            Widgets.BeginScrollView(inRect, ref settingsScrollPosition, viewRect);
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(viewRect);
            listing.Label("IC_SettingsV03Construction".Translate());
            listing.GapLine();
            listing.Label("IC_SettingsV03Timing".Translate());
            Settings.assimilationDurationMultiplier = MultiplierSlider(listing, "IC_SettingsV03AssimilationDuration", "IC_SettingsV03AssimilationDurationDesc", Settings.assimilationDurationMultiplier, IdentityCrisisSettings.MinDurationMultiplier, IdentityCrisisSettings.MaxDurationMultiplier);
            Settings.bloodCostMultiplier = MultiplierSlider(listing, "IC_SettingsV03BloodCost", "IC_SettingsV03BloodCostDesc", Settings.bloodCostMultiplier, 0f, IdentityCrisisSettings.MaxBloodCostMultiplier);
            Settings.recoveryDurationMultiplier = MultiplierSlider(listing, "IC_SettingsV03RecoveryDuration", "IC_SettingsV03RecoveryDurationDesc", Settings.recoveryDurationMultiplier, IdentityCrisisSettings.MinDurationMultiplier, IdentityCrisisSettings.MaxDurationMultiplier);
            listing.GapLine();
            listing.Label("IC_SettingsV03Medical".Translate());
            listing.CheckboxLabeled("IC_SettingsV03AllowRejuvenation".Translate(), ref Settings.allowRejuvenation, "IC_SettingsV03AllowRejuvenationDesc".Translate());
            listing.CheckboxLabeled("IC_SettingsV03AllowRegrowBodyParts".Translate(), ref Settings.allowRegrowBodyParts, "IC_SettingsV03AllowRegrowBodyPartsDesc".Translate());
            listing.CheckboxLabeled("IC_SettingsV03AllowCureDiseases".Translate(), ref Settings.allowCureDiseases, "IC_SettingsV03AllowCureDiseasesDesc".Translate());
            listing.GapLine();
            Settings.podEffectIntensity = MultiplierSlider(listing, "IC_SettingsV03PodEffects", "IC_SettingsV03PodEffectsDesc", Settings.podEffectIntensity, 0f, IdentityCrisisSettings.MaxPodEffectIntensity);
            listing.GapLine();
            float value = listing.SliderLabeled(
                "IC_SettingsConvincedSuppression".Translate(Settings.convincedSlaveMinSuppression.ToStringPercent()),
                Settings.convincedSlaveMinSuppression, 0f, 1f, 0.6f,
                "IC_SettingsConvincedSuppressionDesc".Translate());
            Settings.convincedSlaveMinSuppression = Mathf.Round(value * 100f) / 100f;
            listing.Gap();
            listing.Label("IC_SettingsV03Policy".Translate());
            if (listing.ButtonText("IC_SettingsV03Reset".Translate()))
            {
                Settings.ResetToDefaults();
            }
            settingsContentHeight = listing.CurHeight + 20f;
            listing.End();
            Widgets.EndScrollView();
        }

        private static float MultiplierSlider(Listing_Standard listing, string labelKey, string descriptionKey, float value, float min, float max)
        {
            float result = listing.SliderLabeled(labelKey.Translate(value.ToStringPercent()), value, min, max, 0.6f, descriptionKey.Translate());
            return Mathf.Round(result * 100f) / 100f;
        }
    }
}
