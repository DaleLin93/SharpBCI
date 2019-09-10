using System;
using System.Threading;
using SharpBCI.Extensions;

namespace SharpBCI.Experiments.MI
{
    [AppEntry(false)]
    public class SampleGrabEntry : IAppEntry
    {

        public string Name => "Sample Grab Test";

        public void Run()
        {
            new Thread(() => new DirectShowVideoSource(new Uri("file://d:/A.mp4"), true).Play()).Start();
            new Thread(() => new DirectShowVideoSource(new Uri("file://d:/B.mp4"), true).Play()).Start();
        }
    }
}
