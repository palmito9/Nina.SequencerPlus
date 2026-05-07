using NINA.Astrometry;
using NINA.Astrometry.Interfaces;
using NINA.Core.Utility;
using NINA.Equipment.Equipment.MyTelescope;
using NINA.Equipment.Interfaces.Mediator;
using NINA.Equipment.Interfaces.ViewModel;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Conditions;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Trigger;
using NINA.WPF.Base.Mediator;
using NINA.WPF.Base.ViewModel;
using Nito.Mvvm;
using Serilog.Debugging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace NINA.Plugin.SequencerPlus {
    /// <summary>
    /// This Class shows the basic principle on how to add a new panel to N.I.N.A. Imaging tab via the plugin interface
    /// In this example an altitude chart is added to the imaging tab that shows the altitude chart based on the position of the telescope    
    /// </summary>
    [Export(typeof(IDockableVM))]
    public class SequencerPlusPluginDockable : DockableVM {

        [ImportingConstructor]
        public SequencerPlusPluginDockable(IProfileService profileService) : base(profileService) {
            Title = "Sequencer+ Panel";

            ExpressionString = Symbol.SequencerPlusPluginObject.DockableExprs;
            BuildExprList();
        }

        private static bool InhibitSave { get; set; } = false;

        public DockableExpr Exp {  get; set; }

        private void BuildExprList() {
            InhibitSave = true;
            if (Exp == null) {
                Exp = new DockableExpr("foo");
                Exp.Evaluate();
            }
            try {
                string[] l = ExpressionString.Split(EXPR_DIVIDER);
                foreach (string s in l) {
                    if (s.Length > 0) {
                        string[] parts = s.Split(EXPR_INTERNAL_DIVIDER);
                        DockableExpr expr = new DockableExpr(parts[0]);

                        // Update radio buttons!
                        if (parts.Length > 1) {
                            expr.DisplayType = parts[1];
                        }
                        if (parts.Length > 2) {
                            expr.ConversionType = parts[2];
                        }
                        ExpressionList.Add(expr);
                    }
                }
            } finally {
                InhibitSave = false;
            }
        }

        public static Task UpdateData() {
            ISequenceItem runningItem = null;

            if (ExpressionList.Count > 0) {
                runningItem = SequencerPlusPlugin.GetRunningItem();
            }
            foreach (DockableExpr e in ExpressionList) {
                ISequenceEntity se = e.SequenceEntity;
                if (runningItem != null) {
                    e.SequenceEntity = runningItem;
                }

                e.Refresh();
                if (runningItem != null) {
                    e.SequenceEntity = se;
                }
            }
            return Task.CompletedTask;
        }

        private const char EXPR_DIVIDER = (char)0x0;
        private const char EXPR_INTERNAL_DIVIDER = (char)0x1;

        public static void SaveDockableExprs() {
            if (InhibitSave) return;
            int count = 0;
            StringBuilder sb = new StringBuilder();
            foreach (DockableExpr e in ExpressionList) {
                sb.Append(e.Expression);
                sb.Append(EXPR_INTERNAL_DIVIDER);
                sb.Append(e.DisplayType);
                sb.Append(EXPR_INTERNAL_DIVIDER);
                sb.Append(e.ConversionType);
                sb.Append(EXPR_DIVIDER);
                count++;
            }
            Logger.Info("SaveDockableExprs saving " + count + " Exprs");
            Symbol.SequencerPlusPluginObject.DockableExprs = sb.ToString();
        }

        public string ExpressionString { get; private set; }

        public static ObservableCollection<DockableExpr> ExpressionList { get; private set; } = new ObservableCollection<DockableExpr>();
        
        public static void RemoveExpr (DockableExpr e) {
            ExpressionList.Remove(e);
            SaveDockableExprs();
        }

        private GalaSoft.MvvmLight.Command.RelayCommand addInstruction;

        public ICommand AddInstruction => addInstruction ??= new GalaSoft.MvvmLight.Command.RelayCommand(PerformAddInstruction);

        private void PerformAddInstruction() {
            ExpressionList.Add(new DockableExpr("New"));
            SaveDockableExprs();
        }
    }
}
