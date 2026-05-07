#region "copyright"

/*
    Copyright © 2016 - 2023 Stefan Berg <isbeorn86+NINA@googlemail.com> and the N.I.N.A. contributors

    This file is part of N.I.N.A. - Nighttime Imaging 'N' Astronomy.

    This Source Code Form is subject to the terms of the Mozilla Public
    License, v. 2.0. If a copy of the MPL was not distributed with this
    file, You can obtain one at http://mozilla.org/MPL/2.0/.
*/

#endregion "copyright"

using GalaSoft.MvvmLight.Command;
using NINA.Sequencer.SequenceItem;
using NINA.Sequencer.Validations;
using NINA.Core.Utility;
using NINA.Equipment.Interfaces.Mediator;
using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NINA.Core.Locale;
using NINA.Core.Enum;
using NINA.Sequencer.Utility;
using NINA.Sequencer.Trigger;
using NINA.Sequencer.Container;
using NINA.Sequencer.Conditions;
using Newtonsoft.Json;
using System.Reflection;
using NINA.Sequencer.Interfaces.Mediator;
using NINA.ViewModel.Sequencer;
using NINA.WPF.Base.Interfaces.Mediator;
using NINA.Core.Model;
using NINA.Astrometry;
using NINA.Equipment.Equipment.MyTelescope;

namespace NINA.Plugin.SequencerPlus {

    [ExportMetadata("Name", "When Becomes Unsafe")]
    [ExportMetadata("Description", "Runs a customizable set of instructions within seconds of an 'Unsafe' condition being recognized.")]
    [ExportMetadata("Icon", "ShieldSVG")]
    [JsonObject(MemberSerialization.OptIn)]

    public abstract class When : SequenceTrigger, IValidatable, IDSOTargetProxy {
        protected ISafetyMonitorMediator safetyMediator;
        protected ISequenceMediator sequenceMediator;
        protected ISequenceNavigationVM sequenceNavigationVM;
        protected IApplicationStatusMediator applicationStatusMediator;
        protected ISwitchMediator switchMediator;
        protected IWeatherDataMediator weatherMediator;
        protected ICameraMediator cameraMediator;
        protected ITelescopeMediator telescopeMediator;

        [ImportingConstructor]
        public When(ISafetyMonitorMediator safetyMediator, ISequenceMediator sequenceMediator, IApplicationStatusMediator applicationStatusMediator, ISwitchMediator switchMediator,
                IWeatherDataMediator weatherMediator, ICameraMediator cameraMediator, ITelescopeMediator telescopeMediator) {
            this.safetyMediator = safetyMediator;
            this.sequenceMediator = sequenceMediator;
            this.applicationStatusMediator = applicationStatusMediator;
            this.switchMediator = switchMediator;
            this.weatherMediator = weatherMediator;
            this.cameraMediator = cameraMediator;
            this.telescopeMediator = telescopeMediator;
            ConditionWatchdog = new ConditionWatchdog(InterruptWhen, TimeSpan.FromSeconds(5));
            Instructions = new IfContainer();
            Instructions.AttachNewParent(Parent);
            Instructions.PseudoParent = this;
            Instructions.Name = Name;
            Instructions.Icon = Icon;
            var fields = sequenceMediator.GetType().GetRuntimeFields();
            foreach (FieldInfo fi in fields) {
                if (fi.Name.Equals("sequenceNavigation")) {

                    sequenceNavigationVM = (ISequenceNavigationVM)fi.GetValue(sequenceMediator);
                }
            }
        }
        protected When(When cloneMe) : this(cloneMe.safetyMediator, cloneMe.sequenceMediator, cloneMe.applicationStatusMediator, cloneMe.switchMediator, cloneMe.weatherMediator, cloneMe.cameraMediator, cloneMe.telescopeMediator) {
            if (cloneMe != null) {
                CopyMetaData(cloneMe);
                Instructions = (IfContainer)cloneMe.Instructions.Clone();
                Instructions.AttachNewParent(Parent);
                Instructions.PseudoParent = this;
                Instructions.Name = cloneMe.Name;
                Instructions.Icon = cloneMe.Icon;
                whenId = ++WhenCounter;
            }
        }

        protected int whenId = 0;

        private static int WhenCounter = 0;

        public static bool inFlight = false;

        public bool InFlight {
            get => inFlight;
            protected set {
                inFlight = value;
                RaisePropertyChanged();
            }
        }

        public ConditionWatchdog ConditionWatchdog { get; set; }

        [JsonProperty]
        public IfContainer Instructions { get; protected set; }

        private bool isSafe;

        public bool IsSafe {
            get => isSafe;
            protected set {
                isSafe = value;
                RaisePropertyChanged();
            }
        }

        private bool iInterrupt = true;

