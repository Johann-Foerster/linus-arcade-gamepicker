using System;
using System.Collections.Generic;

namespace Arcade {
    enum MazeCue { None, Pellet, Cleared, Caught, Launch, LaunchFailed, Returned }

    sealed class MazeSounds : IDisposable {
        const int SampleRate = 22050;
        const int ClearNoteMilliseconds = 76;
        readonly Dictionary<MazeCue, short[]> clips = new Dictionary<MazeCue, short[]>();
        WaveOutput output;
        double retryAt;
        double fanfareUntil;

        internal MazeSounds() {
            Add(MazeCue.Pellet, 42, .34, 784, 1175);
            Add(MazeCue.Cleared, ClearNoteMilliseconds, .22, 523, 659, 784, 1047);
            Add(MazeCue.Caught, 65, .23, 440, 311, 165);
            Add(MazeCue.Launch, 48, .18, 659, 880, 1175);
            Add(MazeCue.LaunchFailed, 82, .18, 262, 196);
            Add(MazeCue.Returned, 56, .18, 392, 587, 784);
            OpenOutput(0);
        }

        internal void Play(MazeCue cue, double now) {
            if (cue == MazeCue.None) return;
            if (cue == MazeCue.Pellet && now < fanfareUntil) return;
            if (output == null && now >= retryAt) OpenOutput(now);
            if (output != null && output.Play(cue) && cue == MazeCue.Cleared)
                fanfareUntil = now + (double)clips[cue].Length / SampleRate;
        }

        void OpenOutput(double now) {
            output = WaveOutput.Open(SampleRate, clips);
            retryAt = now + 1;
        }

        void Add(MazeCue cue, int noteMilliseconds, double volume, params double[] notes) {
            int samplesPerNote = SampleRate * noteMilliseconds / 1000;
            var samples = new short[samplesPerNote * notes.Length];
            int position = 0;
            foreach (double frequency in notes) {
                double phaseStep = 2 * Math.PI * frequency / SampleRate;
                for (int i = 0; i < samplesPerNote; i++) {
                    double phase = i * phaseStep;
                    double attack = i / (SampleRate * .003);
                    double release = (samplesPerNote - 1 - i) / (samplesPerNote * .4);
                    double envelope = Math.Min(1, Math.Min(attack, release));
                    double tone = .74 * Math.Sin(phase) + .18 * Math.Sin(3 * phase) + .08 * Math.Sin(5 * phase);
                    samples[position++] = (short)(short.MaxValue * volume * envelope * tone);
                }
            }
            clips.Add(cue, samples);
        }

        public void Dispose() {
            if (output != null) output.Dispose();
        }
    }
}
