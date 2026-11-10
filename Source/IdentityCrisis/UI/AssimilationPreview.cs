using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // A disposable, unspawned pawn is used only for the portrait. The real pawn is never modified.
    public sealed class AssimilationPreview
    {
        private Pawn preview;
        private string lastSignature;
        private string pendingSignature;
        private float changedAt;
        private bool failed;

        internal static string Signature(AssimilationConfig c)
        {
            return string.Join("|", new string[] {
                c.appearanceSeed.ToString(), c.race?.defName ?? "", c.gender.ToString(), c.ageYears.ToString("R", System.Globalization.CultureInfo.InvariantCulture),
                c.bodyType?.defName ?? "", c.headType?.defName ?? "", c.hair?.defName ?? "",
                c.beard?.defName ?? "", c.overrideHairColor.ToString(), ColorSignature(c.hairColor),
                c.overrideSkinColor.ToString(), ColorSignature(c.skinColor),
                c.addonVariants == null ? "" : string.Join(",", c.addonVariants.Select(x => x.ToString()).ToArray())
            });
        }

        private static string ColorSignature(Color color)
        {
            var culture = System.Globalization.CultureInfo.InvariantCulture;
            return string.Join(",", new[] { color.r.ToString("R", culture), color.g.ToString("R", culture),
                color.b.ToString("R", culture), color.a.ToString("R", culture) });
        }

        public RenderTexture Get(AssimilationConfig config, Pawn original, Vector2 size)
        {
            // This entry point accepts only the planner's resolved snapshot, never raw UI choices.
            if (config?.race == null || original == null) return null;
            // Do not show generated defaults as a prediction when the core has not resolved them.
            if (config.bodyType == null || config.headType == null ||
                (RaceUtility.HasHair(config.race) && config.hair == null) ||
                (RaceUtility.HasBeard(config.race, config.gender) && config.beard == null) ||
                (RaceUtility.HasHair(config.race) && !config.overrideHairColor) || !config.overrideSkinColor ||
                config.addonVariants == null || config.addonVariants.Any(v => v < 0) ||
                config.addonVariants.Count < HarCompat.Addons(config.race).Count) return null;
            string signature = Signature(config);
            if (signature != pendingSignature)
            {
                pendingSignature = signature;
                changedAt = Time.realtimeSinceStartup;
            }
            if (signature != lastSignature && Time.realtimeSinceStartup - changedAt >= 0.2f)
            {
                lastSignature = signature;
                preview = MakePawn(config, original);
                failed = preview == null;
            }
            if (preview == null || failed || lastSignature != signature) return null;
            Rand.PushState(config.appearanceSeed);
            try
            {
                return PortraitsCache.Get(preview, size, Rot4.South, renderHeadgear: false,
                    renderClothes: false, healthStateOverride: PawnHealthState.Mobile);
            }
            catch (Exception e)
            {
                failed = true;
                Log.Warning("[Identity Crisis] Appearance preview failed: " + e);
                return null;
            }
            finally { Rand.PopState(); }
        }

        private static Pawn MakePawn(AssimilationConfig config, Pawn original)
        {
            PawnKindDef kind = DefDatabase<PawnKindDef>.AllDefsListForReading.FirstOrDefault(k => k.race == config.race);
            if (kind == null) return null;
            Rand.PushState(config.appearanceSeed);
            try
            {
                var request = new PawnGenerationRequest(kind, forceGenerateNewPawn: true,
                    canGeneratePawnRelations: false, allowPregnant: false, allowFood: false,
                    allowAddictions: false, fixedBiologicalAge: config.ageYears,
                    fixedChronologicalAge: config.ageYears,
                    fixedGender: config.gender == Gender.None ? (Gender?)null : config.gender,
                    forceNoIdeo: true, forceNoGear: true, dontGiveWeapon: true);
                Pawn p = PawnGenerator.GeneratePawn(request);
                if (p?.story == null) return null;
                p.gender = config.gender;
                p.story.bodyType = config.bodyType;
                p.story.headType = config.headType;
                if (!RaceUtility.HasHair(config.race)) p.story.hairDef = HairDefOf.Bald;
                else p.story.hairDef = config.hair;
                if (p.style != null)
                {
                    if (!RaceUtility.HasBeard(config.race, config.gender)) p.style.beardDef = BeardDefOf.NoBeard;
                    else p.style.beardDef = config.beard;
                }
                if (!config.overrideHairColor && original.story != null)
                    p.story.HairColor = original.story.HairColor;
                if (config.overrideHairColor)
                {
                    p.story.HairColor = config.hairColor;
                    HarCompat.TrySetHairColor(p, config.hairColor);
                }
                RaceSwapper.ApplySkinColor(p, config);
                HarCompat.TryApplyAddonVariants(p, config.addonVariants);
                p.Drawer?.renderer?.SetAllGraphicsDirty();
                PortraitsCache.SetDirty(p);
                return p;
            }
            catch (Exception e)
            {
                Log.Warning("[Identity Crisis] Could not create appearance preview: " + e);
                return null;
            }
            finally
            {
                Rand.PopState();
            }
        }
    }
}
