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

        private static string Signature(AssimilationConfig c)
        {
            return string.Join("|", new string[] {
                c.race?.defName ?? "", c.gender.ToString(), c.ageYears.ToString("R"),
                c.bodyType?.defName ?? "", c.headType?.defName ?? "", c.hair?.defName ?? "",
                c.beard?.defName ?? "", c.overrideHairColor.ToString(), c.hairColor.ToString(),
                c.overrideSkinColor.ToString(), c.skinColor.ToString(),
                c.addonVariants == null ? "" : string.Join(",", c.addonVariants.Select(x => x.ToString()).ToArray())
            });
        }

        public RenderTexture Get(AssimilationConfig config, Pawn original, Vector2 size)
        {
            if (config?.race == null || original == null) return null;
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
            Rand.PushState(original.thingIDNumber ^ config.race.shortHash);
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
            Rand.PushState(original.thingIDNumber ^ config.race.shortHash);
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
                if (config.bodyType != null) p.story.bodyType = config.bodyType;
                if (config.headType != null) p.story.headType = config.headType;
                if (!RaceUtility.HasHair(config.race)) p.story.hairDef = HairDefOf.Bald;
                else if (config.hair != null) p.story.hairDef = config.hair;
                if (p.style != null)
                {
                    if (!RaceUtility.HasBeard(config.race, config.gender)) p.style.beardDef = BeardDefOf.NoBeard;
                    else if (config.beard != null) p.style.beardDef = config.beard;
                }
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
