#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Accord.Math.Comparers;
using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Core.Utility.Notification;
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Breakpoint")]
    [ExportMetadata("Description", "Wait indefinitely (until instruction is stopped or deleted)")]
    [ExportMetadata("Icon", "HourglassSVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class Break : SequenceItem {

        [ImportingConstructor]
        public Break() {
            Time = 60*60*12;  // 12 hours
        }

        private Break(Break cloneMe) : base(cloneMe) {
        }

        public override object Clone() {
            return new Break(this) {
                Time = Time,
                Reason = Reason
            };
        }

        private int time;

        [JsonProperty]
        public int Time {
            get => time;
            set {
                time = value;
                RaisePropertyChanged();
            }
        }
        private bool inFlight;

        [JsonProperty]
        public bool InFlight {
            get => inFlight;
            set {
                inFlight = value;
                RaisePropertyChanged();
                RaisePropertyChanged("NotInFlight");
            }
        }

        public bool NotInFlight {
            get => !InFlight;
            set { }
        }

        private CancellationTokenSource cts;

        public bool Notify { get; set; } = true;

        [JsonProperty]
        public string Reason { get; set; } = "";

        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            InFlight = true;
            cts = new CancellationTokenSource();
            CancellationTokenSource lcts = CancellationTokenSource.CreateLinkedTokenSource(new CancellationToken[] { cts.Token, token });

            try {
                if (Notify) {
                    if (Reason != null && Reason.Length > 0) {
                        Notification.ShowWarning("Breakpoint: " + Reason);
                    } else {
                        Notification.ShowWarning("Breakpoint hit!");
                    }
                }
                await NINA.Core.Utility.CoreUtil.Wait(GetEstimatedDuration(), true, lcts.Token, progress, ""); ;
            } finally {
                InFlight = false;
            }
        }

        public override TimeSpan GetEstimatedDuration() {
            return TimeSpan.FromSeconds(60);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(Break)}, Time: 12 hours";
        }
        
        private GalaSoft.MvvmLight.Command.RelayCommand stopInstructions;

        public ICommand StopInstructions => stopInstructions ??= new GalaSoft.MvvmLight.Command.RelayCommand(PerformStopInstructions);

        private void PerformStopInstructions() {
            if (InFlight) {
                cts.Cancel();
            }
        }

    }
}