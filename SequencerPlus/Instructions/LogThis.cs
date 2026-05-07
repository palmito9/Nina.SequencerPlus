using System;
using System.Threading.Tasks;

using System.Threading;
#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using Newtonsoft.Json;
using NINA.Core.Model;
using NINA.Sequencer.SequenceItem;
using System.ComponentModel.Composition;
using NINA.Core.Utility;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Log On Click")]
    [ExportMetadata("Description", "Add your own entry into NINA's log")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class LogThis : SequenceItem {

        [ImportingConstructor]
        public LogThis() {
        }

        private LogThis(LogThis cloneMe) : base(cloneMe) {
        }

        public override object Clone() {
            return new LogThis() { Name = this.Name, Icon = this.Icon };
        }

        public string LogText { get; set; }

        public void AddToLog() {
            Logger.Error("!!! User says: " + LogText);
            LogText = "";
            RaisePropertyChanged("LogText");
            
        }
        private GalaSoft.MvvmLight.Command.RelayCommand postInstructions;

        public ICommand SendInstruction => postInstructions ??= new GalaSoft.MvvmLight.Command.RelayCommand(AddToLog);


        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            return Task.CompletedTask;
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(LogThis)}";
        }
    }
}