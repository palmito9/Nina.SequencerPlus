using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.ComponentModel.Composition;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Conditional Trigger")]
    [ExportMetadata("Description", "The specified trigger will only be active when the Expression is true.")]
    [ExportMetadata("Icon", "WandSVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceTrigger))]
    [JsonObject(MemberSerialization.OptIn)]
    public class ConditionalTrigger : SequenceTrigger, IValidatable, ITrueFalse {


        [ImportingConstructor]
        public ConditionalTrigger() {
            DropIntoDIYTriggersCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropInSequenceTrigger);
            IfExpr = new Expr(this);
        }

        public override bool AllowMultiplePerSet => true;

        public ICommand DropIntoDIYTriggersCommand { get; set; }

        public ConditionalTrigger(ConditionalTrigger copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                TriggerRunner = (SequentialContainer)copyMe.TriggerRunner.Clone();
                IfExpr = new Expr(this, copyMe.IfExpr.Expression);
            }
        }

        public override object Clone() {
            return new ConditionalTrigger(this) {
            };
        }

        private static object lockObj = new object();
        public bool InFlight { get; set; }

        private Expr _IfExpr;

        [JsonProperty]
        public Expr IfExpr {
            get => _IfExpr;
            set {
                _IfExpr = value;
                RaisePropertyChanged();
            }
        }
        private void DropInSequenceTrigger(DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceTrigger item;
                var source = parameters.Source as ISequenceTrigger;

                if (source.Parent != null && !parameters.Duplicate) {
                    item = source;
                } else {
                    item = (ISequenceTrigger)source.Clone();
                }

                if (item is ConditionalTrigger) return;

                if (item.Parent != TriggerRunner) {
                    item.Parent?.Remove(item);
                    item.AttachNewParent(TriggerRunner);
                }

                TriggerRunner.Triggers.Clear();
                TriggerRunner.Triggers.Add(item);
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
            InFlight = true;
            TriggerRunner.AttachNewParent(context);

            try {
                if (TriggerRunner.Triggers.Count > 0) {
                    ISequenceTrigger t = TriggerRunner.Triggers[0];
                    await t.Run(context, progress, token);
                }
            } finally {
                InFlight = false;
                TriggerRunner.Parent?.Remove(TriggerRunner);
                TriggerRunner.AttachNewParent(Parent);
            }
        }

        private bool SkipTrigger() {
            Symbol.UpdateSwitchWeatherData();
            IfExpr.Evaluate();
            return string.Equals(IfExpr.ValueString, "0", StringComparison.OrdinalIgnoreCase) && (IfExpr.Error == null);
        }

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight || SkipTrigger()) return false;
            if (TriggerRunner.Triggers.FirstOrDefault() == null) return false;
            return TriggerRunner.Triggers.FirstOrDefault().ShouldTrigger(previousItem, nextItem);
        }

        public override bool ShouldTriggerAfter(ISequenceItem previousItem, ISequenceItem nextItem) {
            if (InFlight || SkipTrigger()) return false;
            if (TriggerRunner.Triggers.FirstOrDefault() == null) return false;
            return TriggerRunner.Triggers.FirstOrDefault().ShouldTriggerAfter(previousItem, nextItem);
        }

        // Per Nick Holland
        public override void SequenceBlockInitialize() {
            if (!InFlight && TriggerRunner.Triggers.FirstOrDefault() != null)
                TriggerRunner.Triggers.FirstOrDefault().SequenceBlockInitialize();
        }

        public override void AfterParentChanged() {
            base.AfterParentChanged();
            foreach (ISequenceTrigger item in TriggerRunner.Triggers) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            foreach (ISequenceItem item in TriggerRunner.Items) {
                if (item.Parent == null) item.AttachNewParent(TriggerRunner);
            }
            TriggerRunner.AttachNewParent(Parent);
            IfExpr.Validate();
        }
        public virtual bool Validate() {
            // Validate the Items (this will update their status)
            if (TriggerRunner == null) return true;
            foreach (ISequenceTrigger item in TriggerRunner.Triggers) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }
            foreach (ISequenceItem item in TriggerRunner.Items) {
                if (item is IValidatable vitem) {
                    _ = vitem.Validate();
                }
            }
            IfExpr.Validate();

            IList<string> i = new List<string>();
            Expr.AddExprIssues(i, IfExpr);

            if (TriggerRunner.Triggers.Count == 0) {
                i.Add("There must be a Trigger specified for this instruction");
            }
            Issues = i;
            return i.Count == 0;
        }
        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(DIYTrigger)}";
        }
    }
}