        [JsonProperty]
        public bool Interrupt {
            get => iInterrupt;
            set {
                iInterrupt = value;
                RaisePropertyChanged();
            }
        }

        private IList<string> issues = new List<string>();

        public override void Initialize() {
            base.Initialize();
            Instructions.Initialize();
        }

        public IList<string> Issues {
            get => issues;
            set {
                issues = value;
                RaisePropertyChanged();
            }
        }

        protected void CommonValidate() {
            if (Instructions.PseudoParent == null) {
                Instructions.PseudoParent = this;
            }

            // Avoid infinite loop by checking first...
            if (Instructions.Parent != Parent) {
                Instructions.AttachNewParent(Parent);
            }

            foreach (ISequenceItem item in Instructions.Items) {
                if (item is IValidatable val) {
                    //item.AttachNewParent(Parent);
                    _ = val.Validate();
                }
            }
        }

        public bool Validate() {
            CommonValidate();

            var i = new List<string>();

            foreach (ISequenceItem item in Instructions.Items) {
                if (item is IValidatable v) {
                    _ = v.Validate();
                }
            }

            Issues = i;
            return i.Count == 0;
        }

        public abstract bool Check();

        public override void AfterParentChanged() {
            if (Parent == null) {
                SequenceBlockTeardown();
            } else {
                Instructions.AttachNewParent(Parent);
                if (Parent.Status == SequenceEntityStatus.RUNNING) {
                    SequenceBlockInitialize();
                }
            }
        }

        public override void SequenceBlockTeardown() {
            try { ConditionWatchdog?.Cancel(); } catch { }
        }

        public override void SequenceBlockInitialize() {
            ConditionWatchdog?.Start();
        }

        public string StartStop {
            get {
                return Stopped && InFlight ? "Reset Trigger" : Stopped ? "Restart" : "Pause";
            }
            set { }
        }

        private bool stopped = false;
        public bool Stopped {
            get => stopped;
            set {
                stopped = value;
                RaisePropertyChanged("StartStop");
            }
        }

        private ConditionWatchdog LoopWatchdog { get; set; }

        private bool Triggered { get; set; } = false;

        private bool Critical { get; set; } = false;

        private void UpdateChildren(ISequenceContainer c) {
            foreach (var item in c.Items) {
                item.AfterParentChanged();
            }
        }

        ISequenceItem RunningItem = null;

        private ISequenceContainer LoopTerminator;
        
        private bool CanContinue(ISequenceContainer container, ISequenceItem previousItem, ISequenceItem nextItem) {
            var conditionable = container as IConditionable;
            var canContinue = false;
            var conditions = conditionable?.GetConditionsSnapshot()?.Where(x => x.Status != SequenceEntityStatus.DISABLED).ToList();
            if (conditions != null && conditions.Count > 0) {
                canContinue = conditionable.CheckConditions(previousItem, nextItem);
                if (!canContinue) {
                    LoopTerminator = container;
                }
            } else {
                canContinue = container.Iterations < 1;
            }

            if (container.Parent != null) {
                canContinue = canContinue && CanContinue(container.Parent, previousItem, nextItem);
            }

            return canContinue;
        }

        private void CheckSlewing() {
            TelescopeInfo info = telescopeMediator.GetInfo();

            Logger.Warning("CheckSlewing, WhenUnsafe = " + (this is WhenUnsafe) + ", Connected = " + info.Connected + ", CanSlew = " + info.CanSlew + ", info.Slewing = " + info.Slewing);
            if (this is WhenUnsafe && info.Connected && info.CanSlew && info.Slewing) {
                try {
                    Logger.Warning("WBU, mount slewing; attempting to stop the slew");
                    telescopeMediator.StopSlew();
                    int timeout = 10;
                    while (info.Slewing && --timeout > 0) {
                        Thread.Sleep(1000);
                        info = telescopeMediator.GetInfo();
                    }
                    if (info.Slewing) {
                        Logger.Warning("WBU, timeout out after continuing to slew for 10 seconds");
                    }
                } catch (Exception e) {
                    Logger.Warning("WBU, can't stop telescope slew: " + e);
                }
            }

        }

