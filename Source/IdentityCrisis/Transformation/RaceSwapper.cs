using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace IdentityCrisis
{
    // Rebuilds a pawn as another race. Call only while the pawn is unspawned (inside the pod),
    // so pawn.def can change without desyncing map listers; comps run PostSpawnSetup on exit.
    public static class RaceSwapper
    {
        private class KeptHediff
        {
            public Hediff hediff;
            public BodyPartRecord newPart;
        }

        // Returns what was done to the pawn (for the Log tab), or null if nothing was changed.
        public static AssimilationReport Transform(Pawn pawn, AssimilationConfig config, IntVec3 dropCell, Map map)
        {
            ThingDef oldRace = pawn.def;
            ThingDef newRace = config?.race;
            if (newRace?.race == null || !newRace.race.Humanlike)
            {
                Log.Error("[Identity Crisis] Invalid target race for " + pawn);
                return null;
            }
            float oldAgeYears = pawn.ageTracker.AgeBiologicalYearsFloat;
            AssimilationReport report = new AssimilationReport
            {
                fromRace = oldRace,
                toRace = newRace,
                fromGender = pawn.gender,
                fromAgeYears = oldAgeYears
            };
            List<KeptHediff> kept = new List<KeptHediff>();

            Step("hediffs", () => kept = PrepareHediffs(pawn, newRace.race.body, dropCell, map, report));
            Step("surgery bills", () => pawn.health.surgeryBills?.Clear());

            pawn.def = newRace;

            Step("part remap", () => RemapHediffs(pawn, kept));
            Step("comps", () => SyncComps(pawn));
            Step("gender", () => ApplyGender(pawn, config));
            Step("age", () => ApplyAge(pawn, config, oldRace, oldAgeYears));
            Step("appearance", () => ApplyAppearance(pawn, config));
            Step("genes", () => RemoveForbiddenGenes(pawn, report));
            Step("gear", () => FixGear(pawn, dropCell, map, report));
            Step("refresh", () => Refresh(pawn));

            report.toGender = pawn.gender;
            report.toAgeYears = pawn.ageTracker.AgeBiologicalYearsFloat;
            return report;
        }

        private static void Step(string name, Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                Log.Error("[Identity Crisis] Transformation step '" + name + "' failed: " + e);
            }
        }

        // Removes everything the new body should not inherit (wounds, scars, missing parts, diseases)
        // and remembers implants that have a matching part in the new body.
        private static List<KeptHediff> PrepareHediffs(Pawn pawn, BodyDef newBody, IntVec3 dropCell, Map map, AssimilationReport report)
        {
            List<KeptHediff> kept = new List<KeptHediff>();
            List<Hediff> toRemove = new List<Hediff>();
            List<BodyPartRecord> oldParts = pawn.RaceProps.body.AllParts;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff.Part == null)
                {
                    if (!KeepWholeBody(hediff))
                    {
                        toRemove.Add(hediff);
                        AddVisible(report.healed, hediff);
                    }
                    continue;
                }
                if (IsImplant(hediff))
                {
                    BodyPartRecord mapped = MapPart(hediff.Part, oldParts, newBody);
                    if (mapped != null)
                    {
                        kept.Add(new KeptHediff { hediff = hediff, newPart = mapped });
                        report.implantsMoved.Add(MovedLabel(hediff, mapped));
                        continue;
                    }
                    if (hediff.def.spawnThingOnRemoved != null && map != null)
                    {
                        GenPlace.TryPlaceThing(ThingMaker.MakeThing(hediff.def.spawnThingOnRemoved), dropCell, map, ThingPlaceMode.Near);
                        report.implantsExtracted.Add(HediffLabel(hediff));
                    }
                    else
                    {
                        report.implantsLost.Add(HediffLabel(hediff));
                    }
                }
                else
                {
                    AddVisible(report.healed, hediff);
                }
                toRemove.Add(hediff);
            }
            foreach (Hediff hediff in toRemove)
            {
                pawn.health.RemoveHediff(hediff);
            }
            return kept;
        }

        private static void AddVisible(List<string> list, Hediff hediff)
        {
            if (hediff.Visible)
            {
                list.Add(HediffLabel(hediff));
            }
        }

        private static string HediffLabel(Hediff hediff)
        {
            string label = hediff.LabelCap;
            if (hediff.Part != null)
            {
                label += " (" + hediff.Part.Label + ")";
            }
            return label;
        }

        private static string MovedLabel(Hediff hediff, BodyPartRecord newPart)
        {
            string label = hediff.LabelCap;
            string oldPartLabel = hediff.Part.Label;
            string newPartLabel = newPart.Label;
            string parts = oldPartLabel == newPartLabel ? newPartLabel : oldPartLabel + " → " + newPartLabel;
            return label + " (" + parts + ")";
        }

        private static bool KeepWholeBody(Hediff hediff)
        {
            if (hediff is Hediff_ConversionRecord)
            {
                return true;
            }
            if (hediff.def == IC_DefOf.IC_FormWashShock || hediff.def == IC_DefOf.IC_PhantomDiscomfort || hediff.def == IC_DefOf.IC_GenderShift)
            {
                return true;
            }
            if (hediff.def.makesSickThought)
            {
                return false;
            }
            if (hediff is Hediff_Addiction)
            {
                return true;
            }
            return !hediff.def.isBad;
        }

        private static bool IsImplant(Hediff hediff)
        {
            return hediff is Hediff_AddedPart || hediff is Hediff_Implant || hediff is Hediff_Level || hediff.def.countsAsAddedPartOrImplant;
        }

        private static BodyPartRecord MapPart(BodyPartRecord oldPart, List<BodyPartRecord> oldParts, BodyDef newBody)
        {
            List<BodyPartRecord> candidates = newBody.AllParts.Where(p => p.def == oldPart.def).ToList();
            if (candidates.Count == 0)
            {
                return null;
            }
            if (!oldPart.customLabel.NullOrEmpty())
            {
                BodyPartRecord byLabel = candidates.FirstOrDefault(p => p.customLabel == oldPart.customLabel);
                if (byLabel != null)
                {
                    return byLabel;
                }
            }
            int index = oldParts.Where(p => p.def == oldPart.def).ToList().IndexOf(oldPart);
            return index >= 0 && index < candidates.Count ? candidates[index] : candidates[0];
        }

        private static void RemapHediffs(Pawn pawn, List<KeptHediff> kept)
        {
            foreach (KeptHediff entry in kept)
            {
                entry.hediff.Part = entry.newPart;
            }
            pawn.health.hediffSet.DirtyCache();
        }

        private static void SyncComps(Pawn pawn)
        {
            if (pawn.def.comps == null)
            {
                return;
            }
            foreach (ThingComp comp in pawn.AllComps)
            {
                CompProperties props = pawn.def.comps.FirstOrDefault(p => p.compClass == comp.GetType());
                if (props != null)
                {
                    comp.props = props;
                }
            }
        }

        private static void ApplyGender(Pawn pawn, AssimilationConfig config)
        {
            if (!pawn.RaceProps.hasGenders)
            {
                pawn.gender = Gender.None;
                return;
            }
            if (config.gender != Gender.None)
            {
                pawn.gender = config.gender;
            }
            else if (pawn.gender == Gender.None)
            {
                pawn.gender = Rand.Bool ? Gender.Male : Gender.Female;
            }
        }

        private static void ApplyAge(Pawn pawn, AssimilationConfig config, ThingDef oldRace, float oldAgeYears)
        {
            float years = config.ageYears;
            if (years <= 0f)
            {
                years = oldAgeYears * RaceUtility.LifeExpectancy(pawn.def) / RaceUtility.LifeExpectancy(oldRace);
            }
            years = Mathf.Clamp(years, RaceUtility.AdultMinAge(pawn.def), RaceUtility.MaxAge(pawn.def));
            pawn.ageTracker.AgeBiologicalTicks = (long)(years * GenDate.TicksPerYear);
        }

        private static void ApplyAppearance(Pawn pawn, AssimilationConfig config)
        {
            HarCompat.ResetAlienComp(pawn);
            Gender gender = pawn.gender;

            List<BodyTypeDef> bodies = RaceUtility.AllowedBodyTypes(pawn.def, gender);
            BodyTypeDef body = config.bodyType;
            if (body == null || !bodies.Contains(body))
            {
                body = bodies.Contains(pawn.story.bodyType)
                    ? pawn.story.bodyType
                    : bodies.RandomElementWithFallback(gender == Gender.Female ? BodyTypeDefOf.Female : BodyTypeDefOf.Male);
            }
            pawn.story.bodyType = body;

            List<HeadTypeDef> heads = RaceUtility.AllowedHeadTypes(pawn.def, gender);
            HeadTypeDef head = config.headType;
            if (head == null || !heads.Contains(head))
            {
                head = heads.Contains(pawn.story.headType) ? pawn.story.headType : heads.RandomElementWithFallback(pawn.story.headType);
            }
            pawn.story.headType = head;

            if (!RaceUtility.HasHair(pawn.def))
            {
                pawn.story.hairDef = HairDefOf.Bald;
            }
            else if (config.hair != null)
            {
                pawn.story.hairDef = config.hair;
            }
            else if (pawn.story.hairDef == null || !PawnStyleItemChooser.WantsToUseStyle(pawn, pawn.story.hairDef))
            {
                pawn.story.hairDef = PawnStyleItemChooser.RandomHairFor(pawn);
            }

            if (pawn.style != null)
            {
                if (!RaceUtility.HasBeard(pawn.def, gender))
                {
                    pawn.style.beardDef = BeardDefOf.NoBeard;
                }
                else if (config.beard != null)
                {
                    pawn.style.beardDef = config.beard;
                }
                else if (pawn.style.beardDef == null || !PawnStyleItemChooser.WantsToUseStyle(pawn, pawn.style.beardDef))
                {
                    pawn.style.beardDef = PawnStyleItemChooser.RandomBeardFor(pawn);
                }
            }

            if (config.overrideHairColor)
            {
                pawn.story.HairColor = config.hairColor;
                HarCompat.TrySetHairColor(pawn, config.hairColor);
            }
        }

        private static void RemoveForbiddenGenes(Pawn pawn, AssimilationReport report)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null)
            {
                return;
            }
            foreach (Gene gene in pawn.genes.GenesListForReading.ToList())
            {
                if (!HarCompat.CanHaveGene(gene.def, pawn.def))
                {
                    string label = gene.def.LabelCap;
                    pawn.genes.RemoveGene(gene);
                    report.genesRemoved.Add(label);
                }
            }
        }

        private static void FixGear(Pawn pawn, IntVec3 dropCell, Map map, AssimilationReport report)
        {
            if (pawn.apparel != null)
            {
                foreach (Apparel apparel in pawn.apparel.WornApparel.ToList())
                {
                    if (ApparelUtility.HasPartsToWear(pawn, apparel.def) && HarCompat.CanWear(apparel.def, pawn.def))
                    {
                        continue;
                    }
                    string label = apparel.LabelCap;
                    pawn.apparel.Remove(apparel);
                    Stash(pawn, apparel, dropCell, map);
                    report.gearRemoved.Add(label);
                }
            }
            if (pawn.equipment != null)
            {
                foreach (ThingWithComps equipment in pawn.equipment.AllEquipmentListForReading.ToList())
                {
                    if (HarCompat.CanEquip(equipment.def, pawn.def))
                    {
                        continue;
                    }
                    string label = equipment.LabelCap;
                    pawn.equipment.Remove(equipment);
                    Stash(pawn, equipment, dropCell, map);
                    report.gearRemoved.Add(label);
                }
            }
        }

        private static void Stash(Pawn pawn, Thing thing, IntVec3 dropCell, Map map)
        {
            if (pawn.inventory != null && pawn.inventory.innerContainer.TryAdd(thing))
            {
                return;
            }
            if (map != null)
            {
                GenPlace.TryPlaceThing(thing, dropCell, map, ThingPlaceMode.Near);
            }
        }

        private static void Refresh(Pawn pawn)
        {
            pawn.health.hediffSet.DirtyCache();
            pawn.health.capacities?.Notify_CapacityLevelsDirty();
            pawn.health.summaryHealth?.Notify_HealthChanged();
            pawn.needs?.AddOrRemoveNeedsAsAppropriate();
            pawn.VerbTracker?.InitVerbsFromZero();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            PortraitsCache.SetDirty(pawn);
        }
    }
}
