using Accord.Math;
using AvalonDock.Controls;
using NINA.Core.Utility;
using NINA.Core.Utility.Extensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ToastNotifications.Utilities;


namespace NINA.Plugin.SequencerPlus {
    /// <summary>
    /// Interaction logic for DockableTemplates.xaml
    ///
    /// </summary>
    /// 
    [Export(typeof(ResourceDictionary))]
    public partial class DockableTemplates : ResourceDictionary {
        public DockableTemplates() {
            InitializeComponent();
        }

        public void OpenTooltip(object sender, ToolTipEventArgs e) {
            ((DockableExpr)((TextBlock)sender).DataContext).IsOpen = true;
            e.Handled = true;
        }

        public void CheckDisplay(object sender, RoutedEventArgs e) {
            DockableExpr expr = (DockableExpr)((RadioButton)sender).DataContext;
            String displayType = (string)((RadioButton)sender).Content;
            expr.DisplayType = displayType;
            Logger.Info("Checked display box: " + displayType);
        }
        public void CheckConversion(object sender, RoutedEventArgs e) {
            DockableExpr expr = (DockableExpr)((RadioButton)sender).DataContext;
            String conversionType = (string)((RadioButton)sender).Content;
            expr.ConversionType = conversionType;
            Logger.Info("Checked conversion box: " + conversionType);
        }

        public void DeleteExpr(object sender, RoutedEventArgs e) {
            DockableExpr expr = (DockableExpr)((Button)sender).DataContext;
            SequencerPlusPluginDockable.RemoveExpr(expr);
        }

        public void EditExpr(object sender, RoutedEventArgs e) {
            Grid fe = ((FrameworkElement)sender).Parent as Grid;
            Expr expr = fe.DataContext as Expr;
            if (expr == null) return;

            Popup popup = fe.FindName("popup") as Popup;
            TextBox v = popup.FindName("newvalue") as TextBox;
            if (v != null) {
                v.Text = expr.Value.ToString();
            }
            popup.IsOpen = true;
        }

        public void PopupMouseLeave(object sender, MouseEventArgs e) {
            ((Popup)sender).IsOpen = false;
        }

        public void PopupKeyDown(object sender, KeyEventArgs e) {
            if (e.Key == Key.Enter) {
                StackPanel sp = ((FrameworkElement)sender).Parent as StackPanel;
                Border b = sp.Parent as Border;
                Popup popup = b.Parent as Popup;
                if (popup != null) {
                    popup.IsOpen = false;
                }
                Grid g = popup.Parent as Grid;
                if (g == null) return;
                Expr expr = g.DataContext as Expr;
                if (expr == null) return;
                string text = ((TextBox)sender).Text;
                Symbol sym = Symbol.FindSymbol(expr.Expression, expr.SequenceEntity.Parent);
                if (sym != null) {
                    sym.Definition = text;
                    sym.Expr.Evaluate();
                    Logger.Info("Setting " + expr.Expression + " to " + text + " in Sequencer+ Panel");
                }
            }
        }

        public void DragFeedback(object sender, GiveFeedbackEventArgs e) {
        }

        public void DropExpr(object sender, DragEventArgs e) {
            if (e.Source is TextBlock tb && tb.DataContext is DockableExpr de) {
                Grid gg = tb.Parent as Grid;
                if (gg != null) {
                    gg.Opacity = 1;
                    gg.Background = OldBackground;
                }
            }

            Expr target = ((FrameworkElement)sender).DataContext as Expr;
            ObservableCollection<DockableExpr> exprs = SequencerPlusPluginDockable.ExpressionList;
            if (target == null) return;
            int targetIndex = -1;

            for (int i = 0; i < exprs.Count; i++) {
                if (exprs[i] == target) {
                    targetIndex = i;
                }
            }

            if (targetIndex < 0) return;

            string data = (string)e.Data.GetData(DataFormats.StringFormat);
            Int32 sourceIndex = Int32.Parse(data);

            if (targetIndex == sourceIndex) return;

            DockableExpr source = exprs[sourceIndex];
            if (targetIndex > sourceIndex) {
                for (int i = sourceIndex + 1; i <= targetIndex; i++) {
                    exprs[i - 1] = exprs[i];
                }
            } else {
                for (int i = sourceIndex - 1; i >= targetIndex; i--) {
                    exprs[i + 1] = exprs[i];
                }
            }
            exprs[targetIndex] = source;
            SequencerPlusPluginDockable.SaveDockableExprs();
            Logger.Info("Item " + e.Data.GetData(DataFormats.StringFormat) + " dropped at " + ((FrameworkElement)sender).DataContext);
        }

