using NAudio.Dsp;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    /// <summary>
    /// Basic example of a multi-band eq
    /// uses the same settings for both channels in stereo audio
    /// Call Update after you've updated the bands
    /// Potentially to be added to NAudio in a future version
    /// </summary>
    public class EqualizerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;
        private readonly BiQuadFilter[,] filters;
        private readonly int channels;
        private readonly int bandCount;
        private bool updated;

        public EqualizerSampleProvider(ISampleProvider sourceProvider, EqualizerPresets preset)
        {
            this.sourceProvider = sourceProvider;
            channels = this.sourceProvider.WaveFormat.Channels;
            filters = SetupPreset(preset, this.sourceProvider.WaveFormat.SampleRate);
            bandCount = filters.Length;
            Bypass = false;
            OutputGain = 1.0;
        }

        public EqualizerSampleProvider(ISampleProvider sourceProvider, BiQuadFilter[,] filters)
        {
            this.sourceProvider = sourceProvider;
            channels = this.sourceProvider.WaveFormat.Channels;
            this.filters = filters;
            bandCount = filters.Length;
            Bypass = false;
            OutputGain = 1.0;
        }

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;
        public bool Bypass { get; set; }
        public double OutputGain { get; set; }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);
            if (Bypass)
                return samplesRead;

            if (updated)
            {
                // CreateFilters();
                updated = false;
            }

            for (int n = 0; n < samplesRead; n++)
            {
                int ch = n % channels;

                for (int band = 0; band < bandCount; band++)
                {
                    buffer[offset + n] = filters[ch, band].Transform(buffer[offset + n]);
                }

                buffer[offset + n] *= (float)OutputGain;
            }
            return samplesRead;
        }

        private BiQuadFilter[,] SetupPreset(EqualizerPresets preset, float sampleRate)
            {
            BiQuadFilter[,] filters;
            switch (preset)
            {
                case EqualizerPresets.VHFEmulation:
                    filters = new BiQuadFilter[channels, 5];

                    for (int i = 0; i < channels; i++)
                    {
                        filters[i, 0] = BiQuadFilter.HighPassFilter(sampleRate, 310, 0.25f);

                        filters[i, 1] = BiQuadFilter.PeakingEQ(sampleRate, 450, 0.75f, 17.0f);

                        filters[i, 2] = BiQuadFilter.PeakingEQ(sampleRate, 1450, 1.0f, 25.0f);

                        filters[i, 3] = BiQuadFilter.PeakingEQ(sampleRate, 2000, 1.0f, 25.0f);

                        filters[i, 4] = BiQuadFilter.LowPassFilter(sampleRate, 2500, 0.25f);

                        // filters[i,k] = BiQuadFilter(a0, a1, a2, b0, b1, b2);

                       // filters[i, 0] = new BiQuadFilterExt(1, 0, 0, 0.005591900032114, 0, 0); // constant gain
                        //filters[i, 1] = new BiQuadFilterExt(1, -2.246084053669330, 1.308048105744373, 1, -2.049077000906383, 1.049132214936655);
                        //filters[i, 2] = new BiQuadFilterExt(1, -1.717126913902030, 0.764498261867003, 1, -1.953113876762170, 0.953166433747108);
                        //filters[i, 3] = new BiQuadFilterExt(1, -1.968349536365062, 1.094229495690919, 1, -3.207563403598932, 3.286247911643905);
                        //filters[i, 4] = new BiQuadFilterExt(1, -1.798845366042862, 0.913885222155853, 1, -0.929195887161554, 0.324601315276864);
                        //filters[i, 5] = new BiQuadFilterExt(1, -2.058902946698752, 1.061302377718410, 1, -1.843447350526883, 1.136863417829124);
                        //filters[i, 6] = new BiQuadFilterExt(1, -1.939976636135262, 0.942237894371066, 1, -1.650544977015706, 0.904043632776936);

                    }
                    break;
                default:
                    throw new Exception("Preset not defined");
            }
            return filters;
        }
    }

    public enum EqualizerPresets
    {
        VHFEmulation = 1
    }
}
