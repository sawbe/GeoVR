using GeoVR.Shared;
using MessagePack.CryptoDto;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace GeoVR.Connection
{
    public class ClientConnection
    {
        Logger logger = NLog.LogManager.GetCurrentClassLogger();

        //Data
        private readonly ClientConnectionData connection;

        //Voice server
        private UdpClient udpClient;
        private CancellationTokenSource voiceServerCancelTokenSource;
        public BlockingCollection<IMsgPackTypeName> VoiceServerTransmitQueue { get; private set; }
        public BlockingCollection<IMsgPackTypeName> VoiceServerReceiveQueue { get; private set; }

        //Connection checking
        private CancellationTokenSource connectionCheckCancelTokenSource;
        private Task taskServerConnectionCheck;
        private ManualResetEventSlim requestDisconnectEvent;

        //Properties
        //public bool Authenticated { get { return clientConnectionData.ApiServerConnection.Authenticated; } }
        public bool IsConnected { get { return connection.IsConnected; } }
        public bool ReceiveAudio { get { return connection.ReceiveAudio; } set { connection.ReceiveAudio = value; } }

        public ApiServerConnection ApiServerConnection { get { return connection.ApiServerConnection; } }

        public event EventHandler<ConnectedEventArgs> Connected;
        public event EventHandler<DisconnectedEventArgs> Disconnected;

        public ClientConnection(string apiServer)
        {
            connection = new ClientConnectionData();
            connection.ApiServerConnection = new ApiServerConnection(apiServer);
            connection.ReceiveAudio = true;
            connection.Callsign = null;
            //client = new RestClient(apiServer);
            VoiceServerTransmitQueue = new BlockingCollection<IMsgPackTypeName>();
            VoiceServerReceiveQueue = new BlockingCollection<IMsgPackTypeName>();
            //DataServerTransmitQueue = new BlockingCollection<IMsgPack>();
            //DataServerReceiveQueue = new BlockingCollection<IMsgPack>();
            requestDisconnectEvent = new ManualResetEventSlim(false);
            logger.Debug(nameof(ClientConnection) + " instantiated");
        }

        public async Task Connect(string username, string password, string callsign, string client)     //Client is something like "vPilot 2.2.3"
        {
            if (connection.IsConnected)
                throw new Exception("Client already connected");

            connection.Username = username;
            connection.Callsign = callsign;
            connection.DisconnectRequested = false;
            connection.DisconnectRequestedReason = string.Empty;
            await connection.ApiServerConnection.Connect(username, password, client);

            await GetVoiceCredentials(callsign);
            await Task.Run(() => ConnectToVoiceServer());

            if (taskServerConnectionCheck != null && taskServerConnectionCheck.Status == TaskStatus.Running)        //Leftover from previous connection session.
            {
                connectionCheckCancelTokenSource.Cancel();
                taskServerConnectionCheck.Wait();
            }

            connectionCheckCancelTokenSource = new CancellationTokenSource();
            taskServerConnectionCheck = new Task(() => TaskServerConnectionCheck(logger, connectionCheckCancelTokenSource.Token, requestDisconnectEvent, connection, InternalDisconnect), TaskCreationOptions.LongRunning);
            taskServerConnectionCheck.Start();

            connection.IsConnected = true;
            Connected?.Invoke(this, new ConnectedEventArgs());
            logger.Debug("Connected: " + callsign);
        }

        public async Task Disconnect(string reason)       //End-user initiated disconnect
        {
            connection.DisconnectRequestedReason = reason;
            connection.DisconnectRequested = true;
            requestDisconnectEvent.Set();
            if (taskServerConnectionCheck != null)
                await taskServerConnectionCheck;//Disco is handled by task
        }

        private async Task DoDisconnect(string reason, bool autoreconnect)
        {
            if (!connection.IsConnected)
                throw new Exception("Client not connected");

            connection.IsConnected = false;
            connection.DisconnectRequested = false;
            connection.DisconnectRequestedReason = string.Empty;
            requestDisconnectEvent.Reset();

            if (!string.IsNullOrWhiteSpace(connection.Callsign))
            {
                try     //Ignore exceptions from this API method, it's purely a best effort
                {
                    await connection.ApiServerConnection.RemoveCallsign(connection.Callsign);
                }
                catch { }
            }

            DisconnectFromVoiceServer();
            if (!autoreconnect)
                connection.ApiServerConnection.ForceDisconnect();   //Discard the JWT
            connection.Tokens = null;

            Disconnected?.Invoke(this, new DisconnectedEventArgs() { Reason = reason, AutoReconnect = autoreconnect });
            logger.Debug("Disconnected: " + reason);
        }

        private async Task InternalDisconnect(DisconnectReasons reason)
        {
            string disconnectReasonString;
            switch (reason)
            {
                case DisconnectReasons.LostConnection:
                    disconnectReasonString = "Lost server connection";
                    break;
                case DisconnectReasons.InternalLibraryError10:
                    disconnectReasonString = "Internal library error 10";
                    break;
                case DisconnectReasons.InternalLibraryError20:
                    disconnectReasonString = "Internal library error 20";
                    break;
                case DisconnectReasons.InternalLibraryError30:
                    disconnectReasonString = "Internal library error 30.";
                    break;
                case DisconnectReasons.Requested:
                    disconnectReasonString = connection.DisconnectRequestedReason;
                    break;
                default:
                    disconnectReasonString = "Unknown error";
                    break;
            }
            bool autoreconnect = reason == DisconnectReasons.LostConnection;

            await DoDisconnect(disconnectReasonString, autoreconnect);

            if (autoreconnect)
                Reconnect();
        }

        private async void Reconnect()
        {
            for (int i = 1; i <= 3; i++)
            {
                logger.Info($"Reconnection attempt {i} ({connection.Callsign})");

                try
                {
                    logger.Debug("Waiting for " + (i * i * i) + " seconds");
                    await Task.Delay(i * i * i * 1000);       //1 second, 8 seconds, 27 seconds.

                    await GetVoiceCredentials(connection.Callsign);
                    await Task.Run(() => ConnectToVoiceServer());

                    connectionCheckCancelTokenSource = new CancellationTokenSource();
                    taskServerConnectionCheck = new Task(() => TaskServerConnectionCheck(logger, connectionCheckCancelTokenSource.Token, requestDisconnectEvent, connection, InternalDisconnect), TaskCreationOptions.LongRunning);
                    taskServerConnectionCheck.Start();

                    connection.IsConnected = true;
                    Connected?.Invoke(this, new ConnectedEventArgs());
                    logger.Info($"Reconnection success ({connection.Callsign})");
                    return;
                }
                catch (Exception ex)
                {
                    logger.Debug("Discarding the following exception");
                    logger.Debug(ex);
                    //Swallow the exception which is likely to be an API timeout             
                }
            }

            logger.Debug("Reconnection failed");
            Disconnected?.Invoke(this, new DisconnectedEventArgs() { Reason = "Reconnection failed", AutoReconnect = false });
        }

        private async Task GetVoiceCredentials(string callsign)
        {
            connection.Tokens = await connection.ApiServerConnection.AddCallsign(callsign);
            connection.VoiceConnectionDateTimeUtc = DateTime.UtcNow;
            connection.CreateCryptoChannels();
        }

        private void ConnectToVoiceServer()
        {
            DisconnectFromVoiceServer();

            string[] s = connection.Tokens.VoiceServer.AddressIpV4.Split(':');

            if (!IPAddress.TryParse(s[0], out IPAddress voiceServerIp))
            {
                logger.Error("IP address not in correct format");
                throw new Exception("IP address not in correct format");
            }

            if (!int.TryParse(s[1], out int voiceServerPort))
            {
                logger.Error("Port number not in correct format");
                throw new Exception("Port number not in correct format");
            }

            const int SIO_UDP_CONNRESET = -1744830452;
            udpClient = new UdpClient(0);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                udpClient.Client.IOControl(
                (IOControlCode)SIO_UDP_CONNRESET,
                new byte[] { 0, 0, 0, 0 },
                null);
            }

            var remoteEndpoint = new IPEndPoint(voiceServerIp, voiceServerPort);
            voiceServerCancelTokenSource = new CancellationTokenSource();
            connection.TaskVoiceServerTransmit = new Task(() => TaskVoiceServerTransmit(logger, voiceServerCancelTokenSource.Token, connection, udpClient, remoteEndpoint, VoiceServerTransmitQueue), TaskCreationOptions.LongRunning);
            connection.TaskVoiceServerTransmit.Start();
            connection.TaskVoiceServerReceive = new Task(() => TaskVoiceServerReceive(logger, voiceServerCancelTokenSource.Token, connection, udpClient, VoiceServerReceiveQueue), TaskCreationOptions.LongRunning);
            connection.TaskVoiceServerReceive.Start();
            connection.TaskVoiceServerHeartbeat = new Task(() => TaskVoiceServerHeartbeat(logger, voiceServerCancelTokenSource.Token, connection, VoiceServerTransmitQueue), TaskCreationOptions.LongRunning);
            connection.TaskVoiceServerHeartbeat.Start();

            logger.Debug("Connected to voice server (" + voiceServerIp.ToString() + ":" + voiceServerPort.ToString() + ")");
        }

        private void DisconnectFromVoiceServer()
        {
            if (voiceServerCancelTokenSource != null)
                voiceServerCancelTokenSource.Cancel();
            if (udpClient != null)
                udpClient.Close();

            if (connection.TaskVoiceServerTransmit?.Status == TaskStatus.Running)
                connection.TaskVoiceServerTransmit.Wait();
            connection.TaskVoiceServerTransmit = null;

            if (connection.TaskVoiceServerReceive?.Status == TaskStatus.Running)
                connection.TaskVoiceServerReceive.Wait();
            connection.TaskVoiceServerReceive = null;

            if (connection.TaskVoiceServerHeartbeat?.Status == TaskStatus.Running)
                connection.TaskVoiceServerHeartbeat.Wait();
            connection.TaskVoiceServerHeartbeat = null;

            logger.Debug("All TaskVoiceServer tasks stopped");
        }

        private static void TaskVoiceServerTransmit(
            Logger logger,
            CancellationToken cancelToken,
            ClientConnectionData connection,
            UdpClient udpClient,
            IPEndPoint server,
            BlockingCollection<IMsgPackTypeName> transmitQueue)
        {
            logger.Debug(nameof(TaskVoiceServerTransmit) + " started");
            CryptoDtoSerializer serializer = new CryptoDtoSerializer();
            try
            {
                byte[] dataBytes;
                while (!cancelToken.IsCancellationRequested)
                {
                    if (transmitQueue.TryTake(out IMsgPackTypeName obj, 250, cancelToken))
                    {
                        if (connection.IsConnected)
                        {
                            switch (obj.GetType().Name)
                            {
                                case nameof(RadioTxDto):
                                    if (logger.IsTraceEnabled)
                                        logger.Trace(((RadioTxDto)obj).ToDebugString());
                                    dataBytes = serializer.Serialize(connection.VoiceCryptoChannel, CryptoDtoMode.ChaCha20Poly1305, (RadioTxDto)obj);
                                    udpClient.Send(dataBytes, dataBytes.Length, server);
                                    connection.VoiceServerBytesSent += dataBytes.Length;
                                    break;
                                case nameof(CallRequestDto):
                                    if (logger.IsTraceEnabled)
                                        logger.Trace("Sending CallRequestDto");
                                    dataBytes = serializer.Serialize(connection.VoiceCryptoChannel, CryptoDtoMode.ChaCha20Poly1305, (CallRequestDto)obj);
                                    udpClient.Send(dataBytes, dataBytes.Length, server);
                                    connection.VoiceServerBytesSent += dataBytes.Length;
                                    break;
                                case nameof(CallResponseDto):
                                    if (logger.IsTraceEnabled)
                                        logger.Trace("Sending CallResponseDto");
                                    dataBytes = serializer.Serialize(connection.VoiceCryptoChannel, CryptoDtoMode.ChaCha20Poly1305, (CallResponseDto)obj);
                                    udpClient.Send(dataBytes, dataBytes.Length, server);
                                    connection.VoiceServerBytesSent += dataBytes.Length;
                                    break;
                                case nameof(HeartbeatDto):
                                    if (logger.IsTraceEnabled)
                                        logger.Trace("Sending voice server heartbeat");
                                    dataBytes = serializer.Serialize(connection.VoiceCryptoChannel, CryptoDtoMode.ChaCha20Poly1305, (HeartbeatDto)obj);
                                    udpClient.Send(dataBytes, dataBytes.Length, server);
                                    connection.VoiceServerBytesSent += dataBytes.Length;
                                    break;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            logger.Debug(nameof(TaskVoiceServerTransmit) + " stopped");
        }

        private static void TaskVoiceServerReceive(
            Logger logger,
            CancellationToken cancelToken,
            ClientConnectionData connection,
            UdpClient udpClient,
            BlockingCollection<IMsgPackTypeName> receiveQueue)
        {
            logger.Debug(nameof(TaskVoiceServerReceive) + " started");

            try
            {
                IPEndPoint sender = new IPEndPoint(IPAddress.Any, 60005);
                byte[] data;

                while (!cancelToken.IsCancellationRequested)
                {
                    data = udpClient.Receive(ref sender);               //UDP is a datagram protocol, not a streaming protocol, so it's whole datagrams here. 
                    if (data.Length < 30 || data.Length > 1500)
                        continue;
                    //Could check that the sender has the right IP - but NAT-ing significantly reduces the attack surface here
                    connection.VoiceServerBytesReceived += data.Length;

                    CryptoDtoDeserializer.Deserializer deserializer;
                    try
                    {
                        deserializer = CryptoDtoDeserializer.DeserializeIgnoreSequence(connection.VoiceCryptoChannel, data);
                    }
                    catch (Exception ex)
                    {
                        logger.Error(ex);
                        continue;
                    }

                    if (!deserializer.IsSequenceValid())
                    {
                        logger.Debug("Duplicate or old packet received");
                        continue;       //If a duplicate packet received (because it's UDP) - ignore it
                    }
                    //Crypto DTO stream is only concerned with duplicated or old packets, it doesn't discard out-of-order. Need to find out if Opus discards OOO packets.  

                    var dtoName = deserializer.GetDtoName();

                    if (dtoName == "AR")
                        dtoName = RadioRxDto.TypeNameConst;     //The server will be sending RadioRxDto as "AR" to maintain backwards compatibility for a year or two until old clients get updated.

                    switch (dtoName)
                    {
                        case RadioRxDto.TypeNameConst:
                            {
                                var dto = deserializer.GetDto<RadioRxDto>();
                                if (connection.ReceiveAudio && connection.IsConnected)
                                    receiveQueue.Add(dto);
                                break;
                            }
                        case nameof(CallRequestDto):
                        case ShortDtoNames.CallRequest:
                            {
                                var dto = deserializer.GetDto<CallRequestDto>();
                                if (connection.ReceiveAudio && connection.IsConnected)
                                    receiveQueue.Add(dto);
                                break;
                            }
                        case nameof(CallResponseDto):
                        case ShortDtoNames.CallResponse:
                            {
                                var dto = deserializer.GetDto<CallResponseDto>();
                                if (connection.ReceiveAudio && connection.IsConnected)
                                    receiveQueue.Add(dto);
                                break;
                            }
                        case nameof(HeartbeatAckDto):
                        case ShortDtoNames.HeartbeatAckDto:
                            connection.LastVoiceServerHeartbeatAckUtc = DateTime.UtcNow;
                            logger.Trace("Received voice server heartbeat");
                            break;
                    }
                }
            }
            catch (SocketException sex)
            {
                if (connection.IsConnected)     //If the socket exception occurs whilst disconnected, it's likely to just be a forced socket closure from doing disconnect().
                    logger.Error(sex);
            }
            catch (Exception ex)
            {
                logger.Error(ex);
            }

            logger.Debug(nameof(TaskVoiceServerReceive) + " stopped");
        }

        private static void TaskVoiceServerHeartbeat(
            Logger logger,
            CancellationToken cancelToken,
            ClientConnectionData connection,
            BlockingCollection<IMsgPackTypeName> transmitQueue)
        {
            logger.Debug(nameof(TaskVoiceServerHeartbeat) + " started");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            var keepAlive = new HeartbeatDto() { Callsign = connection.Callsign };
            while (!cancelToken.IsCancellationRequested)
            {
                if (stopWatch.ElapsedMilliseconds > 3000)
                {
                    transmitQueue.Add(keepAlive);
                    stopWatch.Restart();
                }
                Thread.Sleep(500);
            }

            logger.Debug(nameof(TaskVoiceServerHeartbeat) + " stopped");
        }

        private static async void TaskServerConnectionCheck(
            Logger logger,
            CancellationToken cancelToken,
            ManualResetEventSlim disconnectEvent,
            ClientConnectionData connection,
            Func<DisconnectReasons, Task> disconnectReason)
        {
            logger.Debug(nameof(TaskServerConnectionCheck) + " started");

            Stopwatch stopWatch = new Stopwatch();
            stopWatch.Start();
            while (!cancelToken.IsCancellationRequested)
            {
                if (connection.IsConnected)
                {
                    if (connection.DisconnectRequested)
                    {
                        await disconnectReason(DisconnectReasons.Requested);
                        break;
                    }
                    else if (stopWatch.ElapsedMilliseconds > 3000)
                    {
                        if (!connection.VoiceServerAlive)
                        {
                            logger.Error($"Lost connection to Voice Server. ({connection.Callsign})");
                            await disconnectReason(DisconnectReasons.LostConnection);
                            break;
                        }
                        if (connection.TaskVoiceServerHeartbeat?.Status != TaskStatus.Running)
                        {
                            logger.Error($"TaskVoiceServerHeartbeat not running. ({connection.Callsign})");
                            await disconnectReason(DisconnectReasons.InternalLibraryError10);
                            break;
                        }
                        if (connection.TaskVoiceServerReceive?.Status != TaskStatus.Running)
                        {
                            logger.Error($"TaskVoiceServerReceive not running. ({connection.Callsign})");
                            await disconnectReason(DisconnectReasons.InternalLibraryError20);
                            break;
                        }
                        if (connection.TaskVoiceServerTransmit?.Status != TaskStatus.Running)
                        {
                            logger.Error($"TaskVoiceServerTransmit not running. ({connection.Callsign})");
                            await disconnectReason(DisconnectReasons.InternalLibraryError30);
                            break;
                        }
                        stopWatch.Restart();
                    }
                }
                disconnectEvent.Wait(500);
            }
            stopWatch.Stop();
            logger.Debug(nameof(TaskServerConnectionCheck) + " stopped");
        }
    }
}
