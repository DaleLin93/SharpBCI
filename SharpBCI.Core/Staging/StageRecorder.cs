using System;
using System.Collections.Generic;
using System.Linq;
using MarukoLib.Lang.Exceptions;

namespace SharpBCI.Core.Staging
{

    public class StageRecorder 
    {
        
        private readonly object _lock = new object();

        private readonly ICollection<Stage> _stages = new LinkedList<Stage>();

        public StageProgram AttachedProgram { get; private set; }

        public Stage[] Stages
        {
            get
            {
                lock (_stages)
                    return _stages.ToArray();
            }
        }

        public void Attach(StageProgram program)
        {
            if (AttachedProgram == null) throw new ArgumentNullException(nameof(program));
            lock (_lock)
            {
                if (AttachedProgram != null) throw new StateException("Program is already attached.");
                AttachedProgram = program;
                program.StageChanged += ProgramOnStageChanged;
            }
        }

        public void Detach()
        {
            lock (_lock)
            {
                if (AttachedProgram == null) throw new StateException("No program attached currently.");
                AttachedProgram.StageChanged -= ProgramOnStageChanged;
                AttachedProgram = null;
            }
        }

        public void Reset()
        {
            lock (_stages)
                _stages.Clear();
        }

        private void ProgramOnStageChanged(object sender, StageChangedEventArgs e)
        {
            if (e.TryGetStage(out var stage))
                lock (_stages)
                    _stages.Add(stage);
        }

    }

}
