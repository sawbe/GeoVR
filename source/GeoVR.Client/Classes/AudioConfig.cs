using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GeoVR.Client
{
    public class AudioConfig
    {
        //Thread-safe singleton Pattern
        private static readonly Lazy<AudioConfig> lazy = new Lazy<AudioConfig>(() => new AudioConfig());

        public static AudioConfig Instance { get { return lazy.Value; } }

        private AudioConfig()
        {
            HfEqualizer = EqualizerPresets.VHFEmulation;
            VhfEqualizer = EqualizerPresets.VHFEmulation2;
        }
        //End of singleton pattern

        /// <summary>
        /// Can not be changed in realtime. Value is latched on UserClient.Start.
        /// </summary>
        public EqualizerPresets VhfEqualizer { get; set; }
        /// <summary>
        /// Can not be changed in realtime. Value is latched on UserClient.Start.
        /// </summary>
        public EqualizerPresets HfEqualizer { get; set; }
        /// <summary>
        /// Can not be changed in realtime. Value is latched on UserClient.Start.
        /// </summary>
        public bool HfSquelch { get; set; }
    }
}
