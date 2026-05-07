using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "DIY Trigger")]
    [ExportMetadata("Description", "This trigger will run the specified instructions when the underlying trigger is activated.")]
    [ExportMetadata("Icon", "WandSVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class DIYTrigger : SequenceTrigger, IValidatable {


        [ImportingConstructor]
        public DIYTrigger() {
            DropIntoDIYTriggersCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropInSequenceTrigger);
        }

        public override bool AllowMultiplePerSet => true;

        public ICommand DropIntoDIYTriggersCommand { get; set; }

        public override object Clone() {
            var clone = new DIYTrigger() {
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

        /// <summary>
        /// The actual running logic for when the trigger should run
        /// </summary>
        /// <param name="context"></param>
        /// <param name="progress"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            TriggerRunner.AttachNewParent(context);

            try {
                Logger.Info("DIY Trigger executing...");
                await TriggerRunner.Run(progress, token);
            } finally {
                InFlight = false;
                TriggerRunner.Parent?.Remove(TriggerRunner);
                TriggerRunner.AttachNewParent(Parent);
            }
        }
        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight) return false;
            if (TriggerRunner.Triggers.FirstOrDefault() == null) return false;
            var trigger = TriggerRunner.Triggers.FirstOrDefault();
            var result = trigger.ShouldTrigger(previousItem, nextItem);
            if (result) {
                Logger.Info("DIY Trigger " + trigger.Name + " ShouldTrigger returning true");
            }
            return result;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight) return false;
            if (TriggerRunner.Triggers.FirstOrDefault() == null) return false;
            var trigger = TriggerRunner.Triggers.FirstOrDefault();
            var result = trigger.ShouldTriggerAfter(previousItem, nextItem);
            if (result) {
                Logger.Info("DIY Trigger " + trigger.Name + " ShouldTriggerAfter returning true");
            }
            return result;
        }

        // Per Nick Holland
        public override void SequenceBlockInitialize() {
            if (!InFlight && TriggerRunner.Triggers.FirstOrDefault() != null)
                TriggerRunner.Triggers.FirstOrDefault().SequenceBlockInitialize();
        }

        public override void AfterParentChanged() {
            foreach (ISequenceTrigger item in TriggerRunner.Triggers) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            foreach (ISequenceItem item in TriggerRunner.Items) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            TriggerRunner.AttachNewParent(Parent);
            if (TriggerRunner.Triggers.Count > 0) {
                TriggerRunner.Triggers[0].AfterParentChanged();
            }
        }
        public virtual bool Validate() {
            // Validate the Items (this will update their status)
            if (TriggerRunner == null) return true;
            foreach (ISequenceTrigger item in TriggerRunner.Triggers) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }
            foreach (ISequenceItem item in TriggerRunner.Items) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }
            return true;
        }
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(DIYTrigger)}";
        }
    }
}
