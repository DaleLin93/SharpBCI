using System;
using System.Threading;
using SharpBCI.Extensions;

namespace SharpBCI.Experiments.MI
{
    [AppEntry("Sample Grab Test", false)]
    public class SampleGrabEntry : IAppEntry
    {

        public void Run()
        {
            new Thread(() => new DirectShowVideoSource(new Uri("file://d:/A.mp4"), true).Play()).Start();
            new Thread(() => new DirectShowVideoSource(new Uri("file://d:/B.mp4"), true).Play()).Start();
        }

    }
}
