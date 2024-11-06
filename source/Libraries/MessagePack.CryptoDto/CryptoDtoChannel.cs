﻿using NaCl.Core;
using System;
using System.Linq;
using System.Security.Cryptography;
using ChaCha20Poly1305 = NaCl.Core.ChaCha20Poly1305;

namespace MessagePack.CryptoDto
{
    public class CryptoDtoChannel
    {
        readonly object channelLock = new object();
        private readonly byte[] aeadTransmitKey;
        private ulong transmitSequence;

        private readonly byte[] aeadReceiveKey;

        private readonly ulong[] receiveSequenceHistory;
        private int receiveSequenceHistoryDepth;
        private readonly int receiveSequenceSizeMaxSize;

        public string ChannelTag { get; private set; }
        public DateTime LastTransmitUtc { get; private set; }
        public DateTime LastReceiveUtc { get; private set; }

        private ChaCha20Poly1305 transmit = null;
        internal ChaCha20Poly1305 TransmitChaCha20Poly1305 { get { if (transmit == null) { transmit = new ChaCha20Poly1305(aeadTransmitKey); } return transmit; } }
        private ChaCha20Poly1305 receive = null;
        internal ChaCha20Poly1305 ReceiveChaCha20Poly1305 { get { if (receive == null) { receive = new ChaCha20Poly1305(aeadReceiveKey); } return receive; } }

        public CryptoDtoChannel(string channelTag, int receiveSequenceHistorySize = 10)
        {
            ChannelTag = channelTag;
            
            using (var rnd = RandomNumberGenerator.Create())
            {
                aeadReceiveKey = new byte[Aead.KeySize];
                rnd.GetBytes(aeadReceiveKey);

                aeadTransmitKey = new byte[Aead.KeySize];
                rnd.GetBytes(aeadTransmitKey);
            }

            transmitSequence = 0;
            receiveSequenceSizeMaxSize = receiveSequenceHistorySize;
            if (receiveSequenceSizeMaxSize < 1)
                receiveSequenceSizeMaxSize = 1;
            receiveSequenceHistory = new ulong[receiveSequenceSizeMaxSize];
            receiveSequenceHistoryDepth = 0;
        }

        public CryptoDtoChannel(CryptoDtoChannelConfigDto channelConfig, int receiveSequenceHistorySize = 10)
        {
            ChannelTag = channelConfig.ChannelTag;
            aeadReceiveKey = channelConfig.AeadReceiveKey;
            aeadTransmitKey = channelConfig.AeadTransmitKey;

            transmitSequence = 0;
            receiveSequenceSizeMaxSize = receiveSequenceHistorySize;
            if (receiveSequenceSizeMaxSize < 1)
                receiveSequenceSizeMaxSize = 1;
            receiveSequenceHistory = new ulong[receiveSequenceSizeMaxSize];
            receiveSequenceHistoryDepth = 0;
        }

        public CryptoDtoChannelConfigDto GetRemoteEndpointChannelConfig()
        {
            lock (channelLock)
            {
                return new CryptoDtoChannelConfigDto()
                {
                    ChannelTag = ChannelTag,
                    AeadReceiveKey = aeadTransmitKey.ToArray(),
                    AeadTransmitKey = aeadReceiveKey.ToArray()      //Swap the order for the remote endpoint
                };
            }
        }

        internal ReadOnlySpan<byte> GetReceiveKey(CryptoDtoMode mode)
        {
            lock (channelLock)
            {
                switch (mode)
                {
                    case CryptoDtoMode.ChaCha20Poly1305:
                        return aeadReceiveKey;
                    default:
                        throw new CryptographicException("CryptoDtoMode value not handled.");
                }
            }
        }

        internal void CheckReceivedSequence(ulong sequenceReceived)
        {
            lock (channelLock)
            {
                if (Contains(sequenceReceived))
                {
                    throw new CryptographicException("Received sequence has been duplicated.");         // Duplication or replay attack
                }

                if (receiveSequenceHistoryDepth < receiveSequenceSizeMaxSize)                       //If the buffer has been filled...
                {
                    receiveSequenceHistory[receiveSequenceHistoryDepth++] = sequenceReceived;
                }
                else
                {
                    var minValue = GetMin(out int minIndex);
                    if (sequenceReceived < minValue)
                        throw new CryptographicException("Received sequence is too old.");              // Possible replay attack
                    receiveSequenceHistory[minIndex] = sequenceReceived;
                }

                LastReceiveUtc = DateTime.UtcNow;
            }
        }

        internal bool IsReceivedSequenceAllowed(ulong sequenceReceived)
        {
            lock (channelLock)
            {
                if (Contains(sequenceReceived))
                {
                    return false;
                }

                if (receiveSequenceHistoryDepth < receiveSequenceSizeMaxSize)                       //If the buffer has not been filled...
                {
                    receiveSequenceHistory[receiveSequenceHistoryDepth++] = sequenceReceived;
                    LastReceiveUtc = DateTime.UtcNow;
                    return true;
                }
                else
                {
                    var minValue = GetMin(out int minIndex);
                    if (sequenceReceived < minValue)
                        return false;
                    else
                    {
                        receiveSequenceHistory[minIndex] = sequenceReceived;
                        LastReceiveUtc = DateTime.UtcNow;
                        return true;
                    }
                }
            }
        }

        internal ReadOnlySpan<byte> GetTransmitKey(CryptoDtoMode mode, out ulong sequenceToSend)
        {
            lock (channelLock)
            {
                sequenceToSend = transmitSequence;
                transmitSequence++;
                LastTransmitUtc = DateTime.UtcNow;

                switch (mode)
                {
                    case CryptoDtoMode.ChaCha20Poly1305:
                        return aeadTransmitKey;
                    default:
                        throw new CryptographicException("CryptoDtoMode value not handled.");
                }
            }
        }

        private bool Contains(ulong sequence)
        {
            lock (channelLock)
            {
                for (int i = 0; i < receiveSequenceHistoryDepth; i++)
                {
                    if (receiveSequenceHistory[i] == sequence)
                        return true;
                }
                return false;
            }
        }

        private ulong GetMin(out int minIndex)
        {
            lock (channelLock)
            {
                ulong minValue = ulong.MaxValue;
                minIndex = -1;
                int index = -1;

                for (int i = 0; i < receiveSequenceHistoryDepth; i++)
                {
                    index++;

                    if (receiveSequenceHistory[i] <= minValue)
                    {
                        minValue = receiveSequenceHistory[i];
                        minIndex = index;
                    }
                }
                return minValue;
            }
        }
    }
}
