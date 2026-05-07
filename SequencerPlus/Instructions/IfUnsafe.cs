using Newtonsoft.Json;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System.ComponentModel.Composition;
using NINA.Equipment.Interfaces.Mediator;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "If Unsafe (Deprecated; use 'If !IsSafe')")]
    [ExportMetadata("Description", "Executes a customizable instruction set if the safety monitor indicates that conditions are unsafe.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [ExportMetadata("Category", "Sequencer+ (Safety)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class IfUnsafe : IfSafeUnsafe, IValidatable {
 
        [ImportingConstructor]
        public IfUnsafe(ISafetyMonitorMediator safetyMediator) : base(safetyMediator, false) {
            Instructions.Name = Name;
            Instructions.Icon = Icon;
        }

        public IfUnsafe(IfUnsafe copyMe) : base(copyMe) {
            Instructions.Name = Name;
            Instructions.Icon = Icon;
        }

        public override object Clone() {
            return new IfUnsafe(this) {
            };
        }
    }
}
