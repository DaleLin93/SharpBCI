using JetBrains.Annotations;
using MarukoLib.Lang;
using Newtonsoft.Json;
using SharpBCI.Core.Staging;

namespace SharpBCI.Core.Experiment
{

    /// <summary>
    /// Interface of Experiment Paradigms.
    /// </summary>
    public interface IParadigm
    {

        /// <summary>
        /// Name of paradigm.
        /// </summary>
        [NotNull] string Name { get; }

        /// <summary>
        /// Paradigm related metadata for interop.
        /// </summary>
        [NotNull] IReadonlyContext Metadata { get; }

        /// <summary>
        /// Run paradigm.
        /// </summary>
        /// <param name="session">Current session object.</param>
        void Run([NotNull] Session session);

    }

    /// <summary>
    /// An abstract basic paradigm.
    /// </summary>
    public abstract class Paradigm : IParadigm
    {

        protected Paradigm([NotNull] string name) => Name = name;

        public string Name { get; }

        public virtual IReadonlyContext Metadata { get; } = EmptyContext.Instance;

        public abstract void Run(Session session);

    }

    /// <summary>
    /// An paradigm that can be abstracted into several different stages.
    /// Using <see cref="StageProgram"/> as timeline. 
    /// </summary>
    public abstract class StagedParadigm : Paradigm
    {

        public abstract class Basic : StagedParadigm
        {

            protected Basic([NotNull] string name) : base(name) { }

            [NotNull]
            public sealed override StageProgram CreateStagedProgram(Session session) => new StageProgram(session.Clock, StageProviders);

            [NotNull]
            [JsonIgnore]
            protected abstract IStageProvider[] StageProviders { get; }

        }

        protected StagedParadigm([NotNull] string name) : base(name) { }

        public abstract StageProgram CreateStagedProgram([NotNull] Session session);

    }

}
