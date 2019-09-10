using System;
using System.Diagnostics;
using System.Threading;
using MarukoLib.Lang;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpBCI.Core.Staging;

namespace SharpBCI.Tests
{
    [TestClass]
    public class StageProgramTests
    {

        [TestMethod]
        public void TestPauseResume()
        {
            var stopwatch = new Stopwatch();
            var stageProvider = new StageProvider(new Stage { Duration = 2000 }, new Stage { Duration = 2000 });
            var program = new StageProgram(Clock.SystemTicksClock, stageProvider);
            program.Started += (sender, e) => stopwatch.Restart();
            var evt = new ManualResetEvent(false);
            program.Stopped += (sender, e) =>
            {
                stopwatch.Stop();
                evt.Set();
            };
            program.Start();
            Thread.Sleep(2500);
            Assert.IsTrue(program.Pause());
            Thread.Sleep(2500);
            Assert.IsTrue(program.Resume());
            evt.WaitOne();
            const double estimatedSeconds = 6.5;
            var elapsedTime = stopwatch.Elapsed;
            Assert.IsTrue(Math.Abs(elapsedTime.TotalSeconds - estimatedSeconds) < 1);
        }

        [TestMethod]
        public void TestPauseResume2()
        {
            var stopwatch = new Stopwatch();
            var stageProvider = new StageProvider(new Stage {Duration = 1000}, new Stage {Marker = 1}, new Stage {Duration = 1000});
            var program = new StageProgram(Clock.SystemTicksClock, stageProvider);
            program.Started += (sender, e) => stopwatch.Restart();
            program.StageChanged += (sender, e) => e.Action = (e.Stage?.Marker ?? 0) == 1 ? StageAction.Pause : e.Action;
            var evt = new ManualResetEvent(false);
            program.Stopped += (sender, e) =>
            {
                stopwatch.Stop();
                evt.Set();
            };
            program.Start();
            Thread.Sleep(5000);
            Assert.IsTrue(program.Resume());
            evt.WaitOne();
            const int estimatedSeconds = 6;
            var elapsedTime = stopwatch.Elapsed;
            Assert.IsTrue(Math.Abs(elapsedTime.TotalSeconds - estimatedSeconds) < 1);
        }

    }
}
