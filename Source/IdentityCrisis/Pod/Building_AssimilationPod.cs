using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

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
        private CompRefuelable bloodComp;
        private Effecter progressBar;
        private PodLifecycleState lifecycle;
        private AssimilationConfig activeConfig;
        private int activeDuration;
        private int activeBloodCost;
        private float activeEffectIntensity = 1f;
        private bool userPaused;
        private bool applyAttempted;
        private string lastError;
        private int completionTicks;
        private readonly Dictionary<string, Graphic> stateGraphics = new Dictionary<string, Graphic>();
        public PodLifecycleState State => lifecycle;
        public string LastError => lastError;
        public bool CanEditSetup => Occupant == null && !applyAttempted;
        public override Graphic Graphic
        {
            get
            {
                string state = lifecycle == PodLifecycleState.Error ? "error" :
                    !PowerOn ? "no-power" : lifecycle == PodLifecycleState.Completed ? "completion" :
                    Occupant != null && !userPaused ? "active" : "idle";
                if (!stateGraphics.TryGetValue(state, out Graphic graphic))
                {
                    string path = "Things/Building/IC_AssimilationPod/States/" + state + "/IC_AssimilationPod";
                    graphic = GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout,
                        def.graphicData.drawSize, Color.white, Color.white);
                    stateGraphics[state] = graphic;
                }
                return graphic;
            }
        }

        public Building_AssimilationPod()
        {
            innerContainer = new ThingOwner<Pawn>(this, true, LookMode.Deep);
        }

        public Pawn Occupant => innerContainer.Count > 0 ? innerContainer[0] : null;

        public Pawn PendingPawn => pendingPawn;

        public AssimilationConfig Config => (activeConfig ?? config)?.Copy();

        public bool PowerOn => powerComp == null || powerComp.PowerOn;

        // Blood tank exists only with Biotech (added by Patches/IC_BloodTank.xml).
        public CompRefuelable BloodComp => bloodComp ?? (bloodComp = GetComp<CompRefuelable>());

        public int BloodRequired => BloodComp == null ? 0 : Mathf.Max(0, Mathf.CeilToInt(
            (def.GetModExtension<AssimilationPodExtension>()?.bloodPacksPerConversion ?? 5)
            * (IdentityCrisisMod.Settings?.bloodCostMultiplier ?? 1f)));

        public bool HasEnoughBlood => BloodComp == null || BloodComp.Fuel >= BloodRequired - 0.001f;

        public int TicksToComplete => Mathf.Max(1, Mathf.RoundToInt(
            (def.GetModExtension<AssimilationPodExtension>()?.ticksToComplete ?? 60000)
            * (IdentityCrisisMod.Settings?.assimilationDurationMultiplier ?? 1f)));

        public float Progress => ticksRemaining < 0 ? 0f : Mathf.Clamp01(1f - (float)ticksRemaining / Mathf.Max(1, activeDuration));

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
            bloodComp = GetComp<CompRefuelable>();
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            Eject();
            if (Occupant != null) return;
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Eject();
            if (Occupant != null) return;
            base.Destroy(mode);
        }

        // Called by the setup window when the player presses Accept.
        public void SetPending(Pawn pawn, AssimilationConfig newConfig)
        {
            if (pawn == null || newConfig == null || !CanEditSetup || (Occupant != null && Occupant != pawn))
            {
                return;
            }
            pendingPawn = pawn;
            if (!CanEditSetup) return;
            config = newConfig.Copy();
            lifecycle = PodLifecycleState.Waiting;
            lastError = null;
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
            if (lifecycle == PodLifecycleState.Error || applyAttempted)
                return "IC_PodUnavailable".Translate().Resolve();
            AcceptanceReport basic = ConversionUtility.CanBeAssimilated(pawn);
            if (!basic.Accepted)
            {
                return basic;
            }
            if (!PowerOn)
            {
                return "IC_RejectNoPower".Translate().Resolve();
            }
            if (!HasEnoughBlood)
            {
                return "IC_RejectNoBlood".Translate(BloodRequired, Mathf.FloorToInt(BloodComp.Fuel)).Resolve();
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
            if (pawn == null || pendingPawn != pawn)
            {
                return false;
            }
            AcceptanceReport canAccept = CanAcceptPawn(pawn);
            if (!canAccept.Accepted)
            {
                if (!canAccept.Reason.NullOrEmpty())
                {
                    Messages.Message(canAccept.Reason.CapitalizeFirst(), this, MessageTypeDefOf.RejectInput, false);
                }
                return false;
            }
            if (config?.race == null || config.race == pawn.def)
            {
                Messages.Message("IC_RejectNotSet".Translate(), this, MessageTypeDefOf.RejectInput, false);
                return false;
            }
            TransformationPlan plan = RaceSwapper.BuildPlan(pawn, config.Copy());
            if (!plan.CanApply)
            {
                SetError(plan.Error);
                return false;
            }
            activeConfig = plan.ResolvedConfig;
            activeDuration = TicksToComplete;
            activeBloodCost = BloodRequired;
            activeEffectIntensity = IdentityCrisisMod.Settings?.podEffectIntensity ?? 1f;
            ConversionType type = ConversionUtility.Classify(pawn);
            ThingDef race = pawn.def;
            Gender gender = pawn.gender;
            bool wasSelected = Find.Selector.IsSelected(pawn);
            Map entryMap = pawn.MapHeld;
            IntVec3 entryCell = pawn.PositionHeld;
            pawn.DeSpawnOrDeselect();
            if (!innerContainer.TryAddOrTransfer(pawn))
            {
                if (!pawn.Spawned && entryMap != null)
                    GenSpawn.Spawn(pawn, entryCell, entryMap);
                SetError("IC_PodTransferFailed".Translate().Resolve());
                return false;
            }
            SoundDefOf.CryptosleepCasket_Accept.PlayOneShot(new TargetInfo(Position, Map));
            // The blood is poured in at the start; ejecting the pawn early does not return it.
            if (BloodComp != null && BloodRequired > 0)
            {
                BloodComp.ConsumeFuel(activeBloodCost);
            }
            pendingType = type;
            originalRace = race;
            originalGender = gender;
            ticksRemaining = activeDuration;
            lifecycle = PodLifecycleState.Working;
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
            if (this.IsHashIntervalTick(250)) ValidatePending();
            if (completionTicks > 0 && --completionTicks == 0 && Occupant == null)
                lifecycle = PodLifecycleState.Idle;
            if (Occupant == null) { CleanupEffecter(); return; }
            if (lifecycle == PodLifecycleState.Error || applyAttempted) { CleanupEffecter(); return; }
            if (Occupant.Dead || Occupant.Destroyed) { SetError("IC_PodOccupantLost".Translate().Resolve()); return; }
            if (!PowerOn || userPaused)
            {
                lifecycle = PodLifecycleState.Paused;
                CleanupEffecter();
                return;
            }
            lifecycle = PodLifecycleState.Working;
            if (activeConfig == null || activeDuration <= 0) { SetError("IC_PodLoadUnsafe".Translate().Resolve()); return; }
            ticksRemaining--;
            int interval = Mathf.Max(60, Mathf.RoundToInt(250f / Mathf.Max(0.1f, activeEffectIntensity)));
            if (activeEffectIntensity > 0f && this.IsHashIntervalTick(interval) && Map != null)
                FleckMaker.ThrowMicroSparks(Position.ToVector3Shifted(), Map);
            UpdateEffecter();
            if (ticksRemaining <= 0) Finish();
        }

        private void Finish()
        {
            Pawn pawn = Occupant;
            if (pawn == null || applyAttempted || lifecycle == PodLifecycleState.Error) return;
            CleanupEffecter();
            if (activeConfig == null) { SetError("IC_PodInvalidPlan".Translate().Resolve()); return; }
            TransformationPlan plan = RaceSwapper.BuildPlan(pawn, activeConfig.Copy());
            if (!plan.CanApply) { SetError(plan.Error); return; }
            lifecycle = PodLifecycleState.Completing;
            applyAttempted = true;
            TransformationResult result = RaceSwapper.ApplyPlan(pawn, plan, InteractionCell, Map);
            if (!result.Success || result.Report == null)
            {
                SetError(result.Error ?? "IC_PodUnknownError".Translate().Resolve());
                return;
            }
            AssimilationReport report = result.Report;
            try
            {
                ConversionUtility.ApplyConversion(pawn, pendingType, report.fromRace, report.fromGender,
                    report, activeConfig.recoveryMultiplier);
                PlayLogEntry_Assimilation.TryAdd(pawn, pendingType, report);
                AssimilationLetter.Send(pawn, pendingType, report);
            }
            catch (System.Exception e) { SetError(e.Message); return; }
            innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
            if (Occupant != null) { SetError("IC_PodEjectFailed".Translate().Resolve()); return; }
            if (Map != null && !Destroyed)
                SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(Position, Map)));
            ResetState();
            lifecycle = PodLifecycleState.Completed;
            completionTicks = 180;
        }

        private void SetError(string reason)
        {
            lastError = reason ?? "IC_PodUnknownError".Translate().Resolve();
            lifecycle = PodLifecycleState.Error;
            CleanupEffecter();
            Log.Error("[Identity Crisis] Pod stopped: " + lastError);
            Messages.Message("IC_PodFailed".Translate(lastError), this, MessageTypeDefOf.RejectInput, false);
        }

        public void Eject()
        {
            if (Occupant != null)
            {
                if (Map == null) { SetError("IC_PodEjectFailed".Translate().Resolve()); return; }
                innerContainer.TryDropAll(InteractionCell, Map, ThingPlaceMode.Near);
                if (Occupant != null) { SetError("IC_PodEjectFailed".Translate().Resolve()); return; }
                if (!Destroyed) SoundDefOf.CryptosleepCasket_Eject.PlayOneShot(SoundInfo.InMap(new TargetInfo(Position, Map)));
            }
            CleanupEffecter();
            ResetState();
        }

        private void CancelPending()
        {
            if (Occupant == null) ResetState();
        }

        private void ResetState()
        {
            pendingPawn = null;
            config = null;
            ticksRemaining = -1;
            originalRace = null;
            originalGender = Gender.None;
            activeConfig = null;
            activeDuration = 0;
            activeBloodCost = 0;
            applyAttempted = false;
            userPaused = false;
            lastError = null;
            lifecycle = PodLifecycleState.Idle;
            completionTicks = 0;
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
                if (lifecycle == PodLifecycleState.Error)
                {
                    yield return new Command_Action { defaultLabel = "IC_PodReset".Translate(),
                        defaultDesc = "IC_PodResetDesc".Translate(), action = ResetState };
                    yield break;
                }
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

            if (CanEditSetup) yield return new Command_Action
            {
                defaultLabel = "IC_CommandEditSetup".Translate(),
                defaultDesc = "IC_CommandEditSetupDesc".Translate(target.LabelShort),
                icon = IC_Textures.Edit,
                action = () => Find.WindowStack.Add(new Dialog_AssimilationSetup(this, target, config))
            };

            if (occupant != null)
            {
                if (!applyAttempted && lifecycle != PodLifecycleState.Error)
                    yield return new Command_Action
                    {
                        defaultLabel = (userPaused ? "IC_PodResume" : "IC_PodPause").Translate(),
                        defaultDesc = "IC_PodPauseDesc".Translate(),
                        action = () => userPaused = !userPaused
                    };
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
            sb.AppendLine(("IC_PodState" + lifecycle).Translate());
            if (!lastError.NullOrEmpty()) sb.AppendLine("IC_PodErrorDetail".Translate(lastError));
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
            Scribe_Values.Look(ref lifecycle, "lifecycle", PodLifecycleState.Idle);
            Scribe_Deep.Look(ref activeConfig, "activeConfig");
            Scribe_Values.Look(ref activeDuration, "activeDuration", 0);
            Scribe_Values.Look(ref activeBloodCost, "activeBloodCost", 0);
            Scribe_Values.Look(ref activeEffectIntensity, "activeEffectIntensity", 1f);
            Scribe_Values.Look(ref applyAttempted, "applyAttempted", false);
            Scribe_Values.Look(ref userPaused, "userPaused", false);
            Scribe_Values.Look(ref lastError, "lastError");
            Scribe_Values.Look(ref completionTicks, "completionTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (innerContainer == null) innerContainer = new ThingOwner<Pawn>(this, true, LookMode.Deep);
                if (Occupant != null && (activeConfig == null || activeDuration <= 0 || applyAttempted))
                {
                    lifecycle = PodLifecycleState.Error;
                    lastError = "IC_PodLoadUnsafe".Translate().Resolve();
                }
                else if (Occupant == null && pendingPawn != null) lifecycle = PodLifecycleState.Waiting;
            }
        }
    }
}
