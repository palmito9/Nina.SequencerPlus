using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using NCalc;
using NINA.Core.Utility.Notification;
using NINA.Core.Enum;
using System.Linq;
using System.Text;
using Accord.IO;
using Namotion.Reflection;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Equipment.MySwitch;
using NINA.Equipment.Interfaces;
using NINA.Sequencer.SequenceItem.Utility;
using System.Windows;
using NINA.Equipment.Equipment.MyWeatherData;
using System.Windows.Controls;
using System.Diagnostics;
using NINA.Core.Utility;
using NINA.Core.Locale;
using NINA.WPF.Base.Mediator;
using System.Runtime.CompilerServices;
using NINA.Sequencer.Conditions;
using NINA.Astrometry;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Once Safe")]
    [ExportMetadata("Description", "Waits for Safe condition, then executes the specified instructions.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [ExportMetadata("Category", "Sequencer+ (Safety)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    
    public class OnceSafe : IfCommand, IValidatable, IDSOTargetProxy {
        private ISafetyMonitorMediator safetyMonitorMediator;

        [ImportingConstructor]
        public OnceSafe(ISafetyMonitorMediator safetyMediator) {
            Instructions = new IfContainer();
            Instructions.Add(new LoopCondition() { Iterations = 1 });
            Instructions.Add(new SafetyMonitorCondition(safetyMediator) { });
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            this.safetyMonitorMediator = safetyMediator;
        }

        public OnceSafe(OnceSafe copyMe) : this(copyMe.safetyMonitorMediator) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                foreach (ISequenceCondition instruction in copyMe.Instructions.Conditions) {
                    Instructions.Add((ISequenceCondition)instruction.Clone());
                }
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
            }
        }

        public override object Clone() {
            return new OnceSafe(this) {
            };
        }

        private bool isSafe;

        public bool IsSafe {
            get => isSafe;
            private set {
                isSafe = value;
                RaisePropertyChanged();
            }
        }

        public TimeSpan WaitInterval { get; set; } = TimeSpan.FromSeconds(5);

        private void UpdateChildren(ISequenceContainer c) {
            foreach (var item in c.Items) {
                c.AfterParentChanged();
            }
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            bool IsSafe = WhenUnsafe.CheckSafe(this, safetyMonitorMediator);
            while (!IsSafe && !(Parent == null)) {
                progress?.Report(new ApplicationStatus() { Status = Loc.Instance["Lbl_SequenceItem_SafetyMonitor_WaitUntilSafe_Waiting"] });
                await CoreUtil.Wait(WaitInterval, token, default);
                IsSafe = WhenUnsafe.CheckSafe(this, safetyMonitorMediator);
            }

            // Need RunningItem from WhenUnsafe (When)
            ISequenceItem runningItem = WhenUnsafe.RunningItem;
            if (runningItem != null) {
                Target = DSOTarget.FindTarget(runningItem.Parent);
            } else {
                Target = DSOTarget.FindTarget(Parent);
            }

            if (Target != null) {
                Logger.Info("Found Target: " + Target);
                UpdateChildren(Instructions);
            }

            // Execute instructions now
            await Instructions.Run(progress, token);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(OnceSafe)}";
        }

        public new bool Validate() {
            CommonValidate();

            var i = new List<string>();

            Issues = i;
            return i.Count == 0;
        }
        public InputTarget DSOProxyTarget() {
            return Target;
        }

        public InputTarget Target = null;

        public InputTarget FindTarget(ISequenceContainer c) {
            while (c != null) {
                if (c is IDSOTargetProxy dso) {
                    return dso.DSOProxyTarget();
                }
                c = c.Parent;
            }
            return null;
        }
    }
}
