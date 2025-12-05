using Newtonsoft.Json;
using NINA.Core.Enum;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
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
    [ExportMetadata("Name", "Relaxed Loop")]
    [ExportMetadata("Description", "This trigger will run the specified instructions when the underlying trigger is activated.")]
    [ExportMetadata("Icon", "WandSVG")]
    [ExportMetadata("Category", "Powerups (Misc)")]
    [Export(typeof(ISequenceCondition))]
    [JsonObject(MemberSerialization.OptIn)]
    public class LoopTrigger : SequenceCondition, IValidatable {


        [ImportingConstructor]
        public LoopTrigger() {
            DropIntoDIYTriggersCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropInSequenceTrigger);
            TriggerRunner = new SequentialContainer();
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

        public SequentialContainer TriggerRunner { get; set; }

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
                    return vitem.Validate();
                }
            }
            return true;
        }
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(LoopTrigger)}";
        }

        public override bool Check(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (TriggerRunner.Conditions.Count == 0) return false;
            return TriggerRunner.Conditions[0].RunCheck(previousItem, nextItem);
        }
    }
}
