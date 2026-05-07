using NINA.Astrometry;
using NINA.Profile;
using NINA.Profile.Interfaces;
using NINA.Sequencer;
using NINA.Sequencer.Container;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Validations;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel.Composition;
using System.Linq;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "")]
    [ExportMetadata("Description", "Executes an instruction set if the predicate, based on the results of the previous instruction, is true")]
    [ExportMetadata("Icon", "Pen_NoFill_SVG")]
    //[Export(typeof(ISequenceItem))]
    [Export(typeof(ISequenceContainer))]
    [Newtonsoft.Json.JsonObject(Newtonsoft.Json.MemberSerialization.OptIn)]

    public class IfContainer : SequentialContainer, ISequenceContainer, IValidatable, IDeepSkyObjectContainer {


        public IfContainer() : base() {
        }

        public override IfContainer Clone() {
            IfContainer ic = new IfContainer();
            ic.Items = new ObservableCollection<ISequenceItem>(Items.Select(i => i.Clone() as ISequenceItem));
            foreach (var item in ic.Items) {
                item.AttachNewParent(ic);
            }
            AttachNewParent(Parent);
            return ic;
        }

        private Object lockObj = new Object();

        private ISequenceEntity iPseudoParent;

        public ISequenceEntity PseudoParent {
            get => iPseudoParent;
            set {
                iPseudoParent = value;
            }
        }

        public InputTarget Target {
            get {
                if (PseudoParent is IDSOTargetProxy w && w.DSOProxyTarget() != null) {
                    return w.DSOProxyTarget();
                }

                ISequenceContainer parent = PseudoParent as ISequenceContainer;
                while (parent != null) {
                    if (parent is IDeepSkyObjectContainer dso) {
                        return dso.Target;
                    }
                    parent = parent.Parent;
                }

                IProfileService profileService = SequencerPlusPlugin.ProfileService;
                InputTarget t = new InputTarget(Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Latitude), Angle.ByDegree(profileService.ActiveProfile.AstrometrySettings.Longitude), profileService.ActiveProfile.AstrometrySettings.Horizon);

                ISequenceContainer p = Parent;
                if (p == null) {
                    p = PseudoParent as ISequenceContainer;
                }
                if (p != null) {
                    ContextCoordinates cc = ItemUtility.RetrieveContextCoordinates(p);
                    if (cc != null) {
                        t.InputCoordinates.Coordinates = cc.Coordinates;
                        t.PositionAngle = cc.PositionAngle;
                    }
                }
                return t;
                //return null;
            }
            set { }
        }

        public NighttimeData NighttimeData => throw new NotImplementedException();
        public override void ResetProgress() {
            base.ResetProgress();
            if (PseudoParent != null && PseudoParent is WhenSwitch pp) {
                pp.Disabled = false;
            }
        }

        public override void Initialize() {
            base.Initialize();
            foreach (ISequenceItem item in Items) {
                item.Initialize();
            }

        }

        public new void MoveUp(ISequenceItem item) {
            lock (lockObj) {
                var index = Items.IndexOf(item);
                if (index == 0) {
                    return;
                } else {
                    base.MoveUp(item);
                }
            }
        }

    }
}
