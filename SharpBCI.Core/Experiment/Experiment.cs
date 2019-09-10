using SharpBCI.Core.Staging;
using System;
using MarukoLib.Lang;
using Newtonsoft.Json;

namespace SharpBCI.Core.Experiment
{

    public class ExperimentInitiationException : Exception
    {

        public ExperimentInitiationException() { }

        public ExperimentInitiationException(string message) : base(message) { }

        public ExperimentInitiationException(string message, Exception innerException) : base(message, innerException) { }

    }

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

    public abstract class Experiment : IExperiment
    {

        protected Experiment(string name) => Name = name;

        public string Name { get; }

        public virtual IReadonlyContext Metadata { get; } = EmptyContext.Instance;

        public abstract void Run(Session session);

    }

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
