using Accord.Diagnostics;
using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Interrupt Trigger")]
    [ExportMetadata("Description", "This trigger will stop execution after the currently running instruction, allowing you to add whatever instructions you want before proceeding.")]
    [ExportMetadata("Icon", "SequenceSVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceTrigger))]
    
    [JsonObject(MemberSerialization.OptIn)]
    public class InterruptTrigger : SequenceTrigger, IValidatable, IDSOTargetProxy {

        private GeometryGroup HourglassIcon = (GeometryGroup)Application.Current.Resources["HourglassSVG"];

        [JsonProperty]
        public IfContainer Runner { get; set; }

        [ImportingConstructor]
        public InterruptTrigger() {
            Runner = new IfContainer();
            Runner.AttachNewParent(Parent);
            Runner.PseudoParent = this;
            AddItem(Runner, new WaitIndefinitely() { Name="Wait Indefinitely", Icon = HourglassIcon }); ;
        }

        private void AddItem(IfContainer runner, ISequenceItem item) {
            runner.Items.Add(item);
            item.AttachNewParent(runner);
        }

        private InterruptTrigger(InterruptTrigger copyMe) {
            CopyMetaData(copyMe);
            Name = copyMe.Name;
            Icon = copyMe.Icon;
            Runner = (IfContainer)copyMe.Runner.Clone();
            Runner.AttachNewParent(Parent);
            Runner.PseudoParent = this;
        }

        public override object Clone() {
            return new InterruptTrigger(this);
        }

        public bool InFlight { get; set; }

        public IList<string> Issues => new List<string>();

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            try {
                Target = DSOTarget.FindTarget(Parent);
                if (Target != null) {
                    Logger.Info("Found Target: " + Target);
                    UpdateChildren(Runner);
                }
                await Runner.Run(progress, token);
            } finally {
                //InFlight = false;
            }
        }


        public override void AfterParentChanged() {
            foreach (ISequenceTrigger item in Runner.Triggers) {
                if (item.Parent == null) item.AttachNewParent(Runner);
            }
            foreach (ISequenceItem item in Runner.Items) {
                if (item.Parent == null) item.AttachNewParent(Runner);
            }
            Runner.AttachNewParent(Parent);
        }

        public InputTarget DSOProxyTarget() {
            return Target;
        }
        
        public InputTarget Target = null;

        public InputTarget FindTarget(ISequenceContainer c) {
            while (c != null) {
                if (c is IDeepSkyObjectContainer dso) {
                    return dso.Target;
                } else {
                    c = c.Parent;
                }
            }
            return null;
        }

        private void UpdateChildren(ISequenceContainer c) {
            foreach (var item in c.Items) {
                item.AfterParentChanged();
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            return !InFlight;
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(InterruptTrigger)}";
        }

        public bool Validate() {
            // Make sure a proper tree is maintained
            foreach (ISequenceItem item in Runner.Items) {
                item.AttachNewParent(Runner);
            }
            try {
                Target = DSOTarget.FindTarget(Parent);
                if (Target != null) {
                    //Logger.Info("Found Target: " + Target);
                    UpdateChildren(Runner);
                }
            } finally {
                //InFlight = false;
            }

            Runner.Validate();

            return true;
        }
    }
}