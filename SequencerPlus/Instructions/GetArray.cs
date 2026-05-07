using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using NINA.Sequencer.Validations;
using System.Collections.Generic;
using System.Reflection;
using Antlr.Runtime;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Get from Array")]
    [ExportMetadata("Description", "Gets a value from an Array at the specified index into a Variable")]
    [ExportMetadata("Icon", "ArraySVG")]
    [ExportMetadata("Category", "Sequencer+ (Fun-ctions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]

    public class GetArray : SequenceItem, IValidatable {

        [ImportingConstructor]
        public GetArray() : base() {
            Name = Name;
            Icon = Icon;
            IExpr = new Expr(this);
            VExpr = new Expr(this);
            NameExpr = new Expr(this);
        }

        public GetArray(GetArray copyMe) : base(copyMe) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Name = copyMe.Name;
                Icon = copyMe.Icon;
            }
        }

        public override object Clone() {
            GetArray clone = new GetArray(this);
            clone.IExpr = new Expr(clone, this.IExpr.Expression, "Any");
            clone.VExpr = new Expr(clone, this.VExpr.Expression);
            clone.NameExpr = new Expr(clone, this.NameExpr.Expression);
            return clone;
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
        private Expr _IExpr = null;

        [JsonProperty]
        public Expr IExpr {
            get => _IExpr;
            set {
                _IExpr = value;
                RaisePropertyChanged();
            }
        }

        private Expr _VExpr = null;

        [JsonProperty]
        public Expr VExpr {
            get => _VExpr;
            set {
                _VExpr = value;
                RaisePropertyChanged();
            }
        }

        public static readonly String VALID_SYMBOL = "^[a-zA-Z][a-zA-Z0-9-+_]*$";

        [JsonProperty]
        public string Identifier {
            get { return null; }
            set {
                NameExpr.Expression = value;
            }
        }
        public override string ToString() {
            return $"Get Array: {NameExpr.StringValue} at {IExpr.Value}, into Variable {VExpr.Expression}";
        }

        private IList<string> issues = new List<string>();
        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public bool Validate() {
            IList<string> i = new List<string>();

            if (NameExpr.StringValue != null) {
                if (NameExpr.StringValue.Length == 0) {
                    i.Add("A name for the Array must be specified");
                } else if (!Regex.IsMatch(NameExpr.StringValue, VALID_SYMBOL)) {
                    i.Add("The name of an Array must be alphanumeric");
                    //} else if (!Symbol.Arrays.ContainsKey(Identifier)) {
                    //    i.Add("The Array named '" + Identifier + "' has not been initialized");
                } else if (IExpr != null && IExpr.Expression != null && IExpr.Expression.Length == 0) {
                    i.Add("The Array index must be specified");
                }
            }

            Expr.AddExprIssues(i, IExpr, VExpr, NameExpr);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Symbol.Array arr;
            if (!Symbol.Arrays.TryGetValue(NameExpr.StringValue, out arr)) {
                throw new SequenceEntityFailedException("The Array named '" + Identifier + " has not been initialized");
            }
            object value;
            if (!arr.TryGetValue(IExpr.ValueString, out value)) {
                Logger.Warning("There is no value at index " + (int)IExpr.Value + " in Array " + Identifier + "; returning -1");
                value = -1;
                //throw new SequenceEntityFailedException("There was no value for index " + IExpr.Value + " in Array " + Identifier);
            }

            string resultName = VExpr.Expression;
            if (resultName == null || resultName.Length == 0) {
                throw new SequenceEntityFailedException("There must be a result Variable specified in order to use the Get from Array instruction");
            }
            Symbol sym = Symbol.FindSymbol(resultName, Parent);
            if (sym != null && sym is SetVariable sv) {
                if (value is string vs) {
                    value = "'" + vs + "'";
                }
                sv.Definition = value.ToString();
                Logger.Info("Setting Variable " + sv + " to " + value);
            } else {
                throw new SequenceEntityFailedException("Result Variable is not defined: " + VExpr.Expression);
            }

            return Task.CompletedTask;
        }
    }
}
