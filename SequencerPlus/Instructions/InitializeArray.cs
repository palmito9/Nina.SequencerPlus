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

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Initialize Array")]
    [ExportMetadata("Description", "Creates or re-initializes an Array")]
    [ExportMetadata("Icon", "ArraySVG")]
    [ExportMetadata("Category", "Sequencer+ (Fun-ctions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]

    public class InitializeArray : SequenceItem, IValidatable {

        [ImportingConstructor]
        public InitializeArray() : base() {
            Name = Name;
            Icon = Icon;
            NameExpr = new Expr(this);
        }
        public InitializeArray(InitializeArray copyMe) : base(copyMe) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Name = copyMe.Name;
                Icon = copyMe.Icon;
                NameExpr = new Expr(this, copyMe.NameExpr.Expression);
            }
        }

        public override object Clone() {
            InitializeArray clone = new InitializeArray(this);
            return clone;
        }

        public static readonly String VALID_SYMBOL = "^[a-zA-Z][a-zA-Z0-9-+_]*$";


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
        public string Identifier {
            get { return null;  }
            set {
                NameExpr.Expression = value;
            }
        }

        public override string ToString() {
                return $"Initialize Array: {NameExpr.StringValue}";
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
                }
            }

            Expr.AddExprIssues(i, NameExpr);

            Issues = i;
            RaisePropertyChanged("Issues");
            return i.Count == 0;
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Symbol.Array arr;
            if (Symbol.Arrays.TryGetValue(NameExpr.StringValue, out arr)) {
                arr.Clear();
            } else {
                Symbol.Arrays.TryAdd(NameExpr.StringValue, new Symbol.Array());
            }
            return Task.CompletedTask;
        }
    }
}
