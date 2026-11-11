using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace IdentityCrisis
{
    public class JobDriver_EnterAssimilationPod : JobDriver
    {
        private Building_AssimilationPod Pod => (Building_AssimilationPod)job.GetTarget(TargetIndex.A).Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => Pod.PendingPawn != pawn || !Pod.CanAcceptPawn(pawn).Accepted);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.InteractionCell);

            Toil prepare = Toils_General.Wait(60);
            prepare.FailOnCannotTouch(TargetIndex.A, PathEndMode.InteractionCell);
            prepare.WithProgressBarToilDelay(TargetIndex.A);
            yield return prepare;

            Toil enter = ToilMaker.MakeToil("EnterAssimilationPod");
            enter.initAction = () => Pod.TryAcceptPawn(pawn);
            enter.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return enter;
        }
    }
}
