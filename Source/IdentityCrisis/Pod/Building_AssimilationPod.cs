using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace IdentityCrisis
{
    public class Building_AssimilationPod : Building, IThingHolder
    {
        private ThingOwner<Pawn> innerContainer;
        private Pawn pendingPawn;
        private AssimilationConfig config;
        private int ticksRemaining = -1;
        private ConversionType pendingType;
        private ThingDef originalRace;
        private Gender originalGender;
        private CompPowerTrader powerComp;
        private Effecter progressBar;

        public Building_AssimilationPod()
        {
            innerContainer = new ThingOwner<Pawn>(this, true, LookMode.Deep);
        }

        public Pawn Occupant => innerContainer.Count > 0 ? innerContainer[0] : null;

        public Pawn PendingPawn => pendingPawn;

        public AssimilationConfig Config => config;

        public bool PowerOn => powerComp == null || powerComp.PowerOn;

        public int TicksToComplete => def.GetModExtension<AssimilationPodExtension>()?.ticksToComplete ?? 60000;

        public float Progress => ticksRemaining < 0 ? 0f : Mathf.Clamp01(1f - (float)ticksRemaining / TicksToComplete);

        public bool NeedsCarrying(Pawn pawn)
        {
            return pawn != null && pawn == pendingPawn && Occupant == null && (pawn.IsPrisonerOfColony || pawn.Downed);
        }

        public ThingOwner GetDirectlyHeldThings()
        {
            return innerContainer;
        }

        public void GetChildHolders(List<IThingHolder> outChildren)
        {
            ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            powerComp = GetComp<CompPowerTrader>();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            if (Occupant != null)
            {
                Eject();
            }
            pendingPawn = null;
            config = null;
            CleanupEffecter();
            base.DeSpawn(mode);
        }

        // Called by the setup window when the player presses Accept.
        public void SetPending(Pawn pawn, AssimilationConfig newConfig)
        {
            if (pawn == null || (Occupant != null && Occupant != pawn))
            {
                return;
            }
            pendingPawn = pawn;
            config = newConfig;
            if (Occupant == pawn)
            {
                return;
            }
            if (pawn.IsPrisonerOfColony || pawn.Downed)
            {
                Messages.Message("IC_InspectWaiting".Translate(pawn.LabelShort), this, MessageTypeDefOf.NeutralEvent, false);
                return;
            }
            Job job = JobMaker.MakeJob(IC_DefOf.IC_EnterAssimilationPod, this);
            pawn.jobs?.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public AcceptanceReport CanAcceptPawn(Pawn pawn)
        {
            AcceptanceReport basic = ConversionUtility.CanBeAssimilated(pawn);
            if (!basic.Accepted)
            {
                return basic;
            }
            if (!PowerOn)
            {
                return "IC_RejectNoPower".Translate().Resolve();
            }
            if (Occupant != null)
            {
                return "IC_RejectBusy".Translate().Resolve();
            }
            if (pendingPawn != null && pendingPawn != pawn)
            {
                return "IC_RejectReserved".Translate(pendingPawn.LabelShort).Resolve();
            }
            return true;
        }

        public bool TryAcceptPawn(Pawn pawn)
        {
            if (pawn == null || pendingPawn != pawn || !CanAcceptPawn(pawn).Accepted)
            {
                return false;
            }
            if (config?.race == null || config.race == pawn.def)
            {
                Messages.Message("IC_RejectNotSet".Translate(), this, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            ConversionType type = ConversionUtility.Classify(pawn);
            ThingDef race = pawn.def;
            Gender gender = pawn.gender;
            bool wasSelected = Find.Selector.IsSelected(pawn);
            pawn.DeSpawnOrDeselect();
            if (!innerContainer.TryAddOrTransfer(pawn))
            {
                return false;
            }
            pendingType = type;
            originalRace = race;
            originalGender = gender;
            ticksRemaining = TicksToComplete;
            if (wasSelected)
            {
                Find.Selector.Select(this, false, false);
            }
            Messages.Message("IC_Started".Translate(pawn.LabelShort), this, MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        protected override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(250))
            {
                ValidatePending();
            }
            if (Occupant == null || !PowerOn)
            {
                CleanupEffecter();
                return;
            }
            if (ticksRemaining < 0)
            {
                ticksRemaining = TicksToComplete;
            }
            ticksRemaining--;
            UpdateEffecter();
            if (ticksRemaining <= 0)
            {
                Finish();
            }
        }

        private void Finish()
        {
            Pawn pawn = Occupant;
            if (pawn == null)
            {
                return;
            }
            CleanupEffecter();
            if (config?.race == null || config.race == pawn.def)
            {
                Messages.Message("IC_RejectNotSet".Translate(), this, MessageTypeDefOf.RejectInput, false);
                Eject();
                return;
            }
            ThingDef fromRace = originalRace ?? pawn.def;
            Gender fromGender = originalGender;
            ConversionType type = pendingType;
            Map map = Map;
            AssimilationReport report = RaceSwapper.Transform(pawn, config, InteractionCell, map);
            innerContainer.TryDropAll(InteractionCell, map, ThingPlaceMode.Near);
            // The report is filled from the pawn right before the rebuild, so it is the most
            // reliable "before" state (the pod fields can be empty in older saves).
            if (report != null)
            {
                fromRace = report.fromRace ?? fromRace;
                if (report.fromGender != Gender.None)
                {
                    fromGender = report.fromGender;
                }
            }
            // A failure here must not cut off the Log tab entry, the letter and the pod reset.
            try
            {
                ConversionUtility.ApplyConversion(pawn, type, fromRace, fromGender, report);
            }
            catch (System.Exception e)
            {
                Log.Error("[Identity Crisis] Applying the assimilation consequences to " + pawn + " failed: " + e);
            }
            PlayLogEntry_Assimilation.TryAdd(pawn, type, report);
            // A letter instead of a short message: it stays in the History tab.
            AssimilationLetter.Send(pawn, type, report);
            ResetState();
        }

        public void Eject()
        {
            if (Occupant != null && Map != null)
            {
                innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
            }
            CleanupEffecter();
            ResetState();
        }

        private void CancelPending()
        {
            pendingPawn = null;
            config = null;
        }

        private void ResetState()
        {
            pendingPawn = null;
            config = null;
            ticksRemaining = -1;
            originalRace = null;
            originalGender = Gender.None;
        }

        private void ValidatePending()
        {
            if (pendingPawn == null || Occupant != null)
            {
                return;
            }
            if (pendingPawn.Dead || pendingPawn.Destroyed || pendingPawn.MapHeld != Map || !ConversionUtility.CanBeAssimilated(pendingPawn).Accepted)
            {
                CancelPending();
            }
        }

        private void UpdateEffecter()
        {
            if (progressBar == null)
            {
                progressBar = EffecterDefOf.ProgressBarAlwaysVisible.Spawn();
            }
            progressBar.EffectTick(this, TargetInfo.Invalid);
            MoteProgressBar mote = ((SubEffecter_ProgressBar)progressBar.children[0]).mote;
            if (mote != null)
            {
                mote.progress = Progress;
                mote.offsetZ = -0.5f;
            }
        }

        private void CleanupEffecter()
        {
            progressBar?.Cleanup();
            progressBar = null;
        }

        private void OpenPawnMenu()
        {
            List<FloatMenuOption> options = new List<FloatMenuOption>();
            IEnumerable<Pawn> candidates = Map.mapPawns.FreeColonistsSpawned
                .Concat(Map.mapPawns.SlavesOfColonySpawned)
                .Concat(Map.mapPawns.PrisonersOfColonySpawned)
                .Distinct();
            foreach (Pawn candidate in candidates)
            {
                Pawn pawn = candidate;
                string label = pawn.LabelShortCap + ", " + pawn.def.label;
                AcceptanceReport report = CanAcceptPawn(pawn);
                if (!report.Accepted)
                {
                    options.Add(new FloatMenuOption(label + " (" + report.Reason + ")", null));
                    continue;
                }
                if (RaceUtility.DefaultTargetRace(pawn) == null)
                {
                    options.Add(new FloatMenuOption(label + " (" + "IC_RejectNoRace".Translate() + ")", null));
                    continue;
                }
                if (!pawn.IsPrisonerOfColony && !pawn.Downed && !pawn.CanReach(this, PathEndMode.InteractionCell, Danger.Deadly))
                {
                    options.Add(new FloatMenuOption(label + " (" + "IC_RejectUnreachable".Translate() + ")", null));
                    continue;
                }
                options.Add(new FloatMenuOption(label, () => Find.WindowStack.Add(new Dialog_AssimilationSetup(this, pawn, null))));
            }
            if (options.Count == 0)
            {
                options.Add(new FloatMenuOption("IC_NoEligiblePawns".Translate(), null));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }

            Pawn occupant = Occupant;
            Pawn target = occupant ?? pendingPawn;

            if (target == null)
            {
                Command_Action assimilate = new Command_Action
                {
                    defaultLabel = "IC_CommandAssimilate".Translate(),
                    defaultDesc = "IC_CommandAssimilateDesc".Translate(),
                    icon = IC_Textures.Assimilate,
                    action = OpenPawnMenu
                };
                if (!PowerOn)
                {
                    assimilate.Disable("NoPower".Translate());
                }
                yield return assimilate;
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "IC_CommandEditSetup".Translate(),
                defaultDesc = "IC_CommandEditSetupDesc".Translate(target.LabelShort),
                icon = IC_Textures.Edit,
                action = () => Find.WindowStack.Add(new Dialog_AssimilationSetup(this, target, config))
            };

            if (occupant != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "IC_CommandEject".Translate(),
                    defaultDesc = "IC_CommandEjectDesc".Translate(occupant.LabelShort),
                    icon = IC_Textures.Eject,
                    action = Eject
                };
                if (DebugSettings.ShowDevGizmos)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "DEV: finish assimilation",
                        action = Finish
                    };
                }
            }
            else
            {
                yield return new Command_Action
                {
                    defaultLabel = "IC_CommandCancel".Translate(),
                    defaultDesc = "IC_CommandCancelDesc".Translate(),
                    icon = IC_Textures.Cancel,
                    action = CancelPending
                };
            }
        }

        public override string GetInspectString()
        {
            StringBuilder sb = new StringBuilder();
            string baseString = base.GetInspectString();
            if (!baseString.NullOrEmpty())
            {
                sb.AppendLine(baseString);
            }
            Pawn occupant = Occupant;
            if (occupant != null)
            {
                string raceLabel = config?.race?.label ?? "?";
                sb.AppendLine("IC_InspectTarget".Translate(occupant.LabelShort, raceLabel));
                if (PowerOn)
                {
                    sb.AppendLine("IC_InspectProgress".Translate(Mathf.Max(0, ticksRemaining).ToStringTicksToPeriod()));
                }
                else
                {
                    sb.AppendLine("IC_InspectPaused".Translate());
                }
            }
            else if (pendingPawn != null)
            {
                sb.AppendLine("IC_InspectWaiting".Translate(pendingPawn.LabelShort));
            }
            return sb.ToString().TrimEndNewlines();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Deep.Look(ref innerContainer, "innerContainer", this);
            Scribe_References.Look(ref pendingPawn, "pendingPawn");
            Scribe_Deep.Look(ref config, "config");
            Scribe_Values.Look(ref ticksRemaining, "ticksRemaining", -1);
            Scribe_Values.Look(ref pendingType, "pendingType", ConversionType.Voluntary);
            Scribe_Defs.Look(ref originalRace, "originalRace");
            Scribe_Values.Look(ref originalGender, "originalGender", Gender.None);
        }
    }
}
