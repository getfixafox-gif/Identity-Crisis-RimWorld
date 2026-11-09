using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // Everything chosen in the setup window. Null style fields mean "by race rules".
    public class AssimilationConfig : IExposable
    {
        public ThingDef race;
        public Gender gender = Gender.None;
        public float ageYears = -1f;
        public BodyTypeDef bodyType;
        public HeadTypeDef headType;
        public HairDef hair;
        public BeardDef beard;
        public bool overrideHairColor;
        public Color hairColor = Color.white;

        public AssimilationConfig Copy()
        {
            return (AssimilationConfig)MemberwiseClone();
        }

        public void ExposeData()
        {
            Scribe_Defs.Look(ref race, "race");
            Scribe_Values.Look(ref gender, "gender", Gender.None);
            Scribe_Values.Look(ref ageYears, "ageYears", -1f);
            Scribe_Defs.Look(ref bodyType, "bodyType");
            Scribe_Defs.Look(ref headType, "headType");
            Scribe_Defs.Look(ref hair, "hair");
            Scribe_Defs.Look(ref beard, "beard");
            Scribe_Values.Look(ref overrideHairColor, "overrideHairColor", false);
            Scribe_Values.Look(ref hairColor, "hairColor", Color.white);
        }
    }
}
