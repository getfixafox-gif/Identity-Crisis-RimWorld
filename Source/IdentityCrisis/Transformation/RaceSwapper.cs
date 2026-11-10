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

        // Compatibility entry point: a partial operation is never a successful report.
        public static AssimilationReport Transform(Pawn pawn, AssimilationConfig config, IntVec3 dropCell, Map map)
        {
            TransformationResult result = ApplyPlan(pawn, BuildPlan(pawn, config), dropCell, map);
            return result.Success ? result.Report : null;
        }

        public static TransformationPlan BuildPlan(Pawn pawn, AssimilationConfig config)
        {
            try
            {
                ValidatePawn(pawn, config?.race);
                AssimilationConfig resolved = config.Copy();
                if (!resolved.policyCaptured)
                {
                    IdentityCrisisSettings settings = IdentityCrisisMod.Settings;
                    settings?.Sanitize();
                    resolved.allowRejuvenation = settings?.allowRejuvenation ?? true;
                    resolved.allowRegrowBodyParts = settings?.allowRegrowBodyParts ?? true;
                    resolved.allowCureDiseases = settings?.allowCureDiseases ?? true;
                    resolved.recoveryMultiplier = settings?.recoveryDurationMultiplier ?? 1f;
                    resolved.policyCaptured = true;
                }
                Rand.PushState(resolved.appearanceSeed != 0 ? resolved.appearanceSeed : pawn.thingIDNumber);
                try
                {
                if (RaceUtility.GenderLocked(resolved.race, out Gender fixedGender))
                    resolved.gender = fixedGender;
                else if (resolved.gender == Gender.None)
                    resolved.gender = pawn.gender == Gender.None ? Gender.Male : pawn.gender;
                if (resolved.gender != Gender.None && resolved.gender != Gender.Male && resolved.gender != Gender.Female)
                    throw new InvalidOperationException("Invalid gender.");
                if (float.IsNaN(resolved.ageYears) || float.IsInfinity(resolved.ageYears)) throw new InvalidOperationException("Invalid age.");
                float age = resolved.ageYears <= 0f ? RaceUtility.ProportionalAge(pawn, resolved.race) : resolved.ageYears;
                if (float.IsNaN(age) || float.IsInfinity(age)) throw new InvalidOperationException("Invalid age.");
                resolved.ageYears = Mathf.Clamp(age, RaceUtility.AdultMinAge(resolved.race), RaceUtility.MaxAge(resolved.race));
                HarCompat.ResolveAppearance(pawn, resolved);
                Preflight(pawn, resolved);
                return new TransformationPlan(pawn, resolved, null);
                }
                finally { Rand.PopState(); }
            }
            catch (Exception e)
            {
                return new TransformationPlan(pawn, null, e.Message);
            }
        }

        public static TransformationResult ApplyPlan(Pawn pawn, TransformationPlan plan, IntVec3 dropCell, Map map)
        {
            bool started = false;
            AssimilationReport report = null;
            string stage = "preflight";
            try
            {
                if (plan == null || !plan.CanApply) throw new InvalidOperationException(plan?.Error ?? "No applicable plan.");
                if (plan.Consumed || plan.Pawn != pawn || pawn?.def != plan.SourceRace)
                    throw new InvalidOperationException("Plan is consumed or belongs to another pawn/race.");
                AssimilationConfig config = plan.ResolvedConfig;
                ValidatePawn(pawn, config.race);
                if (pawn.Spawned) throw new InvalidOperationException("Pawn must be unspawned before applying a plan.");
                // Recheck mutable health, genes, gear and compatibility immediately before any mutation.
                Preflight(pawn, config);
                HarCompat.ValidateResolvedAppearance(pawn, config);
                bool mayExtract = pawn.health.hediffSet.hediffs.Any(h => h.Part != null && IsImplant(h)
                    && MapPart(h.Part, pawn.RaceProps.body.AllParts, config.race.race.body) == null);
                bool hasGear = (pawn.apparel?.WornApparel.Count ?? 0) > 0 || (pawn.equipment?.AllEquipmentListForReading.Count ?? 0) > 0;
                if ((mayExtract || hasGear) && (map == null || !dropCell.InBounds(map)))
                    throw new InvalidOperationException("A valid output map and cell are required for implants or gear.");
                report = new AssimilationReport { fromRace = pawn.def, toRace = config.race,
                    fromGender = pawn.gender, fromAgeYears = pawn.ageTracker.AgeBiologicalYearsFloat };
                plan.Consumed = true;
                started = true; // From here failure is conservatively partial; no rollback is promised.
                stage = "hediffs";
                List<KeptHediff> kept = PrepareHediffs(pawn, config.race.race.body, dropCell, map, report, config.allowCureDiseases);
                stage = "surgery bills";
                pawn.health.surgeryBills?.Clear();
                stage = "race and body";
                pawn.def = config.race;
                RemapHediffs(pawn, kept);
                stage = "genes";
                RemoveForbiddenGenes(pawn, report);
                stage = "appearance";
                pawn.gender = config.gender;
                pawn.ageTracker.AgeBiologicalTicks = (long)(config.ageYears * GenDate.TicksPerYear);
                ApplyAppearance(pawn, config);
                stage = "gear";
                FixGear(pawn, dropCell, map, report);
                stage = "refresh";
                Refresh(pawn);
                if (pawn.Dead || pawn.def != config.race || pawn.gender != config.gender
                    || pawn.story.bodyType != config.bodyType || pawn.story.headType != config.headType
                    || pawn.story.hairDef != config.hair || (pawn.style != null && pawn.style.beardDef != config.beard)
                    || pawn.health.hediffSet.hediffs.Any(h => h.Part != null && !pawn.RaceProps.body.AllParts.Contains(h.Part)))
                    throw new InvalidOperationException("Post-apply pawn/body invariant failed.");
                report.toGender = pawn.gender;
                report.toAgeYears = pawn.ageTracker.AgeBiologicalYearsFloat;
                return new TransformationResult(true, false, null, report);
            }
            catch (Exception e)
            {
                string error = "Transformation " + stage + ": " + e.Message;
                Log.Error("[Identity Crisis] " + error + "\n" + e);
                return new TransformationResult(false, started, error, report);
            }
        }

        private static void ValidatePawn(Pawn pawn, ThingDef race)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.def?.race == null || !pawn.RaceProps.Humanlike)
                throw new InvalidOperationException("A living humanlike pawn is required.");
            if (race?.race?.body == null || !race.race.Humanlike || race.race.body.AllParts.NullOrEmpty())
                throw new InvalidOperationException("Invalid target race/body.");
            if (pawn.health?.hediffSet == null || pawn.story == null || pawn.ageTracker == null)
                throw new InvalidOperationException("Required pawn trackers are missing.");
        }

        private static void Preflight(Pawn pawn, AssimilationConfig config)
        {
            // Unknown comps can cache race-specific state in Initialize/PostSpawnSetup. Matching
            // class names alone is NOT proof that rebinding props is safe. Keep identical instances
            // and identical props only; no add/remove/reinitialize lifecycle is attempted here.
            HarCompat.RequireRestrictionChecks();
            if (!config.allowRejuvenation && config.ageYears + 0.01f < RaceUtility.ProportionalAge(pawn, config.race))
                throw new InvalidOperationException("Rejuvenation is disabled in mod settings.");
            if (!config.allowRegrowBodyParts && pawn.health.hediffSet.hediffs.Any(h => h is Hediff_MissingPart))
                throw new InvalidOperationException("Regrowing missing parts is disabled; this transformation would restore a missing part.");
            if (pawn.style == null && config.beard != BeardDefOf.NoBeard)
                throw new InvalidOperationException("Pawn has no beard/style tracker.");
            List<CompProperties> target = config.race.comps ?? new List<CompProperties>();
            if (pawn.AllComps.Count != target.Count)
                throw new InvalidOperationException("Unsupported component-set change.");
            var remaining = new List<CompProperties>(target);
            foreach (ThingComp comp in pawn.AllComps)
            {
                CompProperties match = remaining.FirstOrDefault(p => p != null && p.compClass == comp.GetType() && ReferenceEquals(p, comp.props));
                if (match == null) throw new InvalidOperationException("Unsupported component lifecycle/props change: " + comp.GetType().FullName);
                remaining.Remove(match);
            }
            foreach (Hediff h in pawn.health.hediffSet.hediffs)
            {
                if (h.Part != null && IsImplant(h) && MapPart(h.Part, pawn.RaceProps.body.AllParts, config.race.race.body) == null
                    && h.def.spawnThingOnRemoved == null)
                    throw new InvalidOperationException("Implant has no unambiguous target part or extraction item: " + h.def.defName);
            }
            if (ModsConfig.BiotechActive && pawn.genes != null)
                foreach (Gene gene in pawn.genes.GenesListForReading)
                    RequireKnown(HarCompat.CheckCanHaveGene(gene.def, config.race, pawn.genes.Xenogenes.Contains(gene)), "gene " + gene.def.defName);
            if (pawn.apparel != null)
                foreach (Apparel apparel in pawn.apparel.WornApparel)
                    RequireKnown(HarCompat.CheckCanWear(apparel.def, config.race), "apparel " + apparel.def.defName);
            if (pawn.equipment != null)
                foreach (ThingWithComps equipment in pawn.equipment.AllEquipmentListForReading)
                    RequireKnown(HarCompat.CheckCanEquip(equipment.def, config.race), "equipment " + equipment.def.defName);
        }

        private static bool RequireKnown(HarRestrictionResult result, string subject)
        {
            if (result == HarRestrictionResult.Unavailable)
                throw new InvalidOperationException("HAR restriction check unavailable: " + subject);
            return result == HarRestrictionResult.Allowed;
        }

        // Removes everything the new body should not inherit (wounds, scars, missing parts, diseases)
        // and remembers implants that have a matching part in the new body.
        private static List<KeptHediff> PrepareHediffs(Pawn pawn, BodyDef newBody, IntVec3 dropCell, Map map, AssimilationReport report, bool cureDiseases)
        {
            List<KeptHediff> kept = new List<KeptHediff>();
            List<Hediff> toRemove = new List<Hediff>();
            List<BodyPartRecord> oldParts = pawn.RaceProps.body.AllParts;
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs.ToList())
            {
                if (hediff.Part == null)
                {
                    if (!KeepWholeBody(hediff) && cureDiseases)
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
                        Thing extracted = ThingMaker.MakeThing(hediff.def.spawnThingOnRemoved);
                        if (!GenPlace.TryPlaceThing(extracted, dropCell, map, ThingPlaceMode.Near))
                        {
                            extracted.Destroy();
                            throw new InvalidOperationException("Could not place extracted implant: " + hediff.def.defName);
                        }
                        report.implantsExtracted.Add(HediffLabel(hediff));
                    }
                    else
                    {
                        throw new InvalidOperationException("Implant cannot be safely extracted: " + hediff.def.defName);
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
                List<BodyPartRecord> byLabel = candidates.Where(p => p.customLabel == oldPart.customLabel).ToList();
                return byLabel.Count == 1 && oldParts.Count(p => p.def == oldPart.def && p.customLabel == oldPart.customLabel) == 1
                    ? byLabel[0] : null;
            }
            int index = oldParts.Where(p => p.def == oldPart.def).ToList().IndexOf(oldPart);
            return index >= 0 && index < candidates.Count
                && oldParts.Count(p => p.def == oldPart.def) == candidates.Count ? candidates[index] : null;
        }

        private static void RemapHediffs(Pawn pawn, List<KeptHediff> kept)
        {
            foreach (KeptHediff entry in kept)
            {
                entry.hediff.Part = entry.newPart;
            }
            pawn.health.hediffSet.DirtyCache();
        }

        private static void ApplyAppearance(Pawn pawn, AssimilationConfig config)
        {
            pawn.story.bodyType = config.bodyType;
            pawn.story.headType = config.headType;
            pawn.story.hairDef = config.hair;
            if (pawn.style != null) pawn.style.beardDef = config.beard;
            pawn.story.HairColor = config.hairColor;
            pawn.story.skinColorOverride = config.skinColor;
            HarCompat.ApplyResolvedAppearance(pawn, config);
        }

        public static void ApplySkinColor(Pawn pawn, AssimilationConfig config)
        {
            if (!config.overrideSkinColor || pawn.story == null)
            {
                return;
            }
            pawn.story.skinColorOverride = config.skinColor;
            HarCompat.TrySetSkinColor(pawn, config.skinColor);
        }

        private static void RemoveForbiddenGenes(Pawn pawn, AssimilationReport report)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null)
            {
                return;
            }
            foreach (Gene gene in pawn.genes.GenesListForReading.ToList())
            {
                if (!RequireKnown(HarCompat.CheckCanHaveGene(gene.def, pawn.def, pawn.genes.Xenogenes.Contains(gene)), "gene " + gene.def.defName))
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
                    if (ApparelUtility.HasPartsToWear(pawn, apparel.def) && RequireKnown(HarCompat.CheckCanWear(apparel.def, pawn.def), "apparel " + apparel.def.defName))
                    {
                        continue;
                    }
                    string label = apparel.LabelCap;
                    pawn.apparel.Remove(apparel);
                    if (!Stash(pawn, apparel, dropCell, map))
                    {
                        pawn.apparel.Wear(apparel, false);
                        throw new InvalidOperationException("Could not stash apparel: " + label);
                    }
                    report.gearRemoved.Add(label);
                }
            }
            if (pawn.equipment != null)
            {
                foreach (ThingWithComps equipment in pawn.equipment.AllEquipmentListForReading.ToList())
                {
                    if (RequireKnown(HarCompat.CheckCanEquip(equipment.def, pawn.def), "equipment " + equipment.def.defName))
                    {
                        continue;
                    }
                    string label = equipment.LabelCap;
                    pawn.equipment.Remove(equipment);
                    if (!Stash(pawn, equipment, dropCell, map))
                    {
                        pawn.equipment.AddEquipment(equipment);
                        throw new InvalidOperationException("Could not stash equipment: " + label);
                    }
                    report.gearRemoved.Add(label);
                }
            }
        }

        private static bool Stash(Pawn pawn, Thing thing, IntVec3 dropCell, Map map)
        {
            if (pawn.inventory != null && pawn.inventory.innerContainer.TryAdd(thing))
            {
                return true;
            }
            return map != null && GenPlace.TryPlaceThing(thing, dropCell, map, ThingPlaceMode.Near);
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
