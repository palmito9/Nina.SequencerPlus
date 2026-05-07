using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using NINA.Sequencer.Container;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Global Variable")]
    [ExportMetadata("Description", "Creates a global Variable whose numeric value can be used in various instructions")]
    [ExportMetadata("Icon", "VariableSVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]

    public class SetGlobalVariable : SetVariable {

        [ImportingConstructor]
        public SetGlobalVariable() : base() {
            IsGlobalVariable = true; 
            OriginalExpr = new Expr(GlobalVariables);
        }
        public SetGlobalVariable(SetGlobalVariable copyMe) : base(copyMe) {
            if (copyMe != null) {
                IsGlobalVariable = true;
            }
        }

        public SetGlobalVariable(string id, string def, ISequenceContainer parent) {
            SetGlobalVariable sv = new SetGlobalVariable();
            sv.AttachNewParent(parent);
            sv.Identifier = id;
            sv.Definition = def;
            sv.Executed = true;
        }

        public override object Clone() {
            SetGlobalVariable clone = new SetGlobalVariable(this);
            clone.Identifier = Identifier;
            clone.Definition = Definition;
            clone.OriginalExpr = new Expr(OriginalExpr);
            clone.OriginalExpr.Type = "Any";
            return clone;
        }

        public override string ToString() {
            if (Expr != null) {
                return $"Global Variable: {Identifier}, Definition: {Definition}, Parent {Parent?.Name}, Expr: {Expr}";

            } else {
                return $"Global Variable: {Identifier}, Definition: {Definition}, Parent {Parent?.Name} Expr: null";
            }
        }
    }
}
