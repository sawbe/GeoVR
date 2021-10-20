using GeoVR.Shared;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GeoVR.Client
{
    // This needs to:
    // - Apply the blocking tone when applicable
    // - Apply the click sound when applicable
    // - Mix the CallsignSampleProviders together

    public class ReceiverSampleProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; private set; }

        public bool BypassEffects
        {
            set
            {
                bypassEffects = value;
                SetEffects();
                foreach (var voiceInput in voiceInputs)
                {
                    voiceInput.BypassEffects = value;
                }
            }
        }

        private const int hfFrequencyUpperLimit = 30000000;

        private uint frequency;
        public uint Frequency
        {
            get
            {
                return frequency;
            }
            set
            {
                if (value != frequency)      //If there has been a change in frequency...
                {
                    foreach (var voiceInput in voiceInputs)
                    {
                        voiceInput.Clear();
                    }
                }
                frequency = value;
                SetEffects();
            }
        }

        public int ActiveCallsigns { get { return voiceInputs.Count(x => x.InUse); } }

        public float Volume { get { return volume.Volume; } set { volume.Volume = value; } }

        private bool mute;
        public bool Mute
        {
            get
            {
                return mute;
            }
            set
            {
                mute = value;
                if (value)
                {
                    foreach (var voiceInput in voiceInputs)
                    {
                        voiceInput.Clear();
                    }
                }
                SetEffects();
            }
        }

        private const float clickGain = 1.1f;
        private const double blockToneGain = 0.13f;
        private const float hfWhiteNoiseGain = 0.10f;
        private const float hfCrackleMaxGain = 0.08f;
        private const float hfCrackleMinGain = 0.005f;
        private const int crackleGainUpdateInterval = 696;

        public ushort ID { get; private set; }

        public event EventHandler<TransceiverReceivingCallsignsChangedEventArgs> ReceivingCallsignsChanged;

        private readonly VolumeSampleProvider volume;
        private readonly MixingSampleProvider mixer;
        private readonly BlockingToneSampleProvider blockTone;
        private readonly ResourceSoundSampleProvider hfWhiteNoise;
        private readonly ResourceSoundSampleProvider hfCrackleSoundProvider;
        private readonly List<CallsignSampleProvider> voiceInputs;
        private readonly Random hfCrackleGainGenerator;

        private bool bypassEffects = false;
        private bool doClickWhenAppropriate = false;
        private int lastNumberOfInUseInputs = 0;
        private int crackleReadCounter = 0;
        private readonly bool hfSquelchEn = false;

        public ReceiverSampleProvider(WaveFormat waveFormat, ushort id, int voiceInputNumber)
        {
            WaveFormat = waveFormat;
            ID = id;

            mixer = new MixingSampleProvider(WaveFormat)
            {
                ReadFully = true
            };

            voiceInputs = new List<CallsignSampleProvider>();
            for (int i = 0; i < voiceInputNumber; i++)
            {
                var voiceInput = new CallsignSampleProvider(WaveFormat, this)
                {
                    BypassEffects = bypassEffects
                };
                voiceInputs.Add(voiceInput);
                mixer.AddMixerInput(voiceInput);
            };

            blockTone = new BlockingToneSampleProvider(WaveFormat.SampleRate, 1) { Frequency = 180, Gain = 0 };
            hfWhiteNoise = new ResourceSoundSampleProvider(Samples.Instance.HFWhiteNoise) { Looping = true, Gain = 0 };
            hfCrackleSoundProvider = new ResourceSoundSampleProvider(Samples.Instance.Crackle) { Looping = true, Gain = 0 };
            hfCrackleGainGenerator = new Random();
            
            mixer.AddMixerInput(blockTone.ToMono());
            if (!AudioConfig.Instance.HfSquelch)
            {
                mixer.AddMixerInput(hfWhiteNoise.ToMono());
                mixer.AddMixerInput(hfCrackleSoundProvider.ToMono());
            }
            volume = new VolumeSampleProvider(mixer);

            hfSquelchEn = AudioConfig.Instance.HfSquelch;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            var numberOfInUseInputs = voiceInputs.Count(x => x.InUse);
            if (numberOfInUseInputs > 1)
            {
                blockTone.Frequency = 180;
                blockTone.Gain = blockToneGain;
            }
            else
            {
                blockTone.Gain = 0;
            }

            if (doClickWhenAppropriate && numberOfInUseInputs == 0)
            {
                mixer.AddMixerInput(new ResourceSoundSampleProvider(Samples.Instance.Click) { Gain = clickGain });
                doClickWhenAppropriate = false;
            }

            if (numberOfInUseInputs != lastNumberOfInUseInputs)
            {
                ReceivingCallsignsChanged?.Invoke(this, new TransceiverReceivingCallsignsChangedEventArgs(ID, voiceInputs.Select(x => x.Callsign).Where(x => !string.IsNullOrWhiteSpace(x)).ToList()));
            }
            lastNumberOfInUseInputs = numberOfInUseInputs;

            if(frequency < hfFrequencyUpperLimit && crackleReadCounter++ > crackleGainUpdateInterval)
            {
                crackleReadCounter = 0;
                SetHfCrackle();
            }

            return volume.Read(buffer, offset, count);
        }

        public void AddOpusSamples(IAudioDto audioDto, uint frequency, float distanceRatio)
        {
            if (frequency != this.frequency)        //Lag in the backend means we get the tail end of a transmission if switching frequency in the middle of a transmission
                return;

            CallsignSampleProvider voiceInput = voiceInputs.FirstOrDefault(b => b.Callsign == audioDto.Callsign);
            if (voiceInput == null)
            {
                CallsignSampleProvider notInUseInput = voiceInputs.FirstOrDefault(b => !b.InUse);
                if (notInUseInput != null)//if more than 4 inputs required just do nothing?
                {
                    voiceInput = notInUseInput;
                    voiceInput.Active(audioDto.Callsign, "");
                }
            }

            voiceInput?.AddOpusSamples(audioDto, distanceRatio);
            if (!bypassEffects && (frequency > hfFrequencyUpperLimit || hfSquelchEn))
                doClickWhenAppropriate = true;
        }

        public void AddSilentSamples(IAudioDto audioDto, uint frequency, float distanceRatio)
        {
            if (frequency != this.frequency)
                return;
            //CallsignSampleProvider might change Callsign on us between calling .Any and .First. Use FirstAndDefault once instead
            CallsignSampleProvider voiceInput = voiceInputs.FirstOrDefault(b => b.Callsign == audioDto.Callsign);
            if (voiceInput == null)
            {
                CallsignSampleProvider notInUseInput = voiceInputs.FirstOrDefault(b => !b.InUse);
                if (notInUseInput != null)
                {
                    voiceInput = notInUseInput;
                    voiceInput.ActiveSilent(audioDto.Callsign, "");
                }
            }

            voiceInput?.AddSilentSamples(audioDto);
            //doClickWhenAppropriate = true;
        }

        private void SetEffects()
        {
            SetHfNoise();
            SetHfCrackle();
        }

        private void SetHfNoise()
        {
            if (!bypassEffects && !Mute && Frequency > 0 && Frequency < hfFrequencyUpperLimit)
            {
                hfWhiteNoise.Gain = hfWhiteNoiseGain;
            }
            else
                hfWhiteNoise.Gain = 0;
        }

        private void SetHfCrackle()
        {
            if (!bypassEffects && !Mute && Frequency > 0 && Frequency < hfFrequencyUpperLimit)
            {
                hfCrackleSoundProvider.Gain = Math.Max(hfCrackleMinGain, (float)(hfCrackleGainGenerator.NextDouble() * hfCrackleMaxGain));
            }
            else
                hfCrackleSoundProvider.Gain = 0;
        }
    }
}