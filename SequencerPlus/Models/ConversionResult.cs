using System.Collections.Generic;

namespace NINA.Plugin.SequencerPlus.Models {
    public class ConversionResult {
        public int Converted { get; set; }
        public int Skipped { get; set; }
        public int Errors { get; set; }
        public List<string> ErrorList { get; set; } = new List<string>();
    }
}
