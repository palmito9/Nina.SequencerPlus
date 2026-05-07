using Newtonsoft.Json;
using NINA.Core.Utility;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus {
    public abstract class IfCommand : SequenceItem, ISequenceContainer, IValidatable {

        [JsonProperty]
        public IfContainer Condition { get; protected set; }
        
        [JsonProperty]
        public IfContainer Instructions { get; protected set; }

        public IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public IList<ISequenceItem> Items => ((ISequenceContainer)Instructions).Items;

        public bool IsExpanded { get => ((ISequenceContainer)Instructions).IsExpanded; set => ((ISequenceContainer)Instructions).IsExpanded = value; }
        public int Iterations { get => ((ISequenceContainer)Instructions).Iterations; set => ((ISequenceContainer)Instructions).Iterations = value; }

        public IExecutionStrategy Strategy => ((ISequenceContainer)Instructions).Strategy;

        public  static object lockObj = new object();

        public bool HasSpecialChars(string str) {
            return str.Any(ch => !char.IsLetterOrDigit(ch));
        }

        public string RemoveSpecialCharacters(string str) {
            StringBuilder sb = new StringBuilder();
            foreach (char c in str) {
                if ((c >= '0' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '.' || c == '_') {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        public override void ResetProgress() {
            base.ResetProgress();
            Instructions.ResetAll();
            foreach (ISequenceItem item in Instructions.Items) {
                item.ResetProgress();
            }
            if (Condition != null && !(Condition.Items == null || Condition.Items.Count == 0)) {
                Condition.Items[0].Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
            }
        }

        public void Log(string str) {
            Log(str, true);
        }
        
        public void Log(string str, bool success) {
            Logger.Info(str);
            // Notification for debugging...
            //if (success) {
            //    Notification.ShowSuccess(str);
            //} else {
            //    Notification.ShowWarning(str);
            //}
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            foreach (var item in Instructions.Items) {
                item.AfterParentChanged();
            }
            if (Condition != null) {
                foreach (ISequenceItem item in Condition.Items) {
                    item.AfterParentChanged();
                }
            }
        }

        protected void CommonValidate() {
            ValidateInstructions(Instructions);
        }

        protected void ValidateInstructions(IfContainer instructions) {
            try {
                if (instructions.PseudoParent == null) {
                    instructions.PseudoParent = this;
                }

                // Avoid infinite loop by checking first...
                if (instructions.Parent != Parent) {
                    instructions.AttachNewParent(Parent);
                }

                foreach (ISequenceItem item in instructions.Items) {
                    if (item is IValidatable val) {
                        _ = val.Validate();
                    }

                }

                if (Condition != null) {
                    if (Condition.Parent != Parent) {
                        Condition.AttachNewParent(Parent);
                    }
                    foreach (ISequenceItem item in Condition.Items) {
                        if (item is IValidatable val) {
                            _ = val.Validate();
                        }
                    }
                }
            } catch (Exception ex) {
                Logger.Info("Exception in ValidateInstructions: " + ex.Message);
            }
        }

        public override void Initialize() {
            base.Initialize();
            Instructions.Initialize();
        }

        public virtual bool Validate() {
            CommonValidate();

            var i = new List<string>();
            if (Condition == null) { }
            else if (Condition.Items == null || Condition.Items.Count == 0) {
                i.Add("The instruction to check must not be empty!");
            } else if (Condition.Items[0] is IValidatable val) {
                _ = val.Validate();
            }
 
            Issues = i;
            return i.Count == 0;
        }

        public void Add(ISequenceItem item) {
            ((ISequenceContainer)Instructions).Add(item);
        }

        public void MoveUp(ISequenceItem item) {
            ((ISequenceContainer)Instructions).MoveUp(item);
        }

        public void MoveDown(ISequenceItem item) {
            ((ISequenceContainer)Instructions).MoveDown(item);
        }

        public bool Remove(ISequenceItem item) {
            return ((ISequenceContainer)Instructions).Remove(item);
        }

        public bool Remove(ISequenceCondition item) {
            return ((ISequenceContainer)Instructions).Remove(item);
        }

        public bool Remove(ISequenceTrigger item) {
            return ((ISequenceContainer)Instructions).Remove(item);
        }

        public virtual void ResetAll() {
            ((ISequenceContainer)Instructions).ResetAll();
            Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
        }

        public Task Interrupt() {
            return ((ISequenceContainer)Instructions).Interrupt();
        }

        public ICollection<ISequenceItem> GetItemsSnapshot() {
            return ((ISequenceContainer)Instructions).GetItemsSnapshot();
        }
    }
}
