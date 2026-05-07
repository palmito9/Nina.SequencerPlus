using Newtonsoft.Json;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.SequenceItem;
using NINA.WPF.Base.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Plugin Template Instruction")]
    //[ExportMetadata("Description", "This item will just show a notification and is just there to show how the plugin system works")]
    //[ExportMetadata("Icon", "Plugin_Test_SVG")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class InterruptWait : SequenceItem {
 
        [ImportingConstructor]
        public InterruptWait() {
        }

        public InterruptWait(InterruptWait copyMe) : this() {
            CopyMetaData(copyMe);
        }

        [JsonProperty]
        public string Text { get; set; }

        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            progress.Report(new ApplicationStatus() { Status = "Interrupted" });

            Logger.Info("Interrupt; waiting for continue signal");
            TimeSpan maxWait = TimeSpan.FromSeconds(200);
            TimeSpan waitTime = TimeSpan.Zero;

            do {
                if (Parent == null) {
                    Logger.Info("Interrupt wait terminated; instruction deleted");
                    break;
                }

                progress.Report(new ApplicationStatus() { Status = "Interrupt in progress for " + waitTime.ToString(@"mm\:ss") });

                await CoreUtil.Delay(1000, token);
                
                waitTime += TimeSpan.FromSeconds(1);
            } while (waitTime < maxWait);
            progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblResumeTracking"] });

            Logger.Info("Continuing to interrupt instruction list");
        }

        public override object Clone() {
            return new InterruptWait(this);
        }
        

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(InterruptWait)}, Text: {Text}";
        }
    }
}