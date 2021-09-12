using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using GeoVR.Shared;
using NAudio.Wave.SampleProviders;
using NAudio.Utils;
using System.Net;
using System.Net.Sockets;
using RestSharp;
using Concentus.Structs;
using Concentus.Enums;
using System.Diagnostics;
using NLog;
using NAudio.CoreAudioApi;

namespace GeoVR.Client
{
    public class UserClient : BaseClient, IClient
    {
        Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private CancellationTokenSource playbackCancelTokenSource;
        private Task taskAudioPlayback;

        private readonly SelcalInput selcalInput;
        private TxTransceiverDto[] selcalTransmitters;

        private readonly List<Soundcard> soundcards;

        public bool BypassEffects { set { foreach(var soundcard in soundcards)soundcard.BypassEffects = value; } }
        public bool Started { get; private set; }
        public DateTime StartDateTimeUtc { get; private set; }
        public Soundcard[] Soundcards => soundcards.ToArray();

        public event EventHandler SelcalStopped { add { selcalInput.Stopped += value; } remove { selcalInput.Stopped -= value; } }

        private readonly EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler;

        public UserClient(string apiServer, EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler, bool enableSelcal = false) : base(apiServer)
        {
            this.eventHandler = eventHandler;
            soundcards = new List<Soundcard>();
            if(enableSelcal)
            {
                selcalInput = new SelcalInput(sampleRate);
                selcalInput.OpusDataAvailable += SelcalInput_OpusDataAvailable;
            }
            logger.Debug(nameof(UserClient) + " instantiated");
        }

        private void SelcalInput_OpusDataAvailable(object sender, SelcalInput.SelcalOpusDataAvailableEventArgs e)
        {
            if (selcalTransmitters == null || selcalTransmitters.Length == 0)
            {
                selcalInput.Stop();
                return;
            }

            if(Connection.IsConnected)
            {
                Connection.VoiceServerTransmitQueue.Add(new RadioTxDto()
                {
                    Callsign = Callsign,
                    SequenceCounter = e.SequenceCounter,
                    Audio = e.Audio,
                    LastPacket = e.LastData,
                    Transceivers = selcalTransmitters
                });
            }
        }

        private void Soundcard_RadioTxAvailable(object sender, SoundcardRadioTxAvailableEventArgs e)
        {
            e.RadioTx.Callsign = Callsign;
            if (Connection.IsConnected)
                Connection.VoiceServerTransmitQueue.Add(e.RadioTx);
        }

        /// <summary>
        /// Adds additional soundcards to allow input and output to multiple audio devices.
        /// Each soundcard may specify an input and output device, and the corresponding transceiver IDs to process audio for.
        /// </summary>
        /// <param name="inputAudioDeviceName">WASAPI Capture Device FriendlyName</param>
        /// <param name="outputAudioDeviceName">WASAPI Render Device FriendlyName</param>
        /// <param name="transceiverIDs">IDs of the transceivers to process</param>
        public void AddSoundcard(string inputAudioDeviceName, string outputAudioDeviceName, List<ushort> transceiverIDs)
        {
            if (Started)
                throw new Exception("Cannot add while client started");

            MMDevice output = ClientAudioUtilities.MapWasapiOutputDevice(outputAudioDeviceName);
            MMDevice input = ClientAudioUtilities.MapWasapiInputDevice(inputAudioDeviceName);

            AddSoundcard(input, output, transceiverIDs);
        }

        protected void AddSoundcard(MMDevice inputDevice, MMDevice outputDevice, List<ushort> transceiverIDs)
        {
            Soundcard s = new Soundcard(inputDevice, outputDevice, sampleRate, transceiverIDs, eventHandler);
            s.RadioTxAvailable += Soundcard_RadioTxAvailable;
            soundcards.Add(s);

            logger.Debug($"Added Soundcard [Input:{inputDevice?.FriendlyName} Output:{outputDevice?.FriendlyName}]");
        }

        public void RemoveSoundcards()
        {
            if (Started)
                throw new Exception("Cannot remove while client started");

            soundcards.Clear();
        }

        /// <summary>
        /// Starts the audio client with one Soundcard
        /// </summary>
        /// <param name="outputAudioDevice">WASAPI Render Device FriendlyName</param>
        /// <param name="transceiverIDs">IDs of the transceivers to process</param>
        /// <exception cref="Exception">Client already started</exception>
        public void Start(string outputAudioDeviceName, List<ushort> transceiverIDs)
        {
            MMDevice output = ClientAudioUtilities.MapWasapiOutputDevice(outputAudioDeviceName);
            AddSoundcard(null, output, transceiverIDs);
            Start();
        }

