using NINA.Astrometry;
using NINA.Sequencer.Container;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NINA.Plugin.SequencerPlus {
    public interface IDSOTargetProxy {
        public InputTarget DSOProxyTarget();
        public InputTarget FindTarget(ISequenceContainer c);
    }
}
