using System.Collections.Generic;
using UnityEngine;
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
        // Traumas, the coma and other reactions added by ConversionUtility.ApplyConversion.
        public List<string> consequences = new List<string>();

        // "race: human → wolfein, gender: male → female, age: 17 → 21"
        public string DescribeChanges()
        {
            List<string> parts = new List<string>();
            parts.Add("IC_LogRace".Translate(fromRace != null ? fromRace.label : "?", toRace != null ? toRace.label : "?").Resolve());
            if (fromGender != toGender)
            {
                parts.Add("IC_LogGender".Translate(fromGender.GetLabel(), toGender.GetLabel()).Resolve());
            }
            int oldAge = Mathf.FloorToInt(fromAgeYears);
            int newAge = Mathf.FloorToInt(toAgeYears);
            if (oldAge != newAge)
            {
                parts.Add("IC_LogAge".Translate(oldAge.ToString(), newAge.ToString()).Resolve());
            }
            return string.Join(", ", parts);
        }
    }
}
