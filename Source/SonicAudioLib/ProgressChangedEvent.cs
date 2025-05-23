using System;

namespace SonicAudioLib;

public class ProgressChangedEventArgs(double progress) : EventArgs
{
    public double Progress { get; private set; } = Math.Round(progress, 2, MidpointRounding.AwayFromZero);
}

public delegate void ProgressChanged(object sender, ProgressChangedEventArgs e);