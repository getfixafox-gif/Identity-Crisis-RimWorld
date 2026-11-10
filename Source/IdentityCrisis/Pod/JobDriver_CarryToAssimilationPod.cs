using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace IdentityCrisis
{
    // A warden carries a prisoner or a downed pawn into the pod.
    public class JobDriver_CarryToAssimilationPod : JobDriver
    {
        private const TargetIndex TakeeInd = TargetIndex.A;
        private const TargetIndex PodInd = TargetIndex.B;

        private Pawn Takee => (Pawn)job.GetTarget(TakeeInd).Thing;

        private Building_AssimilationPod Pod => (Building_AssimilationPod)job.GetTarget(PodInd).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Takee, job, 1, -1, null, errorOnFailed) && pawn.Reserve(Pod, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TakeeInd);
            this.FailOnDespawnedOrNull(PodInd);
            this.FailOn(() => Pod == null || !Pod.NeedsCarrying(Takee) || !Pod.CanAcceptPawn(Takee).Accepted);

            yield return Toils_Goto.GotoThing(TakeeInd, PathEndMode.OnCell)
                .FailOnDespawnedNullOrForbidden(TakeeInd)
                .FailOnSomeonePhysicallyInteracting(TakeeInd);
            yield return Toils_Haul.StartCarryThing(TakeeInd);
            yield return Toils_Goto.GotoThing(PodInd, PathEndMode.InteractionCell);

            Toil prepare = Toils_General.Wait(60);
            prepare.WithProgressBarToilDelay(PodInd);
            yield return prepare;

            Toil put = ToilMaker.MakeToil("PutIntoAssimilationPod");
            put.initAction = () => { if (!Pod.TryAcceptPawn(Takee)) EndJobWith(JobCondition.Incompletable); };
            put.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return put;
        }
    }
}
