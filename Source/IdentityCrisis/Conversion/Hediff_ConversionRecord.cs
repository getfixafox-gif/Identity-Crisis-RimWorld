using Verse;

namespace IdentityCrisis
{
    // Hidden permanent record of a conversion. Source of truth for converts vs purebloods.
    public class Hediff_ConversionRecord : HediffWithComps
    {
        public ThingDef originalRace;
        public Gender originalGender;
        public ConversionType conversionType;
        public int tickConverted;
        public int timesConverted;

        public override bool Visible => false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref originalRace, "originalRace");
            Scribe_Values.Look(ref originalGender, "originalGender", Gender.None);
            Scribe_Values.Look(ref conversionType, "conversionType", ConversionType.Voluntary);
            Scribe_Values.Look(ref tickConverted, "tickConverted", 0);
            Scribe_Values.Look(ref timesConverted, "timesConverted", 0);
        }
    }
}
