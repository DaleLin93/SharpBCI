using MarukoLib.Lang;
using SharpBCI.Core.Experiment;

namespace SharpBCI.Extensions.Paradigms.Profiler
{

    [Paradigm(ParadigmName, typeof(Factory), "Diagnostics", "0.0.1", Description = "A I/O profiler tool for system diagnostics.")]
    public class ProfilerParadigm : Paradigm
    {
        
        public const string ParadigmName = "Profiler";

        public static readonly ProfilerParadigm Instance = new ProfilerParadigm();

        public class Factory : ParadigmFactory<ProfilerParadigm>
        {

            public override ProfilerParadigm Create(IReadonlyContext context) => Instance;

        }

        private ProfilerParadigm() : base(ParadigmName) { }

        public override void Run(Session session) => new ProfilerWindow(session).ShowDialog();

    }

}
