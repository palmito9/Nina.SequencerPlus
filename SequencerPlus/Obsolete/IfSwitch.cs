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
using NINA.Equipment.Interfaces.Mediator;
using NINA.Core.Utility;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "If Switch/Weather (Deprecated; use If)")]
    [ExportMetadata("Description", "Executes an instruction set if the expression, based on current switch and/or weather values, is true")]
    [ExportMetadata("Icon", "SwitchesSVG")]
    [ExportMetadata("Category", "Switch")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    
    public class IfSwitch : IfCommand, IValidatable {
        private ISwitchMediator switchMediator;
        private IWeatherDataMediator weatherMediator;

        [ImportingConstructor]
        public IfSwitch(ISwitchMediator switchMediator, IWeatherDataMediator weatherMediator) {
            Predicate = "";
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            this.switchMediator = switchMediator;
            this.weatherMediator = weatherMediator;
        }

        public IfSwitch(IfSwitch copyMe) : this(copyMe.switchMediator, copyMe.weatherMediator) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Predicate = copyMe.Predicate;
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
            }
        }

        public override object Clone() {
            return new IfSwitch(this) {
            };
        }

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            throw new SequenceEntityFailedException("IfSwitch has been replaced by If");
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfSwitch)}";
        }

        [JsonProperty]
        public string Predicate {  get; set; }

        public new bool Validate() {

            var i = new List<string>();
            i.Add("This instruction is obsolete; please use the If instruction instead");
            Issues = i;
            return false;
        }

        public bool Check() {
            return false;
        }
     }
}
