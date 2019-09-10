using SharpBCI.Core.Staging;
using MarukoLib.Lang;
using Newtonsoft.Json;

namespace SharpBCI.Core.Experiment
{

    /// <summary>
    /// Interface of Experiment.
    /// </summary>
    public interface IExperiment
    {

        /// <summary>
        /// Name of experiment.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Experiment related metadata for interop.
        /// </summary>
        IReadonlyContext Metadata { get; }

        /// <summary>
        /// Run experiment.
        /// </summary>
        /// <param name="session">Current session object.</param>
        void Run(Session session);

    }

    /// <summary>
    /// An abstract basic experiment.
    /// </summary>
    public abstract class Experiment : IExperiment
    {

        protected Experiment(string name) => Name = name;

        public string Name { get; }

        public virtual IReadonlyContext Metadata { get; } = EmptyContext.Instance;

        public abstract void Run(Session session);

    }

    /// <summary>
    /// An experiment that can be abstracted into several different stages.
    /// Using <see cref="StageProgram"/> as timeline. 
    /// </summary>
    public abstract class StagedExperiment : Experiment
    {

        public abstract class Basic : StagedExperiment
        {

            protected Basic(string name) : base(name) { }

            public sealed override StageProgram CreateStagedProgram(Session session) => new StageProgram(session.Clock, StageProviders);

            [JsonIgnore]
            protected abstract IStageProvider[] StageProviders { get; }

        }

        protected StagedExperiment(string name) : base(name) { }

        public abstract StageProgram CreateStagedProgram(Session session);

    }

}
