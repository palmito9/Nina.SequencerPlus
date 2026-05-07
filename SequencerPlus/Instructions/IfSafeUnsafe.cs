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
using System.Threading;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Equipment.MySafetyMonitor;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "")]
    [ExportMetadata("Description", "")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public abstract class IfSafeUnsafe : IfCommand, IValidatable {

        protected ISafetyMonitorMediator safetyMediator;
        protected bool isSafe = true;

        [ImportingConstructor]
        public IfSafeUnsafe(ISafetyMonitorMediator safetyMediator, bool isSafe) {
            Instructions = new IfContainer();
            this.safetyMediator = safetyMediator;
            this.isSafe = isSafe;
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
        }
        public IfSafeUnsafe(IfSafeUnsafe copyMe) : this(copyMe.safetyMediator, copyMe.isSafe) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
            }
        }

        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {

            while (true) {
                bool safe = WhenUnsafe.CheckSafe(this, safetyMediator);

                if (isSafe && !safe) return;
                if (!isSafe && safe) return;

                Logger.Info(Name + " true; triggered.");

                Runner runner = new Runner(Instructions, progress, token);
                await runner.RunConditional();
                if (runner.ShouldRetry) {
                    runner.ResetProgress();
                    runner.ShouldRetry = false;
                    Logger.Info(Name + "; retrying...");
                    continue;
                }

                return;
            }
        }

        public override void ResetProgress() {
            base.ResetProgress();
            Instructions.ResetProgress();
        }

        public new bool Validate() {
            CommonValidate();

            bool valid = base.Validate();
            return Issues.Count == 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {Name}";
        }
    }
}
