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
using System.Text;
using Antlr.Runtime;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "For Each in Array")]
    [ExportMetadata("Description", "Iterates over the elements of an Array, executing the embedded instructions for each")]
    [ExportMetadata("Icon", "ArraySVG")]
    [ExportMetadata("Category", "Sequencer+ (Fun-ctions)")]
    [Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [JsonObject(MemberSerialization.OptIn)]

    public class ForEachInArray : ForEachList, IValidatable {

        [ImportingConstructor]
        public ForEachInArray() : base() {
            NameExpr = new Expr(this);
        }

        public ForEachInArray(ForEachInArray copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                ValueVariable = copyMe.ValueVariable;
                IndexVariable = copyMe.IndexVariable;
            }
        }

        private string indexVariable = "";

        [JsonProperty]
        public string IndexVariable {
            get => indexVariable;
            set {
                indexVariable = value;
                RaisePropertyChanged();
            }
        }

        private string valueVariable = "";

        [JsonProperty]
        public string ValueVariable {
            get => valueVariable;
            set {
                valueVariable = value;
                RaisePropertyChanged();
            }
        }


        private Expr _NameExpr = null;

        [JsonProperty]
        public Expr NameExpr {
            get => _NameExpr;
            set {
                _NameExpr = value;
                RaisePropertyChanged();
            }
        }

        [JsonProperty]
        public string Array {
            get { return null; }
            set {
                NameExpr.Expression = value;
            }
        }

        public override object Clone() {
            ForEachInArray ic = new ForEachInArray(this);
            ic.Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem));
            foreach (var item in ic.Items) {
                item.AttachNewParent(ic);
            }
            AttachNewParent(Parent);
            if (ic.Conditions.Count == 0) {
                ic.Add(new LoopCondition());
            }
            ic.NameExpr = new Expr(ic, this.NameExpr.Expression);
            return ic;
        }

        public static readonly String VALID_SYMBOL = "^[a-zA-Z][a-zA-Z0-9-+_]*$";

        public new string ValidateArguments () {

            if (IndexVariable == null || IndexVariable.Length == 0) {
                return "There must be an index variable specified";
            }
            if (ValueVariable == null || ValueVariable.Length == 0) {
                return "There must be a value variable specified";
            }

            if (NameExpr.StringValue != null) {
                if (NameExpr.StringValue.Length == 0) {
                    return "A name for the Array must be specified";
                } else if (!Regex.IsMatch(NameExpr.StringValue, VALID_SYMBOL)) {
                    return "The name of an Array must be alphanumeric";
                 }
            }

            return null;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(ForEachInArray)}, IndexVariable: {IndexVariable}, ValueVariable: {ValueVariable}, Array: {NameExpr.Expression}";
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Symbol.Array a = new Symbol.Array();

            if (NameExpr.StringValue != null) {
                if (NameExpr.StringValue.Length == 0) {
                    throw new SequenceEntityFailedException("An Array must be specified and must have been initialized");
                }

                if (!Symbol.Arrays.ContainsKey(NameExpr.StringValue)) {
                    throw new SequenceEntityFailedException("The Array specified does not exist");
                }

                if (!Symbol.Arrays.TryGetValue(NameExpr.StringValue, out a)) {
                    throw new SequenceEntityFailedException("Huh?  Key exists but not Array??");
                }
            }

            ETokens = new string[a.Count];
            int i = 0;
            foreach (var kvp in a) {
                ETokens[i++] = kvp.Key + "," + kvp.Value;
            }

            if (Conditions.Count > 0) {
                LoopCondition lp = Conditions[0] as LoopCondition;
                if (lp != null) {
                    lp.Iterations = ETokens.Length;
                }
            }

            Variable = IndexVariable + "," + ValueVariable;
            StringBuilder sb = new StringBuilder();
            foreach (string e in ETokens) {
                sb.Append(e);
                sb.Append(";");
            }
            ListExpression = sb.ToString();

            return base.Execute(progress, token);
        }

        public new bool Validate() {

            var i = new List<string>();
            if (!IsAttachedToRoot()) return true;

            string e = ValidateArguments();
            if (e != null) {
                i.Add(e);
            }

            Expr.AddExprIssues(i, NameExpr);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }
    }
}
