using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Equipment.MySafetyMonitor;
using NINA.Sequencer.Container;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "If Safe (Deprecated; use 'If IsSafe')")]
    [ExportMetadata("Description", "Executes a customizable instruction set if the safety monitor indicates that conditions are safe.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [ExportMetadata("Category", "Sequencer+ (Safety)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class IfSafe : IfSafeUnsafe, IValidatable {

        [ImportingConstructor]
        public IfSafe(ISafetyMonitorMediator safetyMediator) : base(safetyMediator, true) {
            Instructions.Name = Name;
            Instructions.Icon = Icon;
        }

        public IfSafe(IfSafe copyMe) : base(copyMe) {
            Instructions.Name = Name;
            Instructions.Icon = Icon;
        }

        public SequenceContainer X { get; set; }


        public override object Clone() {
            return new IfSafe(this) {
            };
        }
        
    }
}
