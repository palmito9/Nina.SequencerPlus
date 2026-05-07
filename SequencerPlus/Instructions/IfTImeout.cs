using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.DragDrop;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "If Timed Out")]
    [ExportMetadata("Description", "Executes an instruction set if the predicate instruction failed.")]
    [ExportMetadata("Icon", "IfSVG")]
    [ExportMetadata("Category", "Sequencer+ (Expressions)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class IfTimeout : IfCommand, IValidatable {

        [ImportingConstructor]
        public IfTimeout() {
            Condition = new IfContainer();
            Instructions = new IfContainer();
            DropIntoIfCommand = new GalaSoft.MvvmLight.Command.RelayCommand<DropIntoParameters>(DropIntoCondition);
        }
        public IfTimeout(IfTimeout copyMe) : this() {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Condition = (IfContainer)copyMe.Condition.Clone();
                Instructions = (IfContainer)copyMe.Instructions.Clone();
                Condition.AttachNewParent(Instructions.Parent);
                Time = copyMe.Time;
            }
        }

        public override object Clone() {
            return new IfTimeout(this) {
            };
        }

        private int iTime = 10;
        [JsonProperty]
        public int Time {
            get => iTime;
            set {
                iTime = value;
                RaisePropertyChanged("Time");
            }
        }

        public ICommand DropIntoIfCommand { get; set; }

        private ConditionWatchdog Watchdog { get; set; }

        public DateTime StartTime {  get; set; }

        public volatile bool TimedOut = false;

        private CancellationTokenSource cts;
        private CancellationTokenSource linkedCts;

        private IProgress<ApplicationStatus> progress;


        private Task CheckTimer() {
            TimeSpan elapsed = DateTime.Now - StartTime;
            if (TimedOut) return Task.CompletedTask;
            if (elapsed > TimeSpan.FromSeconds(Time)) {
                TimedOut = true;
                Logger.Info("Timeout period over; interrupting...");
                cts.Cancel();
                linkedCts.Cancel();
                Notification.ShowWarning("Timed out!");
            }
            if (progress != null) {
                string progressStatus;

                TimeSpan t = TimeSpan.FromSeconds(Time);
                string status = "Timeout in ~";

                if (t.Hours > 0) {
                    var remaining = t - elapsed;
                    progressStatus = $"{status} {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2}";
                } else if (t.Minutes > 0) {
                    var remaining = t - elapsed;
                    progressStatus = $"{status} {remaining.Minutes:D2}:{remaining.Seconds:D2}";
                } else {
                    var remaining = t - elapsed;
                    progressStatus = $"{status} {remaining.Seconds} s";
                }

                progress?.Report(
                    new ApplicationStatus {
                        MaxProgress = 1,
                        Progress = -1,
                        Status = progressStatus,
                        ProgressType = ApplicationStatus.StatusProgressType.Percent
                    }
                ) ;  ;
            }
            return Task.CompletedTask;
        }

        public override async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            ISequenceItem condition = Condition.Items[0];

            if (condition == null) {
                Status = NINA.Core.Enum.SequenceEntityStatus.FAILED;
                return;
            }

            this.progress = new Progress<ApplicationStatus>((p) => {
                p.Source = Name;
                progress?.Report(p);
            });

            ConditionWatchdog watch = new ConditionWatchdog(CheckTimer, TimeSpan.FromSeconds(5));
            _ = watch.Start();

            cts = new CancellationTokenSource();
            linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, token);

            try {
                StartTime = DateTime.Now;
                TimedOut = false;
                // Execute the conditional
                condition.Status = NINA.Core.Enum.SequenceEntityStatus.CREATED;
                await condition.Run(progress, linkedCts.Token);
            } catch (Exception ex) {
                watch.Cancel();
                if (TimedOut) {
                    Logger.Info("Timed out; executing instructions...");
                    await Instructions.Run(progress, token);
                } else {
                    Logger.Info("Exception: " + ex.Message);
                }
            } finally {
                this.progress.Report(new ApplicationStatus() { ProgressType = ApplicationStatus.StatusProgressType.ValueOfMaxValue, Status = "" });
                Logger.Info("Execution terminated");
                watch.Cancel();
                cts.Dispose();
                linkedCts.Dispose();
            }
        }

        // Allow only ONE instruction to be added to Condition
        public void DropIntoCondition(DropIntoParameters parameters) {
            lock (lockObj) {
                ISequenceEntity item;
                if (parameters.Source is TemplatedSequenceContainer tsc) {
                    item = (ISequenceEntity)tsc.Clone();
                } else {
                    ISequenceContainer container = (ISequenceContainer)(object)((parameters.Source is ISequenceContainer) ? parameters.Source : null);
                    if (container != null) {
                        item = (ISequenceEntity)((ICloneable)container).Clone();
                    } else {
                        item = (ISequenceEntity)(object)((parameters.Source is ISequenceEntity) ? parameters.Source : null);
                    }
                }

                ISequenceItem si = item as ISequenceItem;
                if (si != null) {

                    if (si.Parent != Condition) {
                        si.Parent?.Remove(si);
                        si.AttachNewParent(Condition);
                    }

                    Condition.Items.Clear();
                    Condition.Items.Add(si);
                }
            }
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(IfTimeout)}";
        }
    }
}
