using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // Everything chosen in the setup window. Null style fields mean "by race rules".
    public class AssimilationConfig : IExposable
    {
        // Persisted once per setup; the transformation planner resolves automatic choices from this seed.
        public int appearanceSeed;
        public bool policyCaptured;
        public bool allowRejuvenation = true;
        public bool allowRegrowBodyParts = true;
        public bool allowCureDiseases = true;
        public float recoveryMultiplier = 1f;
        public ThingDef race;
        public Gender gender = Gender.None;
        public float ageYears = -1f;
        public BodyTypeDef bodyType;
        public HeadTypeDef headType;
        public HairDef hair;
        public BeardDef beard;
        public bool overrideHairColor;
        public Color hairColor = Color.white;
        public bool overrideSkinColor;
        public Color skinColor = Color.white;
        // -1 means the race chooses the variant automatically.
        public List<int> addonVariants = new List<int>();

        public AssimilationConfig Copy()
        {
            AssimilationConfig copy = (AssimilationConfig)MemberwiseClone();
            copy.addonVariants = addonVariants != null ? new List<int>(addonVariants) : new List<int>();
            return copy;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref appearanceSeed, "appearanceSeed", 0);
            Scribe_Values.Look(ref policyCaptured, "policyCaptured", false);
            Scribe_Values.Look(ref allowRejuvenation, "allowRejuvenation", true);
            Scribe_Values.Look(ref allowRegrowBodyParts, "allowRegrowBodyParts", true);
            Scribe_Values.Look(ref allowCureDiseases, "allowCureDiseases", true);
            Scribe_Values.Look(ref recoveryMultiplier, "recoveryMultiplier", 1f);
            Scribe_Defs.Look(ref race, "race");
            Scribe_Values.Look(ref gender, "gender", Gender.None);
            Scribe_Values.Look(ref ageYears, "ageYears", -1f);
            Scribe_Defs.Look(ref bodyType, "bodyType");
            Scribe_Defs.Look(ref headType, "headType");
            Scribe_Defs.Look(ref hair, "hair");
            Scribe_Defs.Look(ref beard, "beard");
            Scribe_Values.Look(ref overrideHairColor, "overrideHairColor", false);
            Scribe_Values.Look(ref hairColor, "hairColor", Color.white);
            Scribe_Values.Look(ref overrideSkinColor, "overrideSkinColor", false);
            Scribe_Values.Look(ref skinColor, "skinColor", Color.white);
            Scribe_Collections.Look(ref addonVariants, "addonVariants", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && addonVariants == null) addonVariants = new List<int>();
        }
    }
}