        private async Task InterruptWhen() {
            Logger.Trace("*When Interrupt*");
            if (!sequenceMediator.Initialized || !sequenceMediator.IsAdvancedSequenceRunning()) return; 
            if (!Interrupt) return;
            if (InFlight || Triggered || Critical) {

                if (RunningItem != null) {
                    ISequenceContainer p = RunningItem.Parent;
                    if (p != null) {
                        LoopTerminator = null;
                        if (!CanContinue(p, PreviousItem, NextItem)) {
                            Logger.Info("Interrupted instruction's loop has terminated.  Stopping WBU Parent, " + Parent);
                            Parent.Interrupt();
                            //if (LoopTerminator != null) {
                            //    Logger.Info("Interrupted instruction's loop has terminated.  Stopping LoopTerminator, " + LoopTerminator);
                            //    await LoopTerminator.Interrupt();
                            //} else {
                            //    Logger.Info("Interrupted instruction's loop has terminated.  Stopping loop Parent, " + p);
                            //    await p.Interrupt();
                            //}
                            return;
                        }
                    }
                }

                Logger.Trace("InFlight/Triggered/Critical, return");
                return;
            }

            if (ShouldTrigger(null, null) && Parent != null) {
                Logger.Trace("ShouldTrigger returned true");
                if (ItemUtility.IsInRootContainer(Parent) && this.Parent.Status == SequenceEntityStatus.RUNNING && this.Status != SequenceEntityStatus.DISABLED) {
                    Target = DSOTarget.FindTarget(Parent);
                    if (Target != null) {
                        UpdateChildren(Instructions);
                    }
                    
                    // This is the only place Triggered is set TRUE
                    Triggered = true;
                    Logger.Info("Interrupting current Instruction Set");

                    Critical = true;
                    try {
                        RunningItem = SequencerPlusPlugin.GetRunningItem();
                        if (this is WhenUnsafe wbu) {
                            WhenUnsafe.RunningItem = RunningItem;
                        }

                        sequenceMediator.CancelAdvancedSequence();
                        Logger.Info("Canceling sequence...");

                        await Task.Delay(1000);
                        while (sequenceMediator.IsAdvancedSequenceRunning()) {
                            Logger.Info("Delay 1000");
                            await Task.Delay(1000);
                        }
                        Logger.Info("Sequence no longer running");
                        CheckSlewing();

                    } finally {
                        Critical = false;
                    }

                    await sequenceMediator.StartAdvancedSequence(true);
                    Logger.Info("Restarting sequence, Triggered -> true");
                } else {
                    if (!ItemUtility.IsInRootContainer(Parent)) {
                        Logger.Info("Can't run When because Parent isn't in root container, " + Parent.Name);
                    } else if (Parent.Status != SequenceEntityStatus.RUNNING) {
                        Logger.Info("Can't run When because Parent is not running, " + Parent.Name + ": " + Parent.Status);
                    }
                }
            } else {
                Logger.Trace("ShouldTrigger returned false");
            }
        }

        public override string ToString() {
            return $"Trigger: {nameof(When)}";
        }

        ISequenceItem NextItem;
        ISequenceItem PreviousItem;

        public override bool ShouldTrigger(ISequenceItem previousItem, ISequenceItem nextItem) {
            Logger.Trace("Id = " + whenId);
            if (InFlight) {
                Logger.Trace("ShouldTrigger: FALSE (InFlight) ");
                return false;
            }
            if (!Check()) {
                if (previousItem == null && nextItem == null) {
                    Logger.Info("ShouldTrigger TRUE from InterruptWhen, setting TriggerRunner");
                    return true;
                }

                Logger.Info("ShouldTrigger TRUE, setting TriggerRunner");
                TriggerRunner = Instructions;

                NextItem = nextItem;
                PreviousItem = previousItem;

                return true;
            }
            Logger.Trace("ShouldTrigger: FALSE");
            return false;
        }

        public async Task Execute(IProgress<ApplicationStatus> progress, CancellationToken token) {
            Logger.Info("Execute " + whenId);
            if (Critical) {
                Logger.Info("When: Execute in critical section; return");
                return;
            }
            if (InFlight) {
                Logger.Info("When: InFlight; return");
                return;
            }
            try {
                while (true) {
                    InFlight = true;
                    Triggered = false;
                    token.ThrowIfCancellationRequested();
                    Logger.Info("When: running TriggerRunner, InFlight -> true, Triggered -> false");
                    CheckSlewing();
                    await TriggerRunner.Run(progress, token);
                    token.ThrowIfCancellationRequested();
                    if (!(this is WhenUnsafe) || Check()) {
                        break;
                    }
                    TriggerRunner.ResetAll();
                }
            } finally {
                InFlight = false;
                Triggered = false;
                if (this is WhenSwitch w && w.OnceOnly) {
                    Logger.Info("When: Execute done; InFlight -> false, Triggered false, DISABLED");
                    w.Disabled = true;
                }
                Logger.Info("When: Execute done; InFlight -> false, Triggered false");
            }
        }

        public InputTarget DSOProxyTarget() {
            return Target;
        }
        
        public InputTarget Target = null;

        public InputTarget FindTarget(ISequenceContainer c) {
            while (c != null) {
                if (c is IDeepSkyObjectContainer dso) {
                    return dso.Target;
                } else {
                    c = c.Parent;
                }
            }
            return null;
        }

        public override Task Execute(ISequenceContainer context, IProgress<ApplicationStatus> progress, CancellationToken token) {
            return Execute(progress, token);
        }
    }
}