        public void DragEnter(object sender, DragEventArgs e) {
            if (e.Source is TextBlock tb && tb.DataContext is DockableExpr de) {
                Grid gg = tb.Parent as Grid;
                if (gg != null) {
                    OldBackground = gg.Background;
                    gg.Background = new SolidColorBrush(Colors.LightBlue);
                    gg.Opacity = .75;
                }
            }
           
            Logger.Info("Enter");

        }

        public Brush OldBackground { get; private set; }

        public void DragLeave(object sender, DragEventArgs e) {
            if (e.Source is TextBlock tb && tb.DataContext is DockableExpr de) {
                Grid gg = tb.Parent as Grid;
                if (gg != null) {
                    gg.Opacity = 1;
                    gg.Background = OldBackground;
                }
            }
            Logger.Info("Leave");

        }

        private void PreviewDragOver(object sender, DragEventArgs e) {
            e.Handled = true;
        }

        private void MouseMove(object sender, MouseEventArgs e) {
            // If the mousebutton isn't pressed, return immediately;
            if (e.LeftButton != MouseButtonState.Pressed)
                return;

            // Cast the sender (TextBox) to 0a F0rameworkElement
            // So we can grab the DataContext
            FrameworkElement fe = sender as FrameworkElement;
            if (fe == null)
                return;

            Grid g = fe.Parent as Grid;
            if (g == null) {
                return;
            }

            bool found = false;
            int i = 0;
            foreach (DockableExpr expr in SequencerPlusPluginDockable.ExpressionList) {
                if (expr == g.DataContext) {
                    found = true;
                    break;
                }
                i++;
            }

            if (!found) {
                Logger.Warning("WTF?");
                return;
            }

            e.Handled = true;

            // Wrap the data.
            DataObject data = new DataObject();
            data.SetData(i.ToString());

            Logger.Info("Dragging item #" + i);

            TextBox tb = null;
            foreach (UIElement item in g.Children) {
                if (item is TextBox) {
                    tb = (TextBox)item;
                    break;
                }
            }

            CreateDragDropWindow(tb);
            System.Windows.DragDrop.AddQueryContinueDragHandler(g, DragContinueHandler);
            System.Windows.DragDrop.AddGiveFeedbackHandler(g, DragFeedbackHandler);

            // Initiate the drag-and-drop operation.
            DragDrop.DoDragDrop(g, data, DragDropEffects.Move);
        }

        private Window _dragdropWindow;

        private void CreateDragDropWindow(Visual dragElement) {
            _dragdropWindow = new Window {
                WindowStyle = WindowStyle.None,
                Opacity=.7,
                AllowsTransparency = true,
                AllowDrop = false,
                Background = Brushes.Transparent,
                IsHitTestVisible = false,
                SizeToContent = SizeToContent.WidthAndHeight,
                Topmost = true,
                ShowInTaskbar = false
            };

            Rectangle r = new Rectangle {
                Width = ((FrameworkElement)dragElement).ActualWidth,
                Height = ((FrameworkElement)dragElement).ActualHeight,
                Fill = new VisualBrush(dragElement)
            };
            _dragdropWindow.Content = r;

            Win32Point w32Mouse = new Win32Point();
            GetCursorPos(ref w32Mouse);


            _dragdropWindow.Left = w32Mouse.X;
            _dragdropWindow.Top = w32Mouse.Y;
            _dragdropWindow.Show();
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetCursorPos(ref Win32Point pt);

        [StructLayout(LayoutKind.Sequential)]
        internal struct Win32Point {
            public Int32 X;
            public Int32 Y;
        };

        public void DragFeedbackHandler(object sender, GiveFeedbackEventArgs e) {
            Mouse.SetCursor(Cursors.Hand);
            e.Handled = true;
        }
        
        public void DragContinueHandler(object sender, QueryContinueDragEventArgs e) {
            if (e.Action == DragAction.Continue && e.KeyStates != DragDropKeyStates.LeftMouseButton) {
                _dragdropWindow.Close();
            } else {
                Win32Point w32Mouse = new Win32Point();
                GetCursorPos(ref w32Mouse);
                _dragdropWindow.Left = w32Mouse.X + 10;
                _dragdropWindow.Top = w32Mouse.Y + 10;
                //_dragdropWindow.Show();
            }
        }

    }
}
