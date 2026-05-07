using NINA.Sequencer;
using System;
using System.Windows;
using System.Windows.Controls;

namespace NINA.Plugin.SequencerPlus {

    public partial class SymControl : UserControl {
        public SymControl() {
            InitializeComponent();
        }

        public static readonly DependencyProperty ExpProperty =
             DependencyProperty.Register("Exp", typeof(Expr), typeof(SymControl), null);

        public Expr Exp { get; set; }

        public void ShowConstants(object sender, ToolTipEventArgs e) {
            Symbol.ShowSymbols(sender);
        }

        public void IfConstant_PredicateToolTip(object sender, ToolTipEventArgs e) {
        //    TextBox predicateText = (TextBox)sender;
        //    IfConstant ifConstant = (IfConstant)(predicateText.DataContext);
        //    predicateText.ToolTip = ifConstant.ShowCurrentInfo();
        //
        //
        }


    }
}

