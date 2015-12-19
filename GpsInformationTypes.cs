using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IoTGpsModule
{
    public class SatelliteInfoClass
    {
        public int? Id { set; get; }
        public int? Elevation { set; get; }
        public int? Azimuth { set; get; }
        public int? Snr { set; get; }
        public bool? InUse { set; get; }
    }
    public class SatellitesInfoClass
    {
       
        public enum FixQuality { None, GpsFix, DGpsFix, PpsFix, RealTimeKinematic, FloatRTK };
        public enum FixType { None,TwoD,ThreeD}

        public ObservableCollection<SatelliteInfoClass> SatelliteList
            = new ObservableCollection<SatelliteInfoClass>();

        public int? TotalSatelliteCount { set; get; }
        public int? UsedSatelliteCount { get; set; }

        
        public FixQuality CurrentFixQuality { set; get; }
        public FixType CurrentFixType { set; get; }
        public bool? IsFixTypeAutomatic { get; set; }
        public DateTime? SatelliteDateTime { set; get; }

    }
    public class PositionInfoClass
    {
      
        
        public double? Latitude { set; get; }        
        public double? Longitude { set; get; }
        public double? Altitude { set; get; }
        public double? Speed { set; get; }
        public double? FacingDirection { set; get; }
        public double? Accuracy { set; get; }
        public double? LatitudeAccuracy { set; get; }
        public double? LongitudeAccuracy { set; get; }
        public double? AltitudeAccuracy { set; get; }
        public double? MagneticVariation { set; get; }

    }

}
