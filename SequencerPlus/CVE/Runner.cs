using NINA.Core.Model;
using NINA.Sequencer.Container;
using NINA.Sequencer.Container.ExecutionStrategy;
using NINA.Sequencer.SequenceItem;
using NINA.Core.Enum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Utility;
using System.Diagnostics;
using NINA.Core.Utility.Notification;

namespace NINA.Plugin.SequencerPlus {
    public class Runner: SequentialContainer {

        public Runner(SequentialContainer runner, IProgress<ApplicationStatus> progress, CancellationToken token) {
            if (runner.Parent == this) {
                Notification.ShowError("TBR RECURSION!");
                Logger.Error("TBR recursing");
                Logger.Error(new StackTrace(true).ToString());
            } else {
                AttachNewParent(runner.Parent);
            }
            RunInstructions = runner;
            Progress = progress;
            Token = token;
            ShouldRetry = false;
            //
        }

        public SequentialContainer RunInstructions { get; set; }

        public bool ShouldRetry { get; set; }

        public IProgress<ApplicationStatus> Progress { get; set; }      

        public CancellationToken Token { get; set; }

        public CancellationTokenSource cts { get; set; } = null;

        public async Task RunConditional () {
            ShouldRetry = false;
            RunInstructions.Status = SequenceEntityStatus.CREATED;
            Logger.Info("When runner: starting sequence.");
            await RunInstructions.Run(Progress, Token);
            Logger.Info("When runner: finishing sequence.");
        }

        public override void ResetProgress() {
            base.ResetProgress();
            foreach (ISequenceItem item in RunInstructions.Items) {
                item.ResetProgress();
            }
        }
    }
}
