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
using NINA.Sequencer.SequenceItem;
using System;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "Fail (for testing)")]
    [ExportMetadata("Description", "This instruction always fails; for experimenting with 'If Fails'!")]
    [ExportMetadata("Icon", "HourglassSVG")]
    //[ExportMetadata("Category", "Sequencer+ (Test)")]
    //[Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class Fail : SequenceItem {

        [ImportingConstructor]
        public Fail() {
        }

        private Fail(Fail cloneMe) : base(cloneMe) {
        }

        public override object Clone() {
            return new Fail(this) {
            };
        }


        public async override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            await NINA.Core.Utility.CoreUtil.Wait(TimeSpan.FromSeconds(0.5 ), true, token, progress, "");
            throw new SequenceEntityFailedException("Fail exception did its thing!");
        }

        public override TimeSpan GetEstimatedDuration() {
            return TimeSpan.FromSeconds(5);
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(WaitIndefinitely)}, Time: 12 hours";
        }
    }
}