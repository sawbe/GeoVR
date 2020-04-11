using MessagePack;
using MessagePack.CryptoDto;

namespace GeoVR.Shared.DTOs
{
    [MessagePackObject]
    [CryptoDto(ShortDtoNames.RadioTxDto)]
    public class RadioTxDto : IMsgPackTypeName, IAudioDto           //Tx from the perspective of the client
    {
        [IgnoreMember]
        public const string TypeNameConst = ShortDtoNames.RadioTxDto;
        [IgnoreMember]
        public string TypeName { get { return TypeNameConst; } }

        [Key(0)]
        public string Callsign { get; set; }
        [Key(1)]
        public uint SequenceCounter { get; set; }
        [Key(2)]
        public byte[] Audio { get; set; }
        [Key(3)]
        public bool LastPacket { get; set; }
        [Key(4)]
        public TxTransceiverDto[] Transceivers { get; set; }
    }
}
