using Verse;

namespace IdentityCrisis
{
    // The gender shift trauma remembers which sex the pawn lived in and which one they got,
    // so the health tab shows what was changed into what.
    public class Hediff_GenderShift : HediffWithComps
    {
        public Gender fromGender;
        public Gender toGender;

        public override string LabelInBrackets
        {
            get
            {
                string baseLabel = base.LabelInBrackets;
                if (fromGender == Gender.None || toGender == Gender.None || fromGender == toGender)
                {
                    return baseLabel;
                }
                string change = fromGender.GetLabel() + " → " + toGender.GetLabel();
                return baseLabel.NullOrEmpty() ? change : baseLabel + ", " + change;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref fromGender, "fromGender", Gender.None);
            Scribe_Values.Look(ref toGender, "toGender", Gender.None);
        }
    }
}
