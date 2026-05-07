using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System.ComponentModel.Composition;
using System.Threading;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.Sequencer;
using NINA.ViewModel.Sequencer;
using System.Reflection;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Core.Enum;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Reset Sequence")]
    [ExportMetadata("Description", "Resets the running sequence")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    //[Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ResetSequence : SequenceItem {

        [ImportingConstructor]
        public ResetSequence() {
        }

        public ResetSequence(ResetSequence copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            ISequenceEntity p = Parent;
            while (p != null) {
                if (p is SequenceRootContainer) {
                    ((ISequenceContainer)p).ResetAll();
                    break;
                }
                p = p.Parent;
            }
            return Task.CompletedTask;
        }

        public override object Clone() {
            return new ResetSequence(this) {
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ResetSequence)}";
        }
    }
}
