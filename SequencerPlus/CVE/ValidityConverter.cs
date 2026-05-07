#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using NINA.Sequencer;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Markup;
using System.Windows.Media;
using static System.Net.Mime.MediaTypeNames;

namespace NINA.Plugin.SequencerPlus {

    public class ValidityConverter : IMultiValueConverter {

        public object Convert(object[] value, Type targetType, object parameter, CultureInfo culture) {
            ISequenceEntity item = value[1] as ISequenceEntity;
            if (item != null && ExpressionConverter.ValidityCache.ContainsKey(item)) {
                return new SolidColorBrush(Colors.GreenYellow);
            }
            return new SolidColorBrush(Colors.Orange);
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}