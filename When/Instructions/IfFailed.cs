using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Sequencer.DragDrop;
using System.Windows.Input;

namespace WhenPlugin.When {
    [ExportMetadata("Name", "If Fails")]
    [ExportMetadata("Description", "Executes an instruction set if the predicate instruction failed.")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Powerups (Expressions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class IfFailed : IfCommand {

        [ImportingConstructor]
        public IfFailed() {
            Condition = new IfContainer();
            Condition.AttachNewParent(Parent);
            Condition.PseudoParent = this;
            Condition.Name = Name;
            Condition.Icon = Icon;
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            Instructions.Name = Name;
            Instructions.Icon = Icon;
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoCondition);
        }
        public IfFailed(IfFailed copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Condition = (IfContainer)copyMe.Condition.Clone();
                Condition.AttachNewParent(Parent);
                Condition.PseudoParent = this;
                Condition.Name = Name;
                Condition.Icon = Icon;
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
                Instructions.Name = Name;
                Instructions.Icon = Icon;
            }
        }

        public override object Clone() {
            return new IfFailed(this) {
            };
        }

        public ICommand DropIntoIfCommand { get; set; }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            ISequenceItem condition = Condition.Items[0];

            if (condition == null) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            while (true) {
                // Execute the conditional
                condition.Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
                await condition.Run(progress, token);

                if (condition.Status != NINA.Core.Enum.SequenceEntityStatus.FAILED) {
                    return;
                }

                Log("IfFailed - Triggered by: " + condition.Name);

                await Instructions.Run(progress, token);

                return;
            }
        }

        // Allow only ONE instruction to be added to Condition
        public void DropIntoCondition (DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceItem item;
                var source = parameters.Source as ISequenceItem;

                if (source.Parent != null && !parameters.Duplicate) {
                    item = source;
                } else {
                    item = (ISequenceItem)source.Clone();
                }

                if (item.Parent != Condition) {
                    item.Parent?.Remove(item);
                    item.AttachNewParent(Condition);
                }

                Condition.Items.Clear();
                Condition.Items.Add(item);
           }
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            foreach (ISequenceItem item in Condition.Items) {
                item.AfterParentChanged();
            }
            foreach (ISequenceItem item in Instructions.Items) {
                item.AfterParentChanged();
            }
        }

        public override void ResetAll() {
            base.ResetAll();
            Condition.ResetAll();
        }

        public override bool Validate() {
            CommonValidate();
            return true;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfFailed)}";
        }
    }
}
