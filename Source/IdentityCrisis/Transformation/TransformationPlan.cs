using Verse;

namespace IdentityCrisis
{
    // Session-only, single-use plan. Persist ResolvedConfig, not this object.
    public sealed class TransformationPlan
    {
        public bool CanApply { get; private set; }
        public string Error { get; private set; }
        public AssimilationConfig ResolvedConfig => resolved?.Copy();
        private readonly AssimilationConfig resolved;
        internal readonly Pawn Pawn;
        internal readonly ThingDef SourceRace;
        internal bool Consumed;

        internal TransformationPlan(Pawn pawn, AssimilationConfig config, string error)
        {
            Pawn = pawn;
            SourceRace = pawn?.def;
            resolved = config?.Copy();
            Error = error;
            CanApply = error == null && resolved != null;
        }
    }
}
