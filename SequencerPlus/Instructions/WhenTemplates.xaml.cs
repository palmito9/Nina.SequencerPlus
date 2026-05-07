using NINA.Sequencer;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace NINA.Plugin.SequencerPlus {

    [Export(typeof(ResourceDictionary))]
    public partial class WhenTemplates : ResourceDictionary {

        public WhenTemplates() {
            InitializeComponent();
        }
  
        public void WhenSwitch_StopStartToolTip(object sender, ToolTipEventArgs e) {
            TextBlock startStopText = (TextBlock)sender;
            WhenUnsafe whenUnsafe = (WhenUnsafe)(startStopText.DataContext);
            if (whenUnsafe.Stopped && whenUnsafe.InFlight) {
                startStopText.ToolTip = "When Becomes Unsafe ended with an 'End Sequence' instruction, disabling the trigger. This button re-enables that trigger.";
            } else if (whenUnsafe.Stopped) {
                startStopText.ToolTip = "Allows the When Becomes Unsafe instructions that you previously paused to continue.";
            } else {
                startStopText.ToolTip = "Pauses the execution of the When Becomes Unsafe instructions.";
            }

        }

        public void ShowConstants(object sender, ToolTipEventArgs e) {
            Symbol.ShowSymbols(sender);
        }
    }
}