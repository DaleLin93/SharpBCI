using System;
using System.Diagnostics;
using System.Threading;
using MarukoLib.Lang;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharpBCI.Core.Staging;

namespace SharpBCI.Tests
{
    [TestClass]
    public class StageProviderTests
    {

        private static StageProvider CreateStageProvider(int stageCount)
        {
            var stages = new Stage[stageCount];
            for (var i = 0; i < stages.Length; i++) stages[i] = new Stage{Marker = i};
            return new StageProvider(stages);
        }

        [TestMethod]
        public void TestCompositeStageProvider()
        {
            var stageProvider0 = CreateStageProvider(3);
            var stageProvider1 = CreateStageProvider(3);
            var stageProvider = new CompositeStageProvider(stageProvider0, stageProvider1);
            Assert.AreEqual(stageProvider0.Count + stageProvider1.Count, stageProvider.AsEnumerable().Count());
        }

        [TestMethod]
        public void TestEnumerable()
        {
            var stageProvider = new StageProvider(new Stage(), new Stage(), new Stage());
            Assert.AreEqual(stageProvider.Count, stageProvider.AsEnumerable().Count());
        }

        [TestMethod]
        public void TestBreak0()
        {
            var stageProvider0 = CreateStageProvider(3);
            var stageProvider1 = CreateStageProvider(3);
            var stageProvider = new CompositeStageProvider(stageProvider0, stageProvider1);
            stageProvider.Next();
            stageProvider0.Break();
            Assert.AreEqual(3, stageProvider.AsEnumerable().Count());
        }

        [TestMethod]
        public void TestBreak1()
        {
            var stageProvider0 = CreateStageProvider(3);
            var stageProvider1 = CreateStageProvider(3);
            var stageProvider = new CompositeStageProvider(stageProvider0, stageProvider1);
            for (var i = 0; i < 4; i++) stageProvider.Next();
            stageProvider1.Break();
            Assert.AreEqual(0, stageProvider.AsEnumerable().Count());
        }

    }
}
