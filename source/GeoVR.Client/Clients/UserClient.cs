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

        private Input input;
        private Output output;
        private SelcalInput selcalInput;

        private SoundcardSampleProvider soundcardSampleProvider;
        private VolumeSampleProvider outputSampleProvider;

        public bool BypassEffects { set { if (soundcardSampleProvider != null) soundcardSampleProvider.BypassEffects = value; } }

        private bool transmit;
        private bool transmitHistory = false;
        private TxTransceiverDto[] transmittingTransceivers;

        public bool Started { get; private set; }
        public DateTime StartDateTimeUtc { get; private set; }

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
                if (outputSampleProvider != null)
                    outputSampleProvider.Volume = outputVolume;
            }
        }

        private double maxDbReadingInPTTInterval = -100;

        private CallRequestDto incomingCallRequest = null;

        public event EventHandler<InputVolumeStreamEventArgs> InputVolumeStream
        {
            add { input.InputVolumeStream += value; }
            remove { input.InputVolumeStream -= value; }
        }

        //public event EventHandler<VolumeStreamEventArgs> OutputVolumeStream
        //{

        //}

        public event EventHandler<CallRequestEventArgs> CallRequest;
        public event EventHandler<CallResponseEventArgs> CallResponse;

        private EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler;

        public UserClient(string apiServer, EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler, bool enableSelcal = false) : base(apiServer)
        {
            this.eventHandler = eventHandler;
            input = new Input(sampleRate);
            input.OpusDataAvailable += Input_OpusDataAvailable;
            input.InputVolumeStream += Input_InputVolumeStream;
            if(enableSelcal)
            {
                selcalInput = new SelcalInput(sampleRate);
                selcalInput.OpusDataAvailable += SelcalInput_OpusDataAvailable;
            }
            output = new Output();
            logger.Debug(nameof(UserClient) + " instantiated");
        }

        private void SelcalInput_OpusDataAvailable(object sender, SelcalInput.SelcalOpusDataAvailableEventArgs e)
        {
            if (transmittingTransceivers == null || transmittingTransceivers.Length == 0 || transmit)//PTT was pressed, stop sending.
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
                    Transceivers = transmittingTransceivers
                });
            }
        }

        private void Input_InputVolumeStream(object sender, InputVolumeStreamEventArgs e)
        {
            if (transmit)
            {
                //Gather AGC stats
                if (e.PeakDB > maxDbReadingInPTTInterval)
                    maxDbReadingInPTTInterval = e.PeakDB;
            }
        }

        private void Input_OpusDataAvailable(object sender, OpusDataAvailableEventArgs e)
        {
            if (transmittingTransceivers == null)
            {
                return;
            }

            if (transmittingTransceivers.Length > 0)
            {
                if (transmit)
                {
                    if (Connection.IsConnected)
                    {
                        Connection.VoiceServerTransmitQueue.Add(new RadioTxDto()
                        {
                            Callsign = Callsign,
                            SequenceCounter = e.SequenceCounter,
                            Audio = e.Audio,
                            LastPacket = false,
                            Transceivers = transmittingTransceivers
                        });

                        //Debug.WriteLine("Sending Audio:" + e.SequenceCounter);
                    }
                }
                if (!transmit && transmitHistory)
                {
                    if (Connection.IsConnected)
                    {
                        Connection.VoiceServerTransmitQueue.Add(new RadioTxDto()
                        {
                            Callsign = Callsign,
                            SequenceCounter = e.SequenceCounter,
                            Audio = e.Audio,
                            LastPacket = true,
                            Transceivers = transmittingTransceivers
                        });
                    }
                }
                transmitHistory = transmit;
            }
        }

        public void Start(string outputAudioDevice, List<ushort> transceiverIDs)
        {
            MMDevice output = ClientAudioUtilities.MapWasapiOutputDevice(outputAudioDevice);
            Start(null, output, transceiverIDs);
        }

        public void Start(string inputAudioDevice, string outputAudioDevice, List<ushort> transceiverIDs)
        {
            MMDevice output = ClientAudioUtilities.MapWasapiOutputDevice(outputAudioDevice);
            MMDevice input = ClientAudioUtilities.MapWasapiInputDevice(inputAudioDevice);
            Start(input, output, transceiverIDs);
        }

        public void Start(MMDevice inputAudioDevice, MMDevice outputAudioDevice, List<ushort> transceiverIDs)
        {
            if (Started)
                throw new Exception("Client already started");

            soundcardSampleProvider = new SoundcardSampleProvider(sampleRate, transceiverIDs, eventHandler);
            outputSampleProvider = new VolumeSampleProvider(soundcardSampleProvider);
            outputSampleProvider.Volume = outputVolume;

            output.Start(outputAudioDevice, outputSampleProvider);

            playbackCancelTokenSource = new CancellationTokenSource();
            taskAudioPlayback = new Task(() => TaskAudioPlayback(logger, playbackCancelTokenSource.Token, Connection.VoiceServerReceiveQueue), TaskCreationOptions.LongRunning);
            taskAudioPlayback.Start();

            if(inputAudioDevice != null)
                input.Start(inputAudioDevice.FriendlyName);

            StartDateTimeUtc = DateTime.UtcNow;

            Connection.ReceiveAudio = true;
            Started = true;
            logger.Debug("Started [Input: " + inputAudioDevice + "] [Output: " + outputAudioDevice + "]");
        }

        public void ChangeInputDevice(MMDevice device)
        {
            if (!Started)
                throw new Exception("Client must be started first");

            if (input.Started)
                input.Stop();
            input.Start(device.FriendlyName);
        }

        public void ChangeOutputDevice(MMDevice device)
        {
            if (!Started)
                throw new Exception("Client must be started first");

            if (output.Started)
                output.Stop();
            output.Start(device, outputSampleProvider);
        }

        public void Stop()
        {
            if (!Started)
                throw new Exception("Client not started");

            Started = false;
            Connection.ReceiveAudio = false;
            logger.Debug("Stopped");

            if(input.Started)
                input.Stop();

            playbackCancelTokenSource.Cancel();
            taskAudioPlayback.Wait();
            taskAudioPlayback = null;

            output.Stop();

            while (Connection.VoiceServerReceiveQueue.TryTake(out _)) { }        //Clear the VoiceServerReceiveQueue.
        }

        public void TransmittingTransceivers(ushort transceiverID)
        {
            TransmittingTransceivers(new TxTransceiverDto[] { new TxTransceiverDto() { ID = transceiverID } });
        }

        public void TransmittingTransceivers(TxTransceiverDto[] transceivers)
        {
            transmittingTransceivers = transceivers;
        }

        public void PTT(bool active)
        {
            if (!Started)
                throw new Exception("Client not started");

            if (transmit == active)     //Ignore repeated keyboard events
                return;

            transmit = active;
            soundcardSampleProvider.PTT(active, transmittingTransceivers);

            if (!active)
            {
                //AGC
                //if (maxDbReadingInPTTInterval > -1)
                //    InputVolumeDb = InputVolumeDb - 1;
                //if(maxDbReadingInPTTInterval < -4)
                //    InputVolumeDb = InputVolumeDb + 1;
                maxDbReadingInPTTInterval = -100;
            }

            logger.Debug("PTT: " + active.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="code">SELCAL tone without hyphen eg "ABCD"</param>
        public void TransmitSELCAL(string code)
        {
            if (!Started)
                throw new Exception("Client not started");

            if (selcalInput == null)
                throw new Exception("SELCAL was not enabled at constructor");

            if (transmit)
                return;//dont try and send while PTT is down

            selcalInput.Play(code);
        }

        public void RequestCall(string callsign)
        {
            if (!Started)
                throw new Exception("Client not started");

            Connection.VoiceServerTransmitQueue.Add(new CallRequestDto() { FromCallsign = Callsign, ToCallsign = callsign });
            //logger.Debug("LandLineRing: " + active.ToString());
        }

        public void RejectCall()
        {
            if (incomingCallRequest != null)
            {
                soundcardSampleProvider.LandLineRing(false);
                Connection.VoiceServerTransmitQueue.Add(new CallResponseDto() { Request = incomingCallRequest, Event = CallResponseEvent.Reject });
                incomingCallRequest = null;
            }
        }

        public void AcceptCall()
        {
            if (incomingCallRequest != null)
            {
                soundcardSampleProvider.LandLineRing(false);
                Connection.VoiceServerTransmitQueue.Add(new CallResponseDto() { Request = incomingCallRequest, Event = CallResponseEvent.Accept });
                incomingCallRequest = null;
            }
        }

        public override void UpdateTransceivers(List<TransceiverDto> transceivers)
        {
            base.UpdateTransceivers(transceivers);
            if (soundcardSampleProvider != null)
                soundcardSampleProvider.UpdateTransceivers(transceivers);
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
                                soundcardSampleProvider.AddOpusSamples(dto, dto.Transceivers);
                                if (logger.IsTraceEnabled)
                                    logger.Trace(dto.ToDebugString());
                                break;
                            }
                        case nameof(CallRequestDto):
                            {
                                var dto = (CallRequestDto)data;
                                if (incomingCallRequest != null)
                                {
                                    Connection.VoiceServerTransmitQueue.Add(new CallResponseDto() { Request = dto, Event = CallResponseEvent.Busy });
                                }
                                else
                                {
                                    incomingCallRequest = dto;
                                    soundcardSampleProvider.LandLineRing(true);
                                    CallRequest?.Invoke(this, new CallRequestEventArgs() { Callsign = dto.FromCallsign });
                                }
                                break;
                            }
                        case nameof(CallResponseDto):
                            {
                                var dto = (CallResponseDto)data;
                                switch (dto.Event)
                                {
                                    // Server events
                                    case CallResponseEvent.Routed:
                                        soundcardSampleProvider.LandLineRing(true);
                                        CallResponse?.Invoke(this, new CallResponseEventArgs() { Callsign = dto.Request.ToCallsign, Event = CallResponseEvent.Routed });        //Our request was routed
                                        break;
                                    case CallResponseEvent.NoRoute:
                                        CallResponse?.Invoke(this, new CallResponseEventArgs() { Callsign = dto.Request.ToCallsign, Event = CallResponseEvent.NoRoute });       //Our request was not routed
                                        break;
                                    //Remote party events
                                    case CallResponseEvent.Busy:
                                        soundcardSampleProvider.LandLineRing(false);
                                        //Play busy tone
                                        CallResponse?.Invoke(this, new CallResponseEventArgs() { Callsign = dto.Request.ToCallsign, Event = CallResponseEvent.Busy });
                                        break;
                                    case CallResponseEvent.Accept:
                                        soundcardSampleProvider.LandLineRing(false);
                                        CallResponse?.Invoke(this, new CallResponseEventArgs() { Callsign = dto.Request.ToCallsign, Event = CallResponseEvent.Accept });
                                        break;
                                    case CallResponseEvent.Reject:
                                        soundcardSampleProvider.LandLineRing(false);
                                        //Play reject tone
                                        CallResponse?.Invoke(this, new CallResponseEventArgs() { Callsign = dto.Request.ToCallsign, Event = CallResponseEvent.Reject });
                                        break;

                                }
                                break;
                            }

                    }
                }
            }
        }
    }
}
