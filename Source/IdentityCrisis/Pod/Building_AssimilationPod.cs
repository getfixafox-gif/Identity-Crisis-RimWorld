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
        private int planSeed;
        private bool userPaused;
        private bool applyAttempted;
        private string lastError;

        public PodLifecycleState State => lifecycle;
        public string LastError => lastError;
        public bool CanEditSetup => Occupant == null && !applyAttempted;
        public int ActiveDuration => activeDuration;
        public int ActiveBloodCost => activeBloodCost;

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
            return pawn != null && pawn == pendingPawn && lifecycle == PodLifecycleState.Waiting && Occupant == null && (pawn.IsPrisonerOfColony || pawn.Downed);
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
            // Never abandon a pawn in a detached holder if placement failed.
            if (Occupant != null) return;
            CleanupEffecter();
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
            if (pawn == null || newConfig == null || !CanEditSetup || (pendingPawn != null && pendingPawn != pawn))
            {
                return;
            }
            pendingPawn = pawn;
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
            if (!Spawned || Destroyed || lifecycle == PodLifecycleState.Error || applyAttempted)
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
            // Resolve automatic choices once, before moving or charging the pawn.
            planSeed = Rand.Int;
            TransformationPlan plan;
            Rand.PushState(planSeed);
            try { plan = RaceSwapper.BuildPlan(pawn, config.Copy()); }
            catch (System.Exception e)
            {
                SetError(e.Message);
                return false;
            }
            finally { Rand.PopState(); }
            if (plan == null || !plan.CanApply || plan.ResolvedConfig == null)
            {
                SetError(plan?.Error ?? "IC_PodInvalidPlan".Translate().Resolve());
                return false;
            }
            activeConfig = plan.ResolvedConfig.Copy();
            activeDuration = TicksToComplete;
            activeBloodCost = BloodRequired;
            activeEffectIntensity = Mathf.Clamp(IdentityCrisisMod.Settings?.podEffectIntensity ?? 1f, 0f, 4f);
            pendingType = ConversionUtility.Classify(pawn);
            originalRace = pawn.def;
            originalGender = pawn.gender;
            bool wasSelected = Find.Selector.IsSelected(pawn);
            Map entryMap = pawn.MapHeld;
            IntVec3 entryCell = pawn.PositionHeld;
            pawn.DeSpawnOrDeselect();
            if (!innerContainer.TryAddOrTransfer(pawn))
            {
                // A failed transfer mus

[Truncated after 206 lines. To continue, call readFile again with offset=207.]