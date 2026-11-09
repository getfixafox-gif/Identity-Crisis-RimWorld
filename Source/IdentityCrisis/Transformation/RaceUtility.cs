using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    public static class RaceUtility
    {
        private static List<ThingDef> cachedRaces;

        public static readonly List<Color> HairPalette = new List<Color>
        {
            new Color(0.05f, 0.05f, 0.05f),
            new Color(0.17f, 0.12f, 0.08f),
            new Color(0.45f, 0.3f, 0.17f),
            new Color(0.85f, 0.72f, 0.45f),
            new Color(0.65f, 0.25f, 0.12f),
            new Color(0.75f, 0.75f, 0.75f),
            new Color(0.95f, 0.95f, 0.95f),
            new Color(0.35f, 0.55f, 0.85f),
            new Color(0.45f, 0.75f, 0.4f),
            new Color(0.6f, 0.35f, 0.75f)
        };

        public static List<ThingDef> HumanlikeRaces
        {
            get
            {
                if (cachedRaces == null)
                {
                    cachedRaces = DefDatabase<ThingDef>.AllDefsListForReading
                        .Where(d => d.category == ThingCategory.Pawn && d.race != null && d.race.Humanlike)
                        .OrderBy(d => d.label)
                        .ToList();
                }
                return cachedRaces;
            }
        }

        public static ThingDef DefaultTargetRace(Pawn pawn)
        {
            return HumanlikeRaces.FirstOrDefault(r => r != pawn.def);
        }

        public static float LifeExpectancy(ThingDef race)
        {
            return race?.race != null && race.race.lifeExpectancy > 0f ? race.race.lifeExpectancy : 80f;
        }

        public static float AdultMinAge(ThingDef race)
        {
            List<LifeStageAge> stages = race?.race?.lifeStageAges;
            if (stages.NullOrEmpty())
            {
                return 18f;
            }
            return stages[stages.Count - 1].minAge;
        }

        public static float MaxAge(ThingDef race)
        {
            return Mathf.Max(AdultMinAge(race) + 1f, LifeExpectancy(race) * 1.25f);
        }

        // Call before the transformation, while pawn.def is still the old race.
        public static float ProportionalAge(Pawn pawn, ThingDef race)
        {
            float age = pawn.ageTracker.AgeBiologicalYearsFloat * LifeExpectancy(race) / LifeExpectancy(pawn.def);
            return Mathf.Clamp(age, AdultMinAge(race), MaxAge(race));
        }

        public static bool GenderLocked(ThingDef race, out Gender fixedGender)
        {
            fixedGender = Gender.None;
            if (race?.race == null)
            {
                return false;
            }
            if (!race.race.hasGenders)
            {
                return true;
            }
            float? male = HarCompat.MaleGenderProbability(race);
            if (male.HasValue)
            {
                if (male.Value <= 0f)
                {
                    fixedGender = Gender.Female;
                    return true;
                }
                if (male.Value >= 1f)
                {
                    fixedGender = Gender.Male;
                    return true;
                }
            }
            return false;
        }

        public static List<BodyTypeDef> AllowedBodyTypes(ThingDef race, Gender gender)
        {
            List<BodyTypeDef> list = HarCompat.BodyTypes(race);
            if (list.NullOrEmpty())
            {
                list = new List<BodyTypeDef> { BodyTypeDefOf.Male, BodyTypeDefOf.Female, BodyTypeDefOf.Thin, BodyTypeDefOf.Fat, BodyTypeDefOf.Hulk };
            }
            List<BodyTypeDef> result = list
                .Where(b => b != null && b != BodyTypeDefOf.Child && b != BodyTypeDefOf.Baby)
                .Where(b => !(gender == Gender.Male && b == BodyTypeDefOf.Female) && !(gender == Gender.Female && b == BodyTypeDefOf.Male))
                .Distinct()
                .ToList();
            return result.Count > 0 ? result : list.Where(b => b != null).Distinct().ToList();
        }

        public static List<HeadTypeDef> AllowedHeadTypes(ThingDef race, Gender gender)
        {
            List<HeadTypeDef> list = HarCompat.HeadTypes(race);
            if (list.NullOrEmpty())
            {
                list = DefDatabase<HeadTypeDef>.AllDefsListForReading.Where(h => h.randomChosen).ToList();
            }
            List<HeadTypeDef> result = list
                .Where(h => h != null && (h.gender == Gender.None || h.gender == gender) && h.requiredGenes.NullOrEmpty())
                .Distinct()
                .ToList();
            return result.Count > 0 ? result : list.Where(h => h != null).Distinct().ToList();
        }

        public static List<HairDef> AllHair()
        {
            return DefDatabase<HairDef>.AllDefsListForReading.OrderBy(h => h.label).ToList();
        }

        public static List<BeardDef> AllBeards()
        {
            return DefDatabase<BeardDef>.AllDefsListForReading.OrderBy(b => b.label).ToList();
        }

        public static bool HasHair(ThingDef race)
        {
            return HarCompat.HasStyle(race, typeof(HairDef));
        }

        public static bool HasBeard(ThingDef race, Gender gender)
        {
            return gender != Gender.Female && HarCompat.HasStyle(race, typeof(BeardDef));
        }

        public static AssimilationConfig DefaultConfig(Pawn pawn, ThingDef race)
        {
            AssimilationConfig config = new AssimilationConfig { race = race, gender = pawn.gender };
            ApplyRaceDefaults(config, pawn);
            return config;
        }

        // Re-validates gender, age and style after the race changes.
        public static void ApplyRaceDefaults(AssimilationConfig config, Pawn pawn)
        {
            if (config.race == null)
            {
                return;
            }
            if (GenderLocked(config.race, out Gender fixedGender))
            {
                config.gender = fixedGender;
            }
            else if (config.gender == Gender.None)
            {
                config.gender = pawn.gender == Gender.None ? Gender.Male : pawn.gender;
            }
            config.ageYears = ProportionalAge(pawn, config.race);
            Validate(config);
        }

        public static void Validate(AssimilationConfig config)
        {
            if (config.race == null)
            {
                return;
            }
            if (config.bodyType != null && !AllowedBodyTypes(config.race, config.gender).Contains(config.bodyType))
            {
                config.bodyType = null;
            }
            if (config.headType != null && !AllowedHeadTypes(config.race, config.gender).Contains(config.headType))
            {
                config.headType = null;
            }
            if (!HasHair(config.race))
            {
                config.hair = null;
            }
            if (!HasBeard(config.race, config.gender))
            {
                config.beard = null;
            }
            config.ageYears = Mathf.Clamp(config.ageYears, AdultMinAge(config.race), MaxAge(config.race));
        }
    }
}
