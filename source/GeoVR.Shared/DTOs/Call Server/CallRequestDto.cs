using MessagePack;
using MessagePack.CryptoDto;

namespace GeoVR.Shared
{
    [MessagePackObject]
    [CryptoDto(ShortDtoNames.CallRequest)]
    public class CallRequestDto : IMsgPackTypeName, ICallDto
    {
        [IgnoreMember]
        public const string TypeNameConst = ShortDtoNames.CallRequest;
        [IgnoreMember]
        public string TypeName { get { return TypeNameConst; } }

        [Key(0)]
        public string FromCallsign { get; set; }
        [Key(1)]
        public string ToCallsign { get; set; }      //The callsign being rung
    }
}