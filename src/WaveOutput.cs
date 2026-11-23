using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Arcade {
    sealed class WaveOutput : IDisposable {
        const uint DefaultDevice = uint.MaxValue;
        const int HeaderInQueue = 0x10;
        static readonly int HeaderSize = Marshal.SizeOf(typeof(WaveHeader));
        static readonly int FlagsOffset = Marshal.OffsetOf(typeof(WaveHeader), "Flags").ToInt32();

        readonly Dictionary<MazeCue, IntPtr> buffers = new Dictionary<MazeCue, IntPtr>();
        IntPtr device;
        IntPtr currentBuffer;

        WaveOutput(IntPtr device) {
            this.device = device;
        }

        internal static WaveOutput Open(int sampleRate, Dictionary<MazeCue, short[]> clips) {
            var format = new WaveFormat {
                FormatTag = 1, Channels = 1, SamplesPerSecond = (uint)sampleRate,
                BytesPerSecond = (uint)sampleRate * sizeof(short), BlockAlign = sizeof(short), BitsPerSample = 16
            };
            IntPtr device;
            if (waveOutOpen(out device, new UIntPtr(DefaultDevice), ref format, IntPtr.Zero, IntPtr.Zero, 0) != 0)
                return null;

            var output = new WaveOutput(device);
            foreach (var clip in clips) {
                if (output.Prepare(clip.Key, clip.Value)) continue;
                output.Dispose();
                return null;
            }
            return output;
        }

        bool Prepare(MazeCue cue, short[] samples) {
            int length = samples.Length * sizeof(short);
            IntPtr buffer = Marshal.AllocHGlobal(HeaderSize + length);
            IntPtr data = IntPtr.Add(buffer, HeaderSize);
            Marshal.Copy(samples, 0, data, samples.Length);
            Marshal.StructureToPtr(new WaveHeader { Data = data, BufferLength = (uint)length }, buffer, false);
            if (waveOutPrepareHeader(device, buffer, (uint)HeaderSize) != 0) {
                Marshal.FreeHGlobal(buffer);
                return false;
            }
            buffers.Add(cue, buffer);
            return true;
        }

        internal bool Play(MazeCue cue) {
            if (currentBuffer != IntPtr.Zero && (Marshal.ReadInt32(currentBuffer, FlagsOffset) & HeaderInQueue) != 0) {
                // Interrupt the old cue instead of queueing feedback behind it.
                if (waveOutReset(device) != 0) return false;
            }
            IntPtr buffer = buffers[cue];
            if (waveOutWrite(device, buffer, (uint)HeaderSize) != 0) return false;
            currentBuffer = buffer;
            return true;
        }

        public void Dispose() {
            if (device == IntPtr.Zero) return;
            waveOutReset(device);
            foreach (IntPtr buffer in buffers.Values) waveOutUnprepareHeader(device, buffer, (uint)HeaderSize);
            // The device can still reference buffers if closing it fails.
            if (waveOutClose(device) != 0) return;
            device = IntPtr.Zero;
            foreach (IntPtr buffer in buffers.Values) Marshal.FreeHGlobal(buffer);
            buffers.Clear();
        }

        [StructLayout(LayoutKind.Sequential, Pack = 2)]
        struct WaveFormat {
            public ushort FormatTag, Channels;
            public uint SamplesPerSecond, BytesPerSecond;
            public ushort BlockAlign, BitsPerSample, ExtraSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct WaveHeader {
            public IntPtr Data;
            public uint BufferLength, BytesRecorded;
            public UIntPtr User;
            public uint Flags, Loops;
            public IntPtr Next;
            public UIntPtr Reserved;
        }

        [DllImport("winmm.dll")] static extern uint waveOutOpen(out IntPtr device, UIntPtr deviceId, ref WaveFormat format, IntPtr callback, IntPtr instance, uint flags);
        [DllImport("winmm.dll")] static extern uint waveOutPrepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutWrite(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutReset(IntPtr device);
        [DllImport("winmm.dll")] static extern uint waveOutUnprepareHeader(IntPtr device, IntPtr header, uint size);
        [DllImport("winmm.dll")] static extern uint waveOutClose(IntPtr device);
    }
}
