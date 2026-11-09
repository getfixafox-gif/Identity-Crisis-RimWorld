using System.Collections.Generic;
using Verse;

namespace IdentityCrisis
{
    // What RaceSwapper did to the pawn. Filled during the transformation
    // and written to the pawn's Log tab by PlayLogEntry_Assimilation.
    public class AssimilationReport
    {
        public ThingDef fromRace;
        public ThingDef toRace;
        public Gender fromGender;
        public Gender toGender;
        public float fromAgeYears;
        public float toAgeYears;
        public List<string> implantsMoved = new List<string>();
        public List<string> implantsExtracted = new List<string>();
        public List<string> implantsLost = new List<string>();
        public List<string> healed = new List<string>();
        public List<string> genesRemoved = new List<string>();
        public List<string> gearRemoved = new List<string>();
    }
}
