using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
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
    [ExportMetadata("Name", "End Instruction Set")]
    [ExportMetadata("Description", "Ends the currenty running sequence; the End Sequence instructions will run")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class EndInstructionSet : SequenceItem, IValidatable {

        [ImportingConstructor]
        public EndInstructionSet() {
        }

        public EndInstructionSet(EndInstructionSet copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                InstructionSetName = copyMe.InstructionSetName;
            }
        }
        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        private string iInstructionSetName = "";
        [JsonProperty]
        public string InstructionSetName {
            get => iInstructionSetName;
            set {
                string name = value as string;
                iInstructionSetName = name;
                RaisePropertyChanged();
            }
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Logger.Info("EndInstructionSet running...");
            ISequenceContainer c = FindInstructionSet();
            if (c != null) {
                c.Interrupt();
                c.Status = SequenceEntityStatus.FINISHED;
            }
            return Task.CompletedTask;
        }

        public override object Clone() {
            return new EndInstructionSet(this) {
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(EndSequence)}";
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
        }

        public ISequenceContainer FindInstructionSet() {
            if (InstructionSetName == null) return null;
            ISequenceEntity p = Parent;
            while (p != null) {
                if (p.Name != null && p.Name.Trim().ToLower().Equals(InstructionSetName.Trim().ToLower(), StringComparison.OrdinalIgnoreCase)) {
                    if (p is ISequenceContainer c) {
                        return c;
                    }
                }
                if (p is IfContainer ifc && ifc.PseudoParent != null && ifc.PseudoParent is ISequenceContainer sc) {
                    p = sc;
                } else {
                    p = p.Parent;
                }
            }
            return null;
        }

        public List<string> InstructionSetNames {
            get {
                List<string> list = new List<string>();
                ISequenceContainer p = Parent;
                while (p != null) {
                    if (!(p is IfContainer) && p.Name != null && p.Name.Length > 0) {
                        list.Add(p.Name);
                    }
                    if (p is IfContainer ifc && ifc.PseudoParent != null && ifc.PseudoParent is ISequenceContainer sc) {
                        p = sc;
                    } else {
                        p = p.Parent;
                    }
                }
                return list;
            }
            set {}
        }

        public bool Validate() {
            var i = new List<string>();

            if (Symbol.IsAttachedToRoot(this)) {
                if (InstructionSetName == null || InstructionSetName.Length == 0) {
                    InstructionSetName = InstructionSetNames[0];
                } else if (FindInstructionSet() == null) {
                    i.Add("The instruction set name cannot be found!");
                }
            }

            Issues = i;
            return i.Count == 0;
        }
    }
}
