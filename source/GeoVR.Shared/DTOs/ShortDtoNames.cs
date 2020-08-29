using System;
using System.Collections.Generic;
using System.Text;

namespace GeoVR.Shared
{
    public static class ShortDtoNames
    {
        public const string HeartbeatDto = "H";
        public const string HeartbeatAckDto = "HA";
        public const string RadioTxDto = "AT"; //"RT"; // Change this to RT once server accepts AT or RT
        public const string RadioRxDto = "RR";
        public const string CallRequest = "CQ";
        public const string CallResponse = "CR";    
        public const string PositionDto = "P";
    }
}
