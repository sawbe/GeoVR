using GeoVR.Shared;
using NAudio.CoreAudioApi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    public class NAudioUserClient : UserClient
    {
        public NAudioUserClient(string apiServer, EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler, bool enableSelcal = false) : base(apiServer, eventHandler, enableSelcal)
        {

        }
    }
}
