using NAudio.Wave;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GeoVR.Shared;
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
        private ushort[] selcalMutedSoundcardIds;

        private readonly List<Soundcard> soundcards;

        /// <summary>
        /// Sets the bypassing of radio effects on each current soundcard
        /// </summary>
        public bool BypassEffects { set { foreach(var soundcard in soundcards)soundcard.BypassEffects = value; } }
        /// <summary>
        /// Audio processing started
        /// </summary>
        public bool Started { get; private set; }
        /// <summary>
        /// Time audio processing started
        /// </summary>
        public DateTime StartDateTimeUtc { get; private set; }
        /// <summary>
        /// Array of Soundcards used for Audio IO
        /// </summary>
        public Soundcard[] Soundcards => soundcards.ToArray();
        /// <summary>
        /// SELCAL finished transmitting
        /// </summary>
        public event EventHandler SelcalStopped { add { selcalInput.Stopped += value; } remove { selcalInput.Stopped -= value; } }
        public event EventHandler<SoundcardStoppedEventArgs> SoundcardStopped;

        private readonly EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiServer"></param>
        /// <param name="eventHandler">EventHandler for receiving callsign changed events</param>
        /// <param name="enableSelcal">True if client wishes to transmit selcal</param>
        public UserClient(string apiServer, EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler, bool enableSelcal = false) : base(apiServer)
        {
            this.eventHandler = eventHandler;
            soundcards = new List<Soundcard>();
            if(enableSelcal)
            {
                selcalInput = new SelcalInput(sampleRate);
                selcalInput.OpusDataAvailable += SelcalInput_OpusDataAvailable;
                selcalInput.Stopped += SelcalInput_Stopped;
            }
            logger.Debug(nameof(UserClient) + " instantiated");
        }

        private void SelcalInput_Stopped(object sender, EventArgs e)
        {
            if (selcalMutedSoundcardIds == null)
                return;
            foreach(var id in selcalMutedSoundcardIds)
            {
                var sc = soundcards.FirstOrDefault(s => s.ID == id);
                sc?.Mute(false);
            }
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
        /// <exception cref="Exception">Client started</exception>
        public void AddSoundcard(string inputAudioDeviceName, string outputAudioDeviceName, List<ushort> transceiverIDs)
        {
            if (Started)
                throw new Exception("Cannot add while client started");

            var output = ClientAudioUtilities.MapOutputDevice(outputAudioDeviceName);
            var input = ClientAudioUtilities.MapInputDevice(inputAudioDeviceName);

            AddSoundcard(input, output, transceiverIDs);
        }

        public void AddSoundcard()
        {
            if (Started)
                throw new Exception("Cannot add while client started");

            AddSoundcard((int?)null, (int?)null, Array.Empty<ushort>().ToList());
        }

        protected void AddSoundcard(int? inputDevice, int? outputDevice, List<ushort> transceiverIDs)
        {
            Soundcard s = new Soundcard(inputDevice, outputDevice, sampleRate, transceiverIDs, eventHandler);
            s.RadioTxAvailable += Soundcard_RadioTxAvailable;
            s.Stopped += Soundcard_OutputStopped;
            soundcards.Add(s);

            logger.Debug($"Added Soundcard [Input:{inputDevice} Output:{outputDevice}]");
        }

        private void Soundcard_OutputStopped(object sender, EventArgs e)
        {
            SoundcardStopped?.Invoke(this, new SoundcardStoppedEventArgs(soundcards.IndexOf((Soundcard)sender), e));
        }

        /// <summary>
        /// Removes all soundcards
        /// </summary>
        /// <exception cref="Exception">Client started</exception>
        public void RemoveSoundcards()
        {
            if (Started)
                throw new Exception("Cannot remove while client started");

            foreach (var s in soundcards)
            {
                s.Stopped -= Soundcard_OutputStopped;
                s.RadioTxAvailable -= Soundcard_RadioTxAvailable;
            }
            soundcards.Clear();
        }

        /// <summary>
        /// Starts the audio client with one Soundcard
        /// </summary>
        /// <param name="outputAudioDeviceName">WASAPI Render Device FriendlyName</param>
        /// <param name="transceiverIDs">IDs of the transceivers to process</param>
        /// <exception cref="Exception">Client already started</exception>
        public void Start(string outputAudioDeviceName, List<ushort> transceiverIDs)
        {
            if (Started)
                throw new Exception("Client already started");

            soundcards.Clear();

            var output = ClientAudioUtilities.MapOutputDevice(outputAudioDeviceName);
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
            if (Started)
                throw new Exception("Client already started");

            soundcards.Clear();

            var output = ClientAudioUtilities.MapOutputDevice(outputAudioDeviceName);
            var input = ClientAudioUtilities.MapInputDevice(inputAudioDeviceName);
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

            playbackCancelTokenSource.Cancel();
            taskAudioPlayback.Wait();
            taskAudioPlayback = null;

            foreach (var soundcard in soundcards)
                soundcard.Stop();

            while (Connection.VoiceServerReceiveQueue.TryTake(out _)) { }        //Clear the VoiceServerReceiveQueue.
        }

        /// <summary>
        /// Change the input device used by a soundcard
        /// </summary>
        /// <param name="soundcard"></param>
        /// <param name="inputAudioDeviceName">WASAPI Capture FriendlyName</param>
        public void ChangeSoundcardInputDevice(Soundcard soundcard, string inputAudioDeviceName)
        {
            var input = ClientAudioUtilities.MapInputDevice(inputAudioDeviceName);
            ChangeSoundcardInputDevice(soundcard, input);
        }
        protected void ChangeSoundcardInputDevice(Soundcard soundcard, int? device)
        {
            soundcard.ChangeInputDevice(device);
        }
        /// <summary>
        /// Change the output device used by a soundcard
        /// </summary>
        /// <param name="soundcard"></param>
        /// <param name="outputAudioDeviceName">WASAPI Render FriendlyName</param>
        public void ChangeSoundcardOutputDevice(Soundcard soundcard, string outputAudioDeviceName)
        {
            var output = ClientAudioUtilities.MapOutputDevice(outputAudioDeviceName);
            ChangeSoundcardOutputDevice(soundcard, output);
        }
        protected void ChangeSoundcardOutputDevice(Soundcard soundcard, int? device)
        {
            soundcard.ChangeOutputDevice(device);
        }

        /// <summary>
        /// Sets Push to Talk state for first soundcard
        /// </summary>
        /// <param name="active">PTT down</param>
        public void PTT(bool active)
        {
            PTT(soundcards[0], active);
        }

        /// <summary>
        /// Sets Push to Talk state for nominated Soundcard. 
        /// If another soundcard is currently transmitting on a shared transceiver, PTT will be blocked
        /// </summary>
        /// <param name="soundcard"></param>
        /// <param name="active">PTT down</param>
        public void PTT(Soundcard soundcard, bool active)
        {
            var reqIds = soundcard.TransmittingTransceivers.Select(t => t.ID);
            if (!active || !soundcards.Any(s => s != soundcard && s.Transmitting && s.TransmittingTransceivers.Select(t => t.ID).Intersect(reqIds).Any()))
            {
                soundcard.PTT(active);
            }
            else
                logger.Debug("PTT blocked for soundcard " + soundcard.ID);
        }

        /// <summary>
        /// Transmits a SELCAL code. SELCAL must be enabled when constructing UserClient
        /// </summary>
        /// <param name="code">Capitalized SELCAL code without hyphen eg "ABCD"</param>
        /// <param name="transmitTransceiversIDs">Transceivers to transmit SELCAL on</param>
        /// <exception cref="System.Exception">Client not started or misconfigured</exception>
        /// <exception cref="System.ArgumentException">Invalid code string</exception>
        public void TransmitSELCAL(string code, IList<ushort> transmitTransceiversIDs)
        {
            if (!Started)
                throw new Exception("Client not started");

            if (selcalInput == null)
                throw new Exception("SELCAL was not enabled at constructor");

            selcalTransmitters = Array.ConvertAll(transmitTransceiversIDs.ToArray(), id => new TxTransceiverDto() { ID = id });
            selcalInput.Play(code);
            var scIds = new List<ushort>();
            foreach(var soundcard in soundcards.Where(s=>s.TransceiverIds.Any(id=> transmitTransceiversIDs.Contains(id))))
            {
                soundcard.Mute(true);
                scIds.Add(soundcard.ID);
            }
            selcalMutedSoundcardIds = scIds.ToArray();
        }

        /// <summary>
        /// Updates transceivers that this client is receiving from the server
        /// </summary>
        /// <param name="transceivers"></param>
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
