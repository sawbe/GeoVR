using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeoVR.Shared;

namespace GeoVR.Client
{
    public class Soundcard
    {
        private static ushort soundcardCounter = 0;
        private readonly Input input;
        private readonly Output output;
        private readonly SoundcardSampleProvider soundcardSampleProvider;
        private readonly VolumeSampleProvider volumeSampleProvider;
        private readonly List<ushort> transceiverIds = new List<ushort>();
        private List<TxTransceiverDto> transmitTrans = new List<TxTransceiverDto>();
        private double maxDbReadingInPTTInterval = -100;
        private bool transmitActive;
        private bool transmitActiveHistory;

        public ushort ID { get; }
        public bool Started { get; internal set; }
        public bool BypassEffects { set { soundcardSampleProvider.BypassEffects = value; } }
        public string InputDeviceName => input?.DeviceName;
        public string OutputDeviceName => output.DeviceName;
        public ushort[] TransceiverIds => transceiverIds.ToArray();
        public TxTransceiverDto[] TransmittingTransceivers => transmitTrans.ToArray();
        private float inputVolumeDb;
        public float InputVolumeDb
        {
            set
            {
                if (value > 18) { value = 18; }
                if (value < -18) { value = -18; }
                inputVolumeDb = value;
                input.Volume = (float)System.Math.Pow(10, value / 20);
            }
            get
            {
                return inputVolumeDb;
            }
        }

        private float outputVolume = 1;
        public float OutputVolumeDb
        {
            set
            {
                if (value > 18) { value = 18; }
                if (value < -60) { value = -60; }
                outputVolume = (float)System.Math.Pow(10, value / 20);
                if (volumeSampleProvider != null)
                    volumeSampleProvider.Volume = outputVolume;
            }
        }

        public event EventHandler<InputVolumeStreamEventArgs> InputVolumeStream
        {
            add { input.InputVolumeStream += value; }
            remove { input.InputVolumeStream -= value; }
        }

        internal event EventHandler<SoundcardRadioTxAvailableEventArgs> RadioTxAvailable;

        public Soundcard(MMDevice inputDevice, MMDevice outputDevice, int sampleRate, List<ushort> transceiverIDs, EventHandler<TransceiverReceivingCallsignsChangedEventArgs> receivingCallsignsChangedHandler)
        {
            ID = soundcardCounter++;
            output = new Output(outputDevice);
            if (inputDevice != null)
            {
                input = new Input(inputDevice, sampleRate);
                input.InputVolumeStream += Input_InputVolumeStream;
                input.OpusDataAvailable += Input_OpusDataAvailable;
            }
            transceiverIds = transceiverIDs;
            soundcardSampleProvider = new SoundcardSampleProvider(sampleRate, transceiverIDs, receivingCallsignsChangedHandler);
            volumeSampleProvider = new VolumeSampleProvider(soundcardSampleProvider)
            {
                Volume = outputVolume
            };
        }

        internal void Start()
        {
            if (Started)
                throw new Exception("Soundcard already started");

            Started = true;

            output.Start(volumeSampleProvider);
            input?.Start();
        }

        internal void Stop()
        {
            if (!Started)
                throw new Exception("Soundcard not started");

            Started = false;

            output.Stop();
            if (input?.Started == true)
                input.Stop();
        }

        internal void ChangeInputDevice(MMDevice inputDevice)
        {
            if (!Started)
                throw new Exception("Soundcard must be started first");
            if (input is null)
                throw new Exception("Input was never initialized");

            if (input.Started)
                input.Stop();
            input.Start(inputDevice);
        }

        internal void ChangeOutputDevice(MMDevice outputDevice)
        {
            if (!Started)
                throw new Exception("Soundcard must be started first");

            output.Stop();
            output.Start(outputDevice, volumeSampleProvider);
        }

        internal void ProcessRadioRx(RadioRxDto dto)
        {
            if(output.Started)
                soundcardSampleProvider.AddOpusSamples(dto, dto.Transceivers);
        }