        /// <summary>
        /// Starts the audio client with one Soundcard
        /// </summary>
        /// <param name="inputAudioDevice">WASAPI Capture Device FriendlyName<</param>
        /// <param name="outputAudioDevice">WASAPI Render Device FriendlyName<</param>
        /// <param name="transceiverIDs">IDs of the transceivers to process</param>
        /// <exception cref="Exception">Client already started</exception>
        public void Start(string inputAudioDeviceName, string outputAudioDeviceName, List<ushort> transceiverIDs)
        {
            MMDevice output = ClientAudioUtilities.MapWasapiOutputDevice(outputAudioDeviceName);
            MMDevice input = ClientAudioUtilities.MapWasapiInputDevice(inputAudioDeviceName);
            AddSoundcard(input, output, transceiverIDs);
            Start();
        }

        /// <summary>
        /// Start audio client
        /// Must have already called AddSoundCard to use this method
        /// </summary>
        /// <exception cref="Exception">Client already started</exception>
        /// <exception cref="Exception">No soundcards defined</exception>
        public void Start()
        {
            if (Started)
                throw new Exception("Client already started");

            if (soundcards.Count == 0)
                throw new Exception("No soundcards defined");

            foreach (var soundcard in soundcards)
                soundcard.Start();

            playbackCancelTokenSource = new CancellationTokenSource();
            taskAudioPlayback = new Task(() => TaskAudioPlayback(logger, playbackCancelTokenSource.Token, Connection.VoiceServerReceiveQueue), TaskCreationOptions.LongRunning);
            taskAudioPlayback.Start();

            StartDateTimeUtc = DateTime.UtcNow;

            Connection.ReceiveAudio = true;
            Started = true;
            logger.Debug($"Started [Soundcards {soundcards.Count}]");
        }

        /// <summary>
        /// Stops the audio client
        /// </summary>
        public void Stop()
        {
            if (!Started)
                throw new Exception("Client not started");

            Started = false;
            Connection.ReceiveAudio = false;
            logger.Debug("Stopped");

            foreach (var soundcard in soundcards)
                soundcard.Stop();

            playbackCancelTokenSource.Cancel();
            taskAudioPlayback.Wait();
            taskAudioPlayback = null;

            while (Connection.VoiceServerReceiveQueue.TryTake(out _)) { }        //Clear the VoiceServerReceiveQueue.
        }

        public void ChangeSoundcardInputDevice(Soundcard soundcard, string inputAudioDeviceName)
        {
            MMDevice input = ClientAudioUtilities.MapWasapiInputDevice(inputAudioDeviceName);
            ChangeSoundcardInputDevice(soundcard, input);
        }
        protected void ChangeSoundcardInputDevice(Soundcard soundcard, MMDevice device)
        {
            soundcard.ChangeInputDevice(device);
        }

        public void ChangeSoundcardOutputDevice(Soundcard soundcard, string outputAudioDeviceName)
        {
            MMDevice output = ClientAudioUtilities.MapWasapiOutputDevice(outputAudioDeviceName);
            ChangeSoundcardOutputDevice(soundcard, output);
        }
        protected void ChangeSoundcardOutputDevice(Soundcard soundcard, MMDevice device)
        {
            soundcard.ChangeOutputDevice(device);
        }

        /// <summary>
        /// Sets Push to Talk state for all soundcards. To set individual push to talks, call Soundcard.PTT instead
        /// </summary>
        /// <param name="active">PTT down</param>
        public void PTT(bool active)
        {
            foreach(var soundcard in soundcards)
                soundcard.PTT(active);
        }

        /// <summary>
        /// Transmits a SELCAL code. SELCAL must be enabled when constructing UserClient
        /// </summary>
        /// <param name="code">Capitalized SELCAL code without hyphen eg "ABCD"</param>
        /// <exception cref="System.Exception">Client not started or misconfigured</exception>
        /// <exception cref="System.ArgumentException">Invalid code string</exception>
        public void TransmitSELCAL(string code, IList<TxTransceiverDto> transmitTransceivers)
        {
            if (!Started)
                throw new Exception("Client not started");

            if (selcalInput == null)
                throw new Exception("SELCAL was not enabled at constructor");

            selcalTransmitters = transmitTransceivers.ToArray();
            selcalInput.Play(code);
        }

        public override void UpdateTransceivers(List<TransceiverDto> transceivers)
        {
            base.UpdateTransceivers(transceivers);
            foreach (var soundcard in soundcards)
                soundcard.UpdateTransceivers(transceivers);
        }

        private void TaskAudioPlayback(Logger logger, CancellationToken cancelToken, BlockingCollection<IMsgPackTypeName> queue)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                if (queue.TryTake(out IMsgPackTypeName data, 500))
                {
                    switch (data.GetType().Name)
                    {
                        case nameof(RadioRxDto):
                            {
                                var dto = (RadioRxDto)data;
                                foreach (var soundcard in soundcards)
                                    soundcard.ProcessRadioRx(dto);

                                if (logger.IsTraceEnabled)
                                    logger.Trace(dto.ToDebugString());
                                break;
                            }
                    }
                }
            }
        }
    }
}
