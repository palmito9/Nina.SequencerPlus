using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Threading;
using System.Windows.Input;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Equipment.MySafetyMonitor;
using NINA.Core.Enum;

namespace NINA.Plugin.SequencerPlus {
 
    [ExportMetadata("Name", "Safe Trigger")]
    [ExportMetadata("Description", "The specified trigger will run ONLY if the safety monitor reports 'Safe' conditions.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [ExportMetadata("Category", "Sequencer+ (Safety)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class SafeTrigger : SequenceTrigger, IValidatable {

        private ISafetyMonitorMediator safetyMediator;

        [ImportingConstructor]
        public SafeTrigger(ISafetyMonitorMediator safetyMediator) {
            this.safetyMediator = safetyMediator;
            SafeTriggerCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropInSequenceTrigger);
        }

        public override bool AllowMultiplePerSet => true;

        public ICommand SafeTriggerCommand { get; set; }

        public override object Clone() {
            var clone = new SafeTrigger(safetyMediator) {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };

            return clone;
        }

        private static object lockObj = new object();
        public bool InFlight { get; set; }

        private void DropInSequenceTrigger(DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceTrigger item;
                var source = parameters.Source as ISequenceTrigger;

                if (source.Parent != null && !parameters.Duplicate) {
                    item = source;
                } else {
                    item = (ISequenceTrigger)source.Clone();
                }

                if (item.Parent != TriggerRunner) {
                    item.Parent?.Remove(item);
                    item.AttachNewParent(TriggerRunner);
                }

                TriggerRunner.Triggers.Clear();
                TriggerRunner.Triggers.Add(item);
            }
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = ImmutableList.CreateRange(value);
                RaisePropertyChanged();
            }
        }

        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            TriggerRunner.AttachNewParent(context);

            try {
                SequenceTrigger trigger = (SequenceTrigger)TriggerRunner.Triggers.FirstOrDefault();
                if (trigger != null) {
                    await trigger.Execute(context, progress, token);
                }
                //await TriggerRunner.Run(progress, token);
            } finally {
                InFlight = false;
                TriggerRunner.Parent?.Remove(TriggerRunner);
                TriggerRunner.AttachNewParent(Parent);
            }
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight) return false;
            SafetyMonitorInfo info = safetyMediator.GetInfo();
            if (!info.IsSafe) return false;
            if (TriggerRunner.Triggers.FirstOrDefault() == null) return false;
            return TriggerRunner.Triggers.FirstOrDefault().ShouldTrigger(previousItem, nextItem);
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight) return false;
            SafetyMonitorInfo info = safetyMediator.GetInfo();
            if (!info.IsSafe) return false;
            if (TriggerRunner.Triggers.FirstOrDefault() == null) return false;
            return TriggerRunner.Triggers.FirstOrDefault().ShouldTriggerAfter(previousItem, nextItem);
        }

        public override void AfterParentChanged() {
            foreach (ISequenceTrigger item in TriggerRunner.Triggers) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            foreach (ISequenceItem item in TriggerRunner.Items) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            TriggerRunner.AttachNewParent(Parent);
        }

        public virtual bool Validate() {
            IList<string> i = new List<string>();

            // Validate the Items (this will update their status)
            if (TriggerRunner == null) return true;
            foreach (ISequenceTrigger item in TriggerRunner.Triggers) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }
           
            if (TriggerRunner.Triggers.FirstOrDefault() == null) {
                i.Add("No trigger has been specified");
            }

            SafetyMonitorInfo info = safetyMediator.GetInfo();
            if (!info.Connected) {
                i.Add("Safety monitor not connected");
            }

            Issues = i;
            return i.Count == 0;
        }

        /// <summary>
        /// This string will be used for logging
        /// </summary>
        /// <returns></returns>
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(SafeTrigger)}";
        }
    }
}