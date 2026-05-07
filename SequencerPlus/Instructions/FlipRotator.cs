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
using NINA.Sequencer.Validations;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Locale;
using NINA.Sequencer.SequenceItem;
using NINA.Equipment.Equipment.MyRotator;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "FlipRotator")]
    [ExportMetadata("Description", "Lbl_SequenceItem_Rotator_MoveRotatorMechanical_Description")]
    [ExportMetadata("Icon", "RotatorSVG")]
    [ExportMetadata("Category", "Sequencer+ (Misc)")]
    [Export(typeof(ISequenceItem))]
    [JsonObject(MemberSerialization.OptIn)]
    public class FlipRotator : SequenceItem, IValidatable {

        private IRotatorMediator rotatorMediator;

        [ImportingConstructor]
        public FlipRotator(IRotatorMediator RotatorMediator) {
            this.rotatorMediator = RotatorMediator;
        }

        private FlipRotator(FlipRotator cloneMe) : this(cloneMe.rotatorMediator) {
            CopyMetaData(cloneMe);
        }

        public override object Clone() {
            return new FlipRotator(this) {
            };
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public string MechanicalPosition { get; set; } = "Unknown";

        public override Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            RotatorInfo info = rotatorMediator.GetInfo();

            if (info.Connected) {
                float pos = info.MechanicalPosition;
                pos = pos > 180 ? pos - 180 : pos + 180;
                if (pos == 360) pos = 0;
                return rotatorMediator.MoveMechanical(pos, token);
            } else {
                throw new SequenceEntityFailedException();
            }
        }

        public bool Validate() {
            var i = new List<string>();
            if (!rotatorMediator.GetInfo().Connected) {
                i.Add(Loc.Instance["LblRotatorNotConnected"]);
                MechanicalPosition = "Unknown";
            } else {
                MechanicalPosition = rotatorMediator.GetInfo().MechanicalPosition.ToString();
            }
            RaisePropertyChanged("MechanicalPosition");
            Issues = i;
            return i.Count == 0;
        }

        public override void AfterParentChanged() {
            Validate();
        }

        public override string ToString() {
            return $"Category: {Category}, Item: {nameof(FlipRotator)}, Mechanical Position: {MechanicalPosition}";
        }
    }
}