        internal void UpdateTransceivers(List<TransceiverDto> trans)
        {
            soundcardSampleProvider.UpdateTransceivers(trans);
        }

        internal void PTT(bool active)
        {
            if (transmitActive == active)
                return;

            soundcardSampleProvider.PTT(active, TransmittingTransceivers);
            transmitActive = active;

            if (!active)
                maxDbReadingInPTTInterval = -100;
        }
        /// <summary>
        /// Updates transceivers that will receive audio when PTT is active.
        /// Only transceiver IDs matching this soundcard will be added
        /// </summary>
        /// <param name="txTranceiverIDs">IDs of Transceivers to transmit on</param>
        public void UpdateTransmittingTransceivers(List<ushort> txTranceiverIDs)
        {
            var con = transceiverIds.Intersect(txTranceiverIDs);
            List<TxTransceiverDto> dtos = new List<TxTransceiverDto>();
            foreach (ushort id in con)
                dtos.Add(new TxTransceiverDto() { ID = id });
            transmitTrans = dtos;
        }
        /// <summary>
        /// Sets the one transceiver that will receive audio when PTT is active 
        /// or none if no IDs match transceivers used by this soundcard
        /// </summary>
        /// <param name="singleTxTransID"></param>
        public void UpdateTransmittingTransceivers(ushort singleTxTransID)
        {
            if (transceiverIds.Contains(singleTxTransID))
                transmitTrans = new List<TxTransceiverDto>() { new TxTransceiverDto() { ID = singleTxTransID } };
            else if(transmitTrans.Count > 0)
                transmitTrans = new List<TxTransceiverDto>();
        }

        /// <summary>
        /// Adds or removes transceivers to existing soundcard
        /// Useful for ATC clients when not enough starting IDs to cover additional frequencies
        /// Each ID corresponds to a sample provider, using minimum value suggested.
        /// </summary>
        /// <param name="ids"></param>
        public void UpdateTransceiverIDs(List<ushort> ids)
        {
            if (output.Started)
                output.Stop();

            transceiverIds.Clear();
            transceiverIds.AddRange(ids);
            soundcardSampleProvider.UpdateReceiverInputs(ids);

            if (Started)
                output.Start(volumeSampleProvider);
        }

        public void SetOutputVolume(float volume)
        {
            volumeSampleProvider.Volume = volume;
        }

        public void SetInputVolume(float volume)
        {
            input.Volume = volume;
        }
        private void Input_InputVolumeStream(object sender, InputVolumeStreamEventArgs e)
        {
            if (transmitActive)
            {
                //Gather AGC stats
                if (e.PeakDB > maxDbReadingInPTTInterval)
                    maxDbReadingInPTTInterval = e.PeakDB;
            }
        }

        private void Input_OpusDataAvailable(object sender, OpusDataAvailableEventArgs e)
        {
            if (transmitTrans == null || transmitTrans.Count == 0)
            {
                return;
            }

            if (transmitActive)
            {
                var tx = new RadioTxDto()
                {
                    SequenceCounter = e.SequenceCounter,
                    Audio = e.Audio,
                    LastPacket = false,
                    Transceivers = TransmittingTransceivers
                };
                RadioTxAvailable?.Invoke(this, new SoundcardRadioTxAvailableEventArgs(tx));
            }
            if (!transmitActive && transmitActiveHistory)
            {
                var tx = new RadioTxDto()
                {
                    SequenceCounter = e.SequenceCounter,
                    Audio = e.Audio,
                    LastPacket = true,
                    Transceivers = TransmittingTransceivers
                };
                RadioTxAvailable?.Invoke(this, new SoundcardRadioTxAvailableEventArgs(tx));
            }
            transmitActiveHistory = transmitActive;
        }
    }

    public class SoundcardRadioTxAvailableEventArgs : EventArgs
    { 
        public RadioTxDto RadioTx { get; }

        public SoundcardRadioTxAvailableEventArgs(RadioTxDto dto)
        {
            RadioTx = dto;
        }
    }
}
