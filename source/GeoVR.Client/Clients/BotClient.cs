using GeoVR.Opus;
using GeoVR.Shared;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Timers;

namespace GeoVR.Client
{
    public class BotClient : BaseClient, IClient
    {
        private System.Timers.Timer audioTimer = null;
        private string file;
        private OpusEncoder encoder;

        public bool Started { get; private set; }
        public DateTime StartDateTimeUtc { get; private set; }

        public BotClient(string apiServer) : base(apiServer)                //Connection.ReceiveAudio = false in the base constructor
        {
            encoder = OpusEncoder.Create(sampleRate, 1, Application.Voip);
            encoder.Bitrate = 16 * 1024;
            Connection.ReceiveAudio = false;
        }

        public void Start(string file, double interval)
        {
            if (Started)
                throw new Exception("Client already started");

            this.file = file;

            if (audioTimer == null)
            {
                audioTimer = new System.Timers.Timer();
                audioTimer.Interval = interval;
                audioTimer.Elapsed += audioTimer_Elapsed;
            }
            audioTimer.Start();

            StartDateTimeUtc = DateTime.UtcNow;
            Started = true;
        }

        public void Stop()
        {
            if (!Started)
                throw new Exception("Client not started");

            Started = false;

            audioTimer.Stop();

            UpdateTransceivers(new List<TransceiverDto>());          
        }

        byte[] encodedDataBuffer = new byte[1275];
        private uint audioSequenceCounter = 0;
        short[] lastPacketBuffer = new short[frameSize];
        private void audioTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            List<TxTransceiverDto> transceivers = new List<TxTransceiverDto> { new TxTransceiverDto() { ID = 0 } };

            //Play a sample every 10 seconds.
            //audioPublishInputQueue.Add(trimmedBuff);
            var reader = new WaveFileReader(file);
            var bytes = (int)reader.SampleCount * 2;
            bytes = bytes - (bytes % reader.BlockAlign);
            byte[] buffer = new byte[bytes];
            reader.Read(buffer, 0, bytes);

            var waveBuffer = ClientAudioUtilities.ConvertBytesTo16BitPCM(buffer);

            int segmentCount = (int)System.Math.Floor((decimal)waveBuffer.Length / frameSize);
            int bufferOffset = 0;
            for (int i = 0; i < segmentCount; i++)
            {
                //int len = encoder.Encode(waveBuffer, bufferOffset, frameSize, encodedDataBuffer, 0, encodedDataBuffer.Length);
                encodedDataBuffer = encoder.Encode(buffer, 1275, out int encodedDataLength);
                bufferOffset += frameSize;

                byte[] trimmedBuff = new byte[encodedDataLength];
                Buffer.BlockCopy(encodedDataBuffer, 0, trimmedBuff, 0, encodedDataLength);

                if (Connection.IsConnected)
                {
                    Connection.VoiceServerTransmitQueue.Add(new RadioTxDto()
                    {
                        Callsign = Callsign,
                        SequenceCounter = audioSequenceCounter++,
                        Audio = trimmedBuff,
                        LastPacket = false,
                        Transceivers = transceivers.ToArray()
                    });
                }

                Thread.Sleep(17);
            }
            Array.Clear(lastPacketBuffer, 0, frameSize);
            int remainderSamples = waveBuffer.Length - (segmentCount * frameSize);
            Buffer.BlockCopy(waveBuffer, bufferOffset, lastPacketBuffer, 0, remainderSamples);
            //int lenRemainder = encoder.Encode(lastPacketBuffer, 0, frameSize, encodedDataBuffer, 0, encodedDataBuffer.Length);
            encodedDataBuffer = encoder.Encode(buffer, 1275, out int lenRemainder);
            byte[] trimmedBuffRemainder = new byte[lenRemainder];
            Buffer.BlockCopy(encodedDataBuffer, 0, trimmedBuffRemainder, 0, lenRemainder);

            Connection.VoiceServerTransmitQueue.Add(new RadioTxDto()
            {
                Callsign = Callsign,
                SequenceCounter = audioSequenceCounter++,
                Audio = trimmedBuffRemainder,
                LastPacket = true,
                Transceivers = transceivers.ToArray()
            });
        }

        //private void TaskVoiceServerReceive(CancellationToken cancelToken, BlockingCollection<IMsgPack> queue)      //Just to empty the queue
        //{
        //    while (!cancelToken.IsCancellationRequested)
        //    {
        //        if (queue.TryTake(out IMsgPack data, 500))
        //        {
        //        }
        //    }
        //}

        //private void TaskClientDataPub(CancellationToken cancelToken, BlockingCollection<object> queue, string address)
        //{
        //    using (var pubSocket = new PublisherSocket())
        //    {
        //        //pubSocket.Options.SendHighWatermark = 1;
        //        pubSocket.Connect(address);

        //        while (!cancelToken.IsCancellationRequested)
        //        {
        //            if (queue.TryTake(out object data, 500))
        //            {
        //                switch (data.GetType().Name)
        //                {
        //                    case nameof(ClientAdd):
        //                        pubSocket.Serialise<ClientAdd>(data);
        //                        break;
        //                    case nameof(ClientHeartbeat):
        //                        pubSocket.Serialise<ClientHeartbeat>(data);
        //                        break;
        //                    case nameof(ClientRadioTranceiversUpdate):
        //                        pubSocket.Serialise<ClientRadioTranceiversUpdate>(data);
        //                        break;
        //                }
        //            }
        //        }
        //    }
        //    taskClientDataPub = null;
        //}

        //private void TaskClientDataSub(CancellationToken cancelToken, string address)
        //{
        //    using (var subSocket = new SubscriberSocket())
        //    {
        //        subSocket.Options.ReceiveHighWatermark = 50;
        //        subSocket.Connect(address);
        //        subSocket.Subscribe("");

        //        while (!cancelToken.IsCancellationRequested)
        //        {
        //            var messageTopicReceived = subSocket.ReceiveFrameString();
        //            switch (messageTopicReceived)
        //            {
        //                case nameof(ServerStatus):
        //                    ServerStatus serverStatus = subSocket.Deserialise<ServerStatus>(out long bytesReceived);
        //                    ClientStatistics.DataBytesReceived += bytesReceived;
        //                    ClientStatistics.DataPayloadsReceived++;
        //                    if (lastServerStatus != null)
        //                        if (serverStatus.StartDateTime > lastServerStatus.StartDateTime)
        //                            dataPublishInputQueue.Add(new ClientAdd() { Callsign = ClientData.Callsign });
        //                    lastServerStatus = serverStatus;
        //                    break;
        //                default:
        //                    var messageReceived = subSocket.ReceiveFrameBytes();
        //                    break;
        //            }
        //        }
        //    }
        //}
    }
}
