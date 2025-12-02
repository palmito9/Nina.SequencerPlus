using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Conditions;
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

namespace WhenPlugin.When {
    [ExportMetadata("Name", "Loop Trigger")]
    [ExportMetadata("Description", "This trigger will run the specified instructions when the underlying trigger is activated.")]
    [ExportMetadata("Icon", "WandSVG")]
    [ExportMetadata("Category", "Powerups (Misc)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class LoopTrigger : SequenceTrigger, IValidatable {


        [ImportingConstructor]
        public LoopTrigger() {
            DropIntoDIYTriggersCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropInSequenceTrigger);
        }

        public override bool AllowMultiplePerSet => true;

        public ICommand DropIntoDIYTriggersCommand { get; set; }

        public override object Clone() {
            var clone = new LoopTrigger() {
                Icon = Icon,
                Name = Name,
                Category = Category,
                Description = Description,
                TriggerRunner = (SequentialContainer)TriggerRunner.Clone()
            };

            return clone;
        }

        private static object lockObj = new object();

        private void DropInSequenceTrigger(DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceCondition item;
                var source = parameters.Source as ISequenceCondition;

                if (source.Parent != null && !parameters.Duplicate) {
                    item = source;
                } else {
                    item = (ISequenceCondition)source.Clone();
                }

                if (item.Parent != TriggerRunner) {
                    item.Parent?.Remove(item);
                    item.AttachNewParent(TriggerRunner);
                }

                TriggerRunner.Conditions.Clear();
                TriggerRunner.Conditions.Add(item);
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
            TriggerRunner.AttachNewParent(context);

            try {
                Parent.Interrupt();
            } finally {
                TriggerRunner.Parent?.Remove(TriggerRunner);
                TriggerRunner.AttachNewParent(Parent);
            }
        }
        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (TriggerRunner.Conditions.FirstOrDefault() == null) return false;
            var condition = TriggerRunner.Conditions.FirstOrDefault();
            var result = !CanContinue(TriggerRunner, previousItem, nextItem);
            if (result) {
                Logger.Info("Loop Trigger " + condition.Name + " ShouldTrigger returning true");
            }
            return result;
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (TriggerRunner.Conditions.FirstOrDefault() == null) return false;
            var condition = TriggerRunner.Conditions.FirstOrDefault();
            var result = !CanContinue(TriggerRunner, previousItem, nextItem);
            if (result) {
                Logger.Info("Loop Trigger " + condition.Name + " ShouldTriggerAfter returning true");
            }
            return result;
        }
        private bool CanContinue(ISequenceContainer container, ISequenceItem previousItem, ISequenceItem nextItem) {
            var conditionable = container as IConditionable;
            var canContinue = false;
            var conditions = conditionable?.GetConditionsSnapshot()?.Where(x => x.Status != SequenceEntityStatus.DISABLED).ToList();
            if (conditions != null && conditions.Count > 0) {
                canContinue = conditionable.CheckConditions(previousItem, nextItem);
            } else {
                canContinue = container.Iterations < 1;
            }

            if (container.Parent != null) {
                canContinue = canContinue && CanContinue(container.Parent, previousItem, nextItem);
            }

            return canContinue;
        }

        public override void AfterParentChanged() {
            foreach (ISequenceCondition item in TriggerRunner.Conditions) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            TriggerRunner.AttachNewParent(Parent);
            if (TriggerRunner.Conditions.Count > 0) {
                TriggerRunner.Conditions[0].AfterParentChanged();
            }
        }
        public virtual bool Validate() {
            // Validate the Items (this will update their status)
            if (TriggerRunner == null) return true;
            foreach (ISequenceCondition item in TriggerRunner.Conditions) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }
            return true;
        }
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(LoopTrigger)}";
        }
    }
}
