using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Enum;
using NINA.Core.Utility;
using System.Text.RegularExpressions;
using NINA.Sequencer.Container;
using NINA.Core.Utility.Converters;
using System.Diagnostics;
using NINA.Sequencer.Conditions;
using System.Runtime.Serialization;
using System.Windows.Controls;
using System.Collections.ObjectModel;
using System.Linq;
using Accord.IO;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "For Each")]
    [ExportMetadata("Description", "Iterates over a list of Variables and Expressions, executing the embedded instructions for each")]
    [ExportMetadata("Icon", "ArraySVG")]
    [ExportMetadata("Category", "Sequencer+ (Fun-ctions)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]

    public class ForEachList : SequentialContainer, IValidatable {

        [ImportingConstructor]
        public ForEachList() {
            Add(new AssignVariables() { Name = "Assign Variables" });
        }

        public ForEachList(ForEachList copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Variable = copyMe.Variable;
                ListExpression = copyMe.ListExpression;
            }
        }

        [OnSerializing]
        public void OnSerializing(StreamingContext context) {
            ISequenceItem toRemove = null;
            foreach (ISequenceItem item in Items) {
                if (item is AssignVariables) {
                    toRemove = item;
                }
            }
            if (toRemove != null) {
                Items.Remove(toRemove);
            }
            Conditions.Clear();
        }

        [OnSerialized]
        public void OnSerialized(StreamingContext context) {
            OnDeserialized(context);
            if (Conditions.Count == 0) {
                Add(new LoopCondition());
            }
        }

        [OnDeserialized]
        public new void OnDeserialized(StreamingContext context) {
            if (Items.Count == 0 || !(Items[0] is AssignVariables)) {
                AssignVariables av = new AssignVariables() { Name = "Assign Variables" };
                Items.Insert(0, av);
                av.AttachNewParent(this);
                Logger.Warning("AssignVariables wasn't found in ForEach, adding...");
            }
        }


        public override Task Interrupt() {
            return this.Parent?.Interrupt();
        }

        [JsonProperty]
        public IfContainer Instructions { get; protected set; }

        public override object Clone() {
            ForEachList ic = new ForEachList(this);
            ic.Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem));
            foreach (var item in ic.Items) {
                item.AttachNewParent(ic);
            }
            AttachNewParent(Parent);
            if (ic.Conditions.Count == 0) {
                ic.Add(new LoopCondition());
            }
            return ic;
        }

        private string variable = "";

        [JsonProperty]
        public string Variable {
            get => variable;
            set {
                if (Parent == null) {
                    //return;
                }
                variable = value;
                RaisePropertyChanged();
            }
        }

        private string listExpression = "";

        [JsonProperty]
        public string ListExpression {
            get => listExpression;
            set {
                if (Parent == null) {
                    //return;
                }
                listExpression = value;
                RaisePropertyChanged();
            }
        }

        public string[] ETokens;
        public string[] VTokens;

        public string ValidateArguments () {
            
            ETokens = ListExpression.Split(";", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            VTokens = Variable.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (ETokens.Length == 0 || VTokens.Length == 0) {
                return "There must be at least one Variable and List Expression";
            }

            foreach (string et in ETokens) {
                string[] ets = et.Split(",", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (ets.Length != VTokens.Length) {
                    return "Each group in the Expression list must have " + VTokens.Length + " items; one for each Variable";
                }
            }

            return null;
        }

        public override void ResetProgress() {
            base.ResetProgress();
            LoopCondition lp = Conditions[0] as LoopCondition;
            lp.CompletedIterations = 0;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ForEachList)}, Variable: {Variable}, List Expression: {ListExpression}";
        }

        public IList<string> Switches { get; set; } = null;

        protected bool IsAttachedToRoot() {
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is SequenceRootContainer) {
                    return true;
                }
                p = p.Parent;
            }
            return false;
        }

        public new bool Validate() {

            var i = new List<string>();
            if (!IsAttachedToRoot()) return true;

            string e = ValidateArguments();
            if (e != null) {
                i.Add(e);
            } else if (Conditions.Count > 0) {
                LoopCondition lp = Conditions[0] as LoopCondition;
                if (lp != null) {
                    lp.Iterations = ETokens.Length;
                }
            }

            foreach (ISequenceItem item in Items) {
                if (item is IValidatable v) {
                    _ = v.Validate();
                }
            }

            Switches = Symbol.GetSwitches();
            RaisePropertyChanged("Switches");

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }
    }
}
