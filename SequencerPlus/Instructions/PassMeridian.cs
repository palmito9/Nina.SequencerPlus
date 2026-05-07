using Newtonsoft.Json;
using NINA.Astrometry;
using NINA.Core.Locale;
using NINA.Core.Model;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Profile.Interfaces;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace NINA.Plugin.SequencerPlus {
    [ExportMetadata("Name", "Wait to Pass Meridian")]
    [ExportMetadata("Description", "Waits until the scope has passed the meridian by the amount specified as 'Minutes after Meridian' in the Meridian Flip Settings (in Options -> Imaging).")]
    [ExportMetadata("Icon", "MeridianFlipSVG")]
    [ExportMetadata("Category", "Sequencer+ (Meridian Flip)")]
    [Export(typeof(ISequenceItem))]

    [JsonObject(MemberSerialization.OptIn)]

    public class PassMeridian : DIYMFOnly, IValidatable {

        private ITelescopeMediator telescopeMediator;
        private IProfileService profileService;

        [ImportingConstructor]
        public PassMeridian(ITelescopeMediator telescopeMediator, IProfileService profileService) {
            this.telescopeMediator = telescopeMediator;
            this.profileService = profileService;
            Name = Name;
            Icon = Icon;
            Waiting = false;
            FlipStatus = "Waiting...";
        }

        public PassMeridian(PassMeridian copyMe) : this(copyMe.telescopeMediator, copyMe.profileService) {
            if (copyMe != null) {
                CopyMetaData(copyMe);
                Name = copyMe.Name;
                Icon = copyMe.Icon;
                Waiting = false;
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

        private bool Waiting { get; set; }

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
    

        public virtual double PauseTimeBeforeMeridian {
            get {
                return FindTrigger()?.PauseTimeBeforeMeridian ?? 0;
            }
            set { }
        }

        public virtual double MinutesAfterMeridian {
            get {
                return FindTrigger()?.MinutesAfterMeridian ?? 0;
            }
            set { }
        }

        public virtual double MaxMinutesAfterMeridian {
            get {
                return FindTrigger()?.MaxMinutesAfterMeridian ?? 0;
            }
            set { }
        }

        public virtual double TimeToMeridianFlip {
            get {
                DIYMeridianFlipTrigger diymf = FindTrigger();
                if (diymf != null) {
                    return diymf.TimeToMeridianFlip;
                }
                Logger.Error("Can't find DIYMF");
                return 0;
            }
            set { }
        
        }
        protected virtual TimeSpan CalculateMaximumTimeRemainaing() {
            return TimeSpan.FromHours(TimeToMeridianFlip);
        }

        protected virtual TimeSpan CalculateMinimumTimeRemaining() {
            //Substract delta from maximum to get minimum time
            var delta = MaxMinutesAfterMeridian - MinutesAfterMeridian;
            var time = CalculateMaximumTimeRemainaing() - TimeSpan.FromMinutes(delta);
            if (time < TimeSpan.Zero) {
                time = TimeSpan.Zero;
            }
            return time;
        }
        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Logger.Trace("Meridian Flip - Passing meridian");

            TimeSpan remainingTime = CalculateMinimumTimeRemaining();

            progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblStopTracking"] });

            Logger.Info("Meridian Flip - Stopping tracking to pass meridian; time remaining = " + remainingTime);
            Waiting = true;
            telescopeMediator.SetTrackingEnabled(false);
            do {
                progress.Report(new ApplicationStatus() { Status = remainingTime.ToString(@"hh\:mm\:ss") });

                var delta = await CoreUtil.Delay(1000, token);

                remainingTime -= delta;
                if (MinutesAfterMeridian > 0) {
                    FlipStatus = $"Flip expected at {TimeString(remainingTime)}, {MinutesAfterMeridian} minutes after transit";
                }
                else {
                    FlipStatus = $"Flip expected at {TimeString(remainingTime)}";
                }
                RaisePropertyChanged("FlipStatus");
            } while (remainingTime.TotalSeconds >= 1);
            progress.Report(new ApplicationStatus() { Status = Loc.Instance["LblResumeTracking"] });

            Waiting = false;
            Logger.Info("Meridian Flip - Resuming tracking after passing meridian");
            telescopeMediator.SetTrackingEnabled(true);
         }


        public override object Clone() {
            return new PassMeridian(this) {
            };
        }

        public override string ToString() {
            return $"Category: {Category}, Item: PassMeridian+";
        }
        private string TimeString(TimeSpan min) {
            return (DateTime.Now + min).ToString("T", CultureInfo.CurrentCulture);
            
        }

        public override bool Validate() {
            var i = new List<string>();

            if (!(IsInsideMeridianFlipEvent())) {
                return false;
            }

            var telescopeInfo = telescopeMediator.GetInfo();
            if (Status == NINA.Core.Enum.SequenceEntityStatus.FINISHED) {
                FlipStatus = "Completed";
            } else if (!telescopeInfo.Connected) {
                i.Add(Loc.Instance["LblTelescopeNotConnected"]);
                FlipStatus = "";
            } else {
                Coordinates target;
                var t = ItemUtility.RetrieveContextCoordinates(Parent);
                if (t != null) {
                    target = t.Coordinates;
                    if (target.RADegrees == 0 && target.Dec == 0) {
                        target = telescopeInfo.Coordinates;
                    }
                } else {
                    target = telescopeInfo.Coordinates;
                }

                if (!Waiting && target != null) {
                    TimeSpan ttm = NINA.Astrometry.MeridianFlip.TimeToMeridian(target, Angle.ByHours(AstroUtil.GetLocalSiderealTimeNow(profileService.ActiveProfile.AstrometrySettings.Longitude)));
                    TimeSpan plusMinutes = TimeSpan.FromMinutes(Math.Max(MinutesAfterMeridian, MaxMinutesAfterMeridian));
                    if (plusMinutes == TimeSpan.Zero) {
                        FlipStatus = $"Expected at {TimeString(ttm)}.";
                    } else if (MinutesAfterMeridian == MaxMinutesAfterMeridian) {
                        FlipStatus = $"Expected at {TimeString(ttm + TimeSpan.FromMinutes(MinutesAfterMeridian))}, {plusMinutes.Minutes} minutes after transit.";
                    } else {
                        FlipStatus = $"Expected {TimeString(ttm + TimeSpan.FromMinutes(MinutesAfterMeridian))} - {TimeString(ttm + plusMinutes)}, up to {plusMinutes.Minutes} minutes after transit.";
                    }
                }
            }
            RaisePropertyChanged("FlipStatus");

            Issues = i;
            return i.Count == 0;
        }
    }
}
