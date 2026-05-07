using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus {
    public abstract class RunnerInstruction : SequenceItem {

 
        public Runner GetRunner() {
            // Find the Runner responsible for this command
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is Runner) {
                    return (Runner)p;
                }
                p = p.Parent;
            }
            return null;
        }

        public void ShouldRetry() {
            GetRunner().ShouldRetry = true;
        }

        public bool IsInIfContainer() {
            ISequenceContainer p = Parent;
            while (p != null) {
                if (p is Runner || p is IfContainer) return true;
                p = p.Parent;
            }
            return false;
        }

        private IList<string> issues = new List<string>();

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        public bool Validate() {
            var i = new List<string>();
            if (!IsInIfContainer()) {
                i.Add("This can only be executed inside an 'If' instruction!");
            }
            Issues = i;
            return i.Count == 0;
        }
    }
}
