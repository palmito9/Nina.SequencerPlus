using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Core.Utility.Notification;
using NINA.Equipment.Interfaces;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Flip Scope")]
    [ExportMetadata("Description", "Performs the actual meridian flip. Stops tracking, flips the scope by slewing, and restarts tracking.")]
    [ExportMetadata("Icon", "MeridianFlipSVG")]
    [ExportMetadata("Category", "Sequencer+ (Meridian Flip)")]
    [Export(typeof(ISequenceItem))]

    [JsonObject(MemberSerialization.OptIn)]

    public class DoFlip : DIYMFOnly, IValidatable {

        private ITelescopeMediator telescopeMediator;
        private IDomeMediator domeMediator;
        private IDomeFollower domeFollower;
 
        [ImportingConstructor]
        public DoFlip(ITelescopeMediator telescopeMediator, IDomeMediator domeMediator, IDomeFollower domeFollower) {
            this.telescopeMediator = telescopeMediator;
            this.domeMediator = domeMediator;
            this.domeFollower = domeFollower;
            Name = Name;
            Icon = Icon;
        }

        public DoFlip(DoFlip copyMe) : this(copyMe.telescopeMediator, copyMe.domeMediator, copyMe.domeFollower) {
            if (copyMe != null) {
                CopyMetaData(copyMe);           
                Name = copyMe.Name;
                Icon = copyMe.Icon;
                FlipStatus = copyMe.FlipStatus;
            }
        }


        [JsonProperty]
        public override String FlipStatus { get; set; }

        private IList<string> issues = new List<string>();

        public override IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblFlippingScope"] });
            Coordinates targetCoordinates = ItemUtility.RetrieveContextCoordinates(Parent).Coordinates; 
            Logger.Info($"Meridian Flip - Scope will flip to coordinates RA: {targetCoordinates.RAString} Dec: {targetCoordinates.DecString} Epoch: {targetCoordinates.Epoch}");
            var flipsuccess = await telescopeMediator.MeridianFlip(targetCoordinates, token);
            Logger.Trace($"Meridian Flip - Successful flip: {flipsuccess}");

            var domeInfo = domeMediator.GetInfo();
            if (domeInfo.Connected && domeInfo.CanSetAzimuth && !domeFollower.IsFollowing) {
                progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblSynchronizingDome"] });
                Logger.Info($"Meridian Flip - Synchronize dome to scope since dome following is not enabled");
                if (!await domeFollower.TriggerTelescopeSync()) {
                    Notification.ShowWarning(Loc.Instance["LblDomeSyncFailureDuringMeridianFlip"]);
                    Logger.Warning("Meridian Flip - Synchronize dome operation didn't complete successfully. Moving on");
                }
            }
        }

        public override object Clone() {
            return new DoFlip(this) {
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: DoFlip+";
        }

        private DIYMeridianFlipTrigger FindTrigger() {
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is SequenceContainer c) {
                    // Found the DSO container; look at triggers
                    foreach (SequenceTrigger t in c.Triggers) {
                        if (t is DIYMeridianFlipTrigger diyTrigger) {
                            return diyTrigger;
                        }
                    }
                }
                p = p.Parent;
            }
            return null;
        }

        public override bool Validate() {
            var i = new List<string>();

            if (!IsInsideMeridianFlipEvent()) {
                return false;
            }

            if (Status == NINA.Core.Enum.SequenceEntityStatus.FINISHED) {
                FlipStatus = "Completed";
                RaisePropertyChanged("FlipStatus");
                return true;
            }

            var telescopeInfo = telescopeMediator.GetInfo();
            if (!telescopeInfo.Connected) {
                i.Add(Loc.Instance["LblTelescopeNotConnected"]);
                FlipStatus = "Telescope not connected";
                RaisePropertyChanged("FlipStatus");
            } else {
                Coordinates target = ItemUtility.RetrieveContextCoordinates(Parent)?.Coordinates;
                if (target != null) {
                    //Logger.Info("**Got target from Parent: " + target);
                    if (target.RADegrees == 0 && target.Dec == 0) {
                        //Logger.Info("**Target is at 0/0; using telescope");
                        target = telescopeMediator.GetInfo().Coordinates;
                        //Logger.Info("** target from telescope: " + target);
                    }
                } else {
                    target = telescopeMediator.GetInfo().Coordinates;
                    //Logger.Info("**Got target from telescope: " + target);
                }


                if (target != null) {
                    FlipStatus = $"Scope will flip to coordinates RA: {target.RAString} Dec: {target.DecString} Epoch: {target.Epoch}";
                } else {
                    FlipStatus = "Cannot retrieve scope coordinates";
                }
                RaisePropertyChanged("FlipStatus");
            }
  
            Issues = i;
            return i.Count == 0;
        }
    }
}

