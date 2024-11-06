using GeoVR.Shared;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;

namespace GeoVR.Client
{
    public class SoundcardSampleProvider : ISampleProvider
    {
        public WaveFormat WaveFormat { get; private set; }
        public bool BypassEffects
        {
            get { return bypassEffects; }
            set
            {
                bypassEffects = value;
                foreach (var receiverInput in receiverInputs.Values)
                {
                    receiverInput.BypassEffects = value;
                }
            }
        }

        private readonly MixingSampleProvider mixer;
        private readonly ConcurrentDictionary<ushort, ReceiverSampleProvider> receiverInputs;

        private readonly EventHandler<TransceiverReceivingCallsignsChangedEventArgs> callsignsEventHandler;

        private bool bypassEffects = false;

        public SoundcardSampleProvider(int sampleRate, List<ushort> receiverIDs, EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler)
        {
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 1);

            mixer = new MixingSampleProvider(WaveFormat)
            {
                ReadFully = true
            };
            callsignsEventHandler = eventHandler;
            receiverInputs = new ConcurrentDictionary<ushort, ReceiverSampleProvider>();

            foreach (var receiverID in receiverIDs)
            {
                var receiverInput = new ReceiverSampleProvider(WaveFormat, receiverID, 4)
                {
                    BypassEffects = bypassEffects
                };
                receiverInput.ReceivingCallsignsChanged += eventHandler;
                if (!receiverInputs.TryAdd(receiverInput.ID, receiverInput))
                    throw new Exception("Receiver ID must be unique");
                mixer.AddMixerInput(receiverInput);
            }
        }

        public void PTT(bool active, TxTransceiverDto[] txTransceivers)
        {
            if (active)
            {
                if (txTransceivers != null && txTransceivers.Length > 0)
                {
                    foreach (var txTransceiver in txTransceivers)
                    {
                        if (receiverInputs.TryGetValue(txTransceiver.ID, out var rc))
                            rc.SetMute(ptt: true);
                    }
                }
            }
            else
            {
                foreach (var receiverInput in receiverInputs.Values)
                {
                    receiverInput.SetMute(ptt: false);
                }
            }
        }

        public void SetReceiverMute(bool mute, ushort id)
        {
            if (receiverInputs.TryGetValue(id, out var recv))
                recv.SetMute(rx: mute);
        }

        public void AddMixerInput(ISampleProvider sampleProvider)
        {
            mixer.AddMixerInput(sampleProvider);
        }

        public void RemoveMixerInput(ISampleProvider sampleProvider)
        {
            mixer.RemoveMixerInput(sampleProvider);
        }

        public int Read(float[] buffer, int offset, int count)
        {
            return mixer.Read(buffer, offset, count);
        }

        public void AddOpusSamples(IAudioDto audioDto, List<RxTransceiverDto> rxTransceivers)
        {
            var rxTransceiversSorted = rxTransceivers.OrderByDescending(x => x.DistanceRatio);

            // The issue in a nutshell - you can have multiple transmitters so the audio DTO contains multiple transceivers with the same ID or
            // you can have multiple receivers so the audio DTO contains multiple transceivers with different IDs, but you only want to play the audio once in either scenario.

            if (rxTransceiversSorted.Any())
            {
                bool audioPlayed = false;
                List<ushort> handledTransceiverIDs = new List<ushort>();
                foreach(var rxTransceiver in rxTransceiversSorted)
                {
                    if (!handledTransceiverIDs.Contains(rxTransceiver.ID) && receiverInputs.TryGetValue(rxTransceiver.ID, out var receiverInput))
                    {
                        handledTransceiverIDs.Add(rxTransceiver.ID);

                        if (receiverInput.Mute)
                            continue;
                        if (!audioPlayed)
                        {
                            receiverInput.AddOpusSamples(audioDto, rxTransceiver.Frequency, rxTransceiver.DistanceRatio);
                            audioPlayed = true;
                        }
                        else
                        {
                            receiverInput.AddSilentSamples(audioDto, rxTransceiver.Frequency, rxTransceiver.DistanceRatio);
                        }
                    }
                }
            }
        }

        public void UpdateReceiverInputs(List<ushort> transIds)
        {
            var inputsToRemove = receiverInputs.Keys.Except(transIds);
            
            foreach(var id in inputsToRemove)
            {
                if (receiverInputs.TryRemove(id, out var rcv))
                    mixer.RemoveMixerInput(rcv);
            }

            var inputsToAdd = transIds.Except(receiverInputs.Keys);
            foreach (var id in inputsToAdd)
            {
                var receiverInput = new ReceiverSampleProvider(WaveFormat, id, 4)
                {
                    BypassEffects = bypassEffects
                };
                receiverInput.ReceivingCallsignsChanged += callsignsEventHandler;
                if (receiverInputs.TryAdd(id, receiverInput))
                    mixer.AddMixerInput(receiverInput);
            }
        }

        public void SetReceiverVolume(ushort transId, float volume)
        {
            if(receiverInputs.TryGetValue(transId, out var recv))
                recv.Volume = volume;
        }

        public void UpdateTransceivers(List<TransceiverDto> transceivers)
        {
            foreach (var transceiver in transceivers)
            {
                if (receiverInputs.TryGetValue(transceiver.ID, out var rcv))
                    rcv.Frequency = transceiver.Frequency;
            }
            foreach (var transceiverID in receiverInputs.Keys.Except(transceivers.Select(t=>t.ID)))
            {
                if (receiverInputs.TryGetValue(transceiverID, out var rcv))
                    rcv.Frequency = 0;
            }
        }
    }
}
