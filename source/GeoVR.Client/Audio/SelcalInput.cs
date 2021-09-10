using Concentus.Enums;
using Concentus.Structs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Timers;

namespace GeoVR.Client
{
    public class SelcalInput
    {
        private static Dictionary<char, float> codeLookup = new Dictionary<char, float>
        {
            { 'A',  312.6f },
            { 'B',  346.7f },
            { 'C',  384.6f },
            { 'D',  426.6f },
            { 'E',  473.2f },
            { 'F',  524.8f },
            { 'G',  582.1f },
            { 'H',  645.7f },
            { 'J',  716.1f },
            { 'K',  794.3f },
            { 'L',  881.0f },
            { 'M',  977.2f },
            { 'P',  1083.9f },
            { 'Q',  1202.3f },
            { 'R',  1333.5f },
            { 'S',  1479.1f },
        };

        private static TimeSpan codeDuration = TimeSpan.FromSeconds(1);

        private readonly int frameSize = 960;
        private WaveFormat waveFormat;

        private OpusEncoder encoder;

        private SelcalOpusDataAvailableEventArgs opusDataAvailableArgs;

        private SignalGenerator signalGeneratorA;
        private SignalGenerator signalGeneratorB;
        private MixingSampleProvider signalMixer;
        private readonly float[] signalBuffer;
        private readonly byte[] encodedSignalBuffer;
        private uint audioSequenceCounter;
        private string tonesPlaying;
        private int toneSent;
        private Timer playbackTimer;

        public event EventHandler<SelcalOpusDataAvailableEventArgs> OpusDataAvailable;
        public event EventHandler Stopped;

        public bool Playing { get; private set; }
        public long OpusBytesEncoded { get; set; }

        public SelcalInput(int sampleRate)
        {
            waveFormat = new WaveFormat(sampleRate, 1);

            encoder = OpusEncoder.Create(sampleRate, 1, OpusApplication.OPUS_APPLICATION_VOIP);
            encoder.Bitrate = 16 * 1024;

            signalGeneratorA = new SignalGenerator(sampleRate, 1) { Gain = 0.07, Type = SignalGeneratorType.Sin };
            signalGeneratorB = new SignalGenerator(sampleRate, 1) { Gain = 0.07, Type = SignalGeneratorType.Sin };

            signalMixer = new MixingSampleProvider(WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1)) { ReadFully = false };

            signalBuffer = new float[frameSize];
            encodedSignalBuffer = new byte[1275];//the maximum encode size

            opusDataAvailableArgs = new SelcalOpusDataAvailableEventArgs();

            playbackTimer = new Timer(10);
            playbackTimer.Elapsed += PlaybackTimer_Elapsed;
        }

        /// <summary>
        /// Starts playback of SELCAL tones
        /// </summary>
        /// <param name="code">Capitalized SELCAL code without hyphen eg "ABCD"</param>
        public void Play(string code)
        {
            if (Playing)
                Stop();

            if (code.Length != 4 || code.Any(c=>!codeLookup.ContainsKey(c)))
                throw new ArgumentException("Invalid SELCAL tone");

            tonesPlaying = code;
            AddSignalsToMixer(code.Substring(0, 2));
            Playing = true;
            playbackTimer.Start();
        }

        public void Stop()
        {
            Playing = false;
            playbackTimer.Stop();
            tonesPlaying = string.Empty;
            toneSent = 0;
            Stopped?.Invoke(this, new EventArgs());
        }

        private void PlaybackTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!Playing)
                return;

            Array.Clear(signalBuffer, 0, signalBuffer.Length);
            int read = signalMixer.Read(signalBuffer, 0, frameSize);
            if (read == 0)//end of singals reached
            {
                if(++toneSent >= 2)
                {
                    Stop();
                    return;
                }
                else//this can only mean we have the second tone to play
                {
                    AddSignalsToMixer(tonesPlaying.Substring(2, 2));
                    playbackTimer.Start();
                    return;
                }
            }
            else //data available
            {
                int encodedDataLength = encoder.Encode(signalBuffer, 0, frameSize, encodedSignalBuffer, 0, encodedSignalBuffer.Length);
                OpusBytesEncoded += encodedDataLength;

                byte[] trimmedBuff = new byte[encodedDataLength];
                Buffer.BlockCopy(encodedSignalBuffer, 0, trimmedBuff, 0, encodedDataLength);
                opusDataAvailableArgs.Audio = trimmedBuff;
                opusDataAvailableArgs.SequenceCounter = audioSequenceCounter++;
                opusDataAvailableArgs.LastData = read < frameSize;
                OpusDataAvailable(this, opusDataAvailableArgs);

                playbackTimer.Start();
            }
        }

        private void AddSignalsToMixer(string partTone)
        {
            signalMixer.RemoveAllMixerInputs();
            signalGeneratorA.Frequency = codeLookup[partTone[0]];
            signalGeneratorB.Frequency = codeLookup[partTone[1]];
            signalMixer.AddMixerInput(signalGeneratorA.Take(codeDuration));
            signalMixer.AddMixerInput(signalGeneratorB.Take(codeDuration));
        }

        public class SelcalOpusDataAvailableEventArgs : OpusDataAvailableEventArgs
        {
            public bool LastData { get; set; }
        }
    }
}
