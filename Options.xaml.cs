using NINA.Core.Utility;
using NINA.Profile;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {

    [Export(typeof(ResourceDictionary))]
    partial class Options : ResourceDictionary {

        public Options() {
            InitializeComponent();
        }

        public void ShowConstants(object sender, ToolTipEventArgs e) {
            Symbol.ShowSymbols(sender);
        }

    }
}