using MessagePack;

namespace GeoVR.Shared.DTOs
{
    [MessagePackObject]
    public class PositionDto
    {
        [Key(0)]
        public string Callsign { get; set; }
        /// <summary>
        /// Represents milliseconds within a minute. Values from 0 to 59999. A minute is used as it's comfortably longer than the AFV network timeout. 
        /// Generated on the transmitting client before sending, at the best possible accuracy (ideally at the point at which data is read from the simulator). 
        /// Used on the receiving clients to determine if data is arriving late as a no/no-go for interpolation (could even be used to calculate speed by using the time difference between 2 updates).
        /// </summary>
        [Key(1)]
        public ushort Counter { get; set; }
        [Key(2)]
        public double LatDeg { get; set; }
        [Key(3)]
        public double LonDeg { get; set; }
        [Key(4)]
        public float HeightMslM { get; set; }
        /// <summary>
        /// AGL is provided as well to allow receiving clients to apply a range check whereby if an aircraft is close to the ground (e.g. within 200m), switch from MSL to AGL to allow the touchdown/taxi to have wheels properly situated on the scenery surface.
        /// </summary>
        [Key(5)]
        public float HeightAglM { get; set; }
        [Key(6)]
        public float PitchDeg { get; set; }
        [Key(7)]
        public float BankDeg { get; set; }
        [Key(8)]
        public float HeadingDeg { get; set; }
    }
}
