using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NINA.Sequencer.Trigger;

namespace NINA.Plugin.SequencerPlus {
    public abstract class DIYMFOnly : SequenceItem, IValidatable {
        public abstract IList<string> Issues { get; set; }

        public abstract string FlipStatus { get; set; }

        public override void AfterParentChanged() {
            Validate();
        }

        public abstract bool Validate();

        protected bool IsInsideMeridianFlipEvent() {
            SequenceContainer p = Parent as SequenceContainer;
            while (p != null) {
                // Found the DSO container; look at triggers
                foreach (SequenceTrigger t in p.Triggers) {
                    if (t is DIYMeridianFlipTrigger diyTrigger) {
                        return true;
                        //SequenceContainer pp = diyTrigger.Parent as SequenceContainer;
                        //while (pp != null) {
                        //    if (pp is IDeepSkyObjectContainer) return true;
                         //   pp = (SequenceContainer)pp.Parent;
                        //}
                    }
                }
                p = (SequenceContainer)p.Parent;
            }
            var i = new List<string>();
            i.Add("This instruction MUST be inside a DIY Meridian Flip trigger");
            FlipStatus = "This instruction must be within a DIY Meridian Flip trigger";
            RaisePropertyChanged("FlipStatus");
            Issues = i;
            return false;
        }

    }
}
