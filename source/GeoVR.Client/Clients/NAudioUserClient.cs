using GeoVR.Shared;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    /// <summary>
    /// This class requires references to NAudio. Use for manual processing of Audio or directly setting MMDevices 
    /// </summary>
    public class NAudioUserClient : UserClient
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="apiServer"></param>
        /// <param name="eventHandler"></param>
        /// <param name="enableSelcal"></param>
        public NAudioUserClient(string apiServer, EventHandler<TransceiverReceivingCallsignsChangedEventArgs> eventHandler, bool enableSelcal = false) : base(apiServer, eventHandler, enableSelcal)
        {

        }

        /// <summary>
        /// Adds an additional soundcard, without an input or output device.
        /// Use this soundcard to manually process received Audio by calling Soundcard.GetOutputSampleProvider.
        /// </summary>
        /// <param name="transceiverIDs">IDs of the transceivers to process</param>
        /// <exception cref="Exception">Client started</exception>
        public void AddSoundcard(List<ushort> transceiverIDs)
        {
            if (Started)
                throw new Exception("Cannot add while client started");

            AddSoundcard(null, null, transceiverIDs);
        }

        public new void AddSoundcard(MMDevice inputDevice, MMDevice outputDevice, List<ushort> transceiverIDs)
        {
            base.AddSoundcard(inputDevice, outputDevice, transceiverIDs);
        }

        public new void ChangeSoundcardInputDevice(Soundcard soundcard, MMDevice device)
        {
            base.ChangeSoundcardInputDevice(soundcard, device);
        }

        public new void ChangeSoundcardOutputDevice(Soundcard soundcard, MMDevice device)
        {
            base.ChangeSoundcardOutputDevice(soundcard, device);
        }
        /// <summary>
        /// When Soundcard does not have an Output set, this may be used to read samples manually
        /// </summary>
        /// <exception cref="Exception">Output exists</exception>
        public ISampleProvider GetSoundcardSampleProvider(Soundcard soundcard)
        {
            return soundcard.GetOutputSampleProvider();
        }
    }
}
