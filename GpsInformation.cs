using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Enumeration;
using Windows.Devices.Geolocation;
using Windows.Devices.SerialCommunication;
using Windows.Storage.Streams;

namespace IoTGpsModule
{
    public class GpsInformation
    {
        #region ConnectingToModule
        public string PortId { get; set; }
        public string PortName { get; set; }
                
        public SerialDevice serialPort = null;
        DataWriter dataWriteObject = null;
        DataReader dataReaderObject = null;

        CancellationTokenSource ReadCancellationTokenSource;

        private async void connectToUART(int baudRate)
        {
            try
            {
                string aqs = SerialDevice.GetDeviceSelector();
                var dis = await DeviceInformation.FindAllAsync(aqs);

                for (int i = 0; i < dis.Count; i++)
                {
                    // printMessage("Device found: " + dis[i].Id);
                    if (dis[i].Id.EndsWith("UART0"))
                    {
                        serialPort = await SerialDevice.FromIdAsync(dis[i].Id);
                        PortId = dis[i].Id;
                        break;
                    }
                }

                 PortName = serialPort.PortName;

                serialPort.WriteTimeout = TimeSpan.FromMilliseconds(2000); // default=2000
                serialPort.ReadTimeout = TimeSpan.FromMilliseconds(2000);
                serialPort.BaudRate = Convert.ToUInt32(baudRate); // 57600
                serialPort.Parity = SerialParity.None;
                serialPort.StopBits = SerialStopBitCount.One;
                serialPort.DataBits = 8;
                serialPort.Handshake = SerialHandshake.None;

                ReadCancellationTokenSource = new CancellationTokenSource();

                listen();
            }
            catch (Exception ex)
            {
                LastErrorMessage = "Initialization error : " + ex.Message + ex.Data;
                connectToUART(baudRate);
            }
        }

        private async void listen()
        {
            try
            {
                if (serialPort != null)
                {
                    dataReaderObject = new DataReader(serialPort.InputStream);

                    while (true)
                    {
                        await readAsync(ReadCancellationTokenSource.Token);
                    }
                }
            }
            catch (Exception ex)
            {
                LastErrorMessage = "Listening error:" + ex.Message;
                listen();
            }
        }
        private async Task readAsync(CancellationToken cancellationToken)
        {
            Task<UInt32> loadAsyncTask;

            uint readBufferLength = 64; //default:1024

            cancellationToken.ThrowIfCancellationRequested();

            dataReaderObject.InputStreamOptions = InputStreamOptions.Partial;
            dataReaderObject.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

            loadAsyncTask = dataReaderObject.LoadAsync(readBufferLength).AsTask(cancellationToken);

            UInt32 bytesRead = await loadAsyncTask;

            try
            {
                
                if (bytesRead > 0)
                {
                    messagebuffer += (dataReaderObject.ReadString(bytesRead));
                    readGpsMessage();
                }
            }catch(Exception ex)
            {
                dataReaderObject.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf16BE;
                string tempmsg = (dataReaderObject.ReadString(512));
                // to skip those weird utf16be characters
                dataReaderObject.UnicodeEncoding = Windows.Storage.Streams.UnicodeEncoding.Utf8;

                LastErrorMessage = "Reading error:" + ex.Message;
                await readAsync(cancellationToken);
            }

        }

        string messagebuffer = "";

        private void readGpsMessage()
        {
            string msg = "";

            while (messagebuffer.Contains(Environment.NewLine))
            {
                
                msg = messagebuffer.Substring(0, messagebuffer.IndexOf(Environment.NewLine));
                
                // +1 to remove the \n\r
                messagebuffer = messagebuffer.Substring(messagebuffer.IndexOf(Environment.NewLine) + 2);

                updateFromNMEA(msg);
            }

            if (msg == "") LastErrorMessage = "Message string not parsed";
        }

        private async void write(string msg)
        {
            try
            {
                if (serialPort != null)
                {
                    dataWriteObject = new DataWriter(serialPort.OutputStream);

                    await writeAsync(msg);
                }
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
            }
        }

        private async Task writeAsync(string msg)
        {
            try
            {
                Task<UInt32> storeAsyncTask;

                dataWriteObject.WriteString(msg);

                storeAsyncTask = dataWriteObject.StoreAsync().AsTask();

                uint byteswritten = await storeAsyncTask;

                if (byteswritten > 0)
                {
                    // printMessage("Command " + msg + "sent sucessfully!");
                }
            }
            catch (Exception ex)
            {
                LastErrorMessage = ex.Message;
            }
        }

        private void CancelReadTask()
        {
            if (ReadCancellationTokenSource != null)
            {
                if (!ReadCancellationTokenSource.IsCancellationRequested)
                {
                    ReadCancellationTokenSource.Cancel();
                }
            }
        }

        private void CloseDevice()
        {
            if (serialPort != null)
            {
                serialPort.Dispose();
            }
            serialPort = null;
        }

        #endregion
                
        public string LastErrorMessage { get; set; }
        public enum GpsStatus { Active, Void, None};
        public GpsStatus CurrentGpsStatus { set; get; }
        public PositionInfoClass PositionInfo { get; set; }
        public SatellitesInfoClass SatellitesInfo { get; set; }

        public GpsInformation()
        {
            connectToUART(9600);
            PositionInfo = new PositionInfoClass();
            SatellitesInfo = new SatellitesInfoClass();
        }

        public async void SendMessageToModule(string msg)
        {
            write(msg);
        }

        private void updateFromNMEA(string msg)
        { 
            try
            {
                // remove checksum
                if (msg.Contains("*"))
                    msg = msg.Substring(0,msg.IndexOf("*"));

                string[] data = msg.Split(',');

                
                switch (data[0].Substring(3, 3))
                {
                    // Recommended minimum
                    case "RMC":
                        DateTime d = new DateTime();
                        
                        // add time
                        d = d.AddHours((data[1] != "") ? Convert.ToInt32(data[1].Substring(0, 2)) : 0);
                        d = d.AddMinutes((data[1] != "") ? Convert.ToInt32(data[1].Substring(2,2)) : 0);
                        d = d.AddSeconds((data[1] != "") ? Convert.ToInt32(data[1].Substring(4,2)) : 0);

                        // add date
                        d = d.AddDays((data[9] != "") ? Convert.ToInt32(data[9].Substring(0,2)) - 1 : 0) ;
                        d = d.AddMonths((data[9] != "") ? Convert.ToInt32(data[9].Substring(2, 2)) - 1 : 0);
                        d = d.AddYears((data[9] != "") ? Convert.ToInt32("20" + data[9].Substring(4, 2)) - 1 : 0);

                        
                        SatellitesInfo.SatelliteDateTime = d.ToLocalTime();

                        // active/not active
                        switch (data[2])
                        {
                            case "A":
                                CurrentGpsStatus = GpsStatus.Active;
                                break;
                            case "V":
                                CurrentGpsStatus = GpsStatus.Void;
                                break;
                            default:
                                CurrentGpsStatus = GpsStatus.None;
                                break;
                        }

                        PositionInfo.Latitude = (data[3] != "") ? 
                            Convert.ToDouble(data[3].Substring(0,data[3].IndexOf(".") - 2)) +
                            (Convert.ToDouble(data[3].Substring(data[3].IndexOf(".") - 2)) / 60)          
                            : (double?)null;

                        if (data[4] == "S")
                            PositionInfo.Latitude -= 2 * PositionInfo.Latitude; // make it negative

                        PositionInfo.Longitude = (data[5] != "") ?
                            Convert.ToDouble(data[5].Substring(0, data[5].IndexOf(".") - 2)) +
                            (Convert.ToDouble(data[5].Substring(data[5].IndexOf(".") - 2)) / 60)
                            : (double?)null;
                        if (data[6] == "W")
                            PositionInfo.Longitude -= 2 * PositionInfo.Longitude;

                        // speed
                        PositionInfo.Speed = (data[7] != "") ? Convert.ToDouble(data[7]) : (double?)null;

                        // facing position to the true north
                        PositionInfo.FacingDirection = (data[8] != "") ? Convert.ToDouble(data[8]) : (double?)null;

                        PositionInfo.MagneticVariation = (data[10] != "") ? Convert.ToDouble(data[10]) : (double?)null;

                        break;

                    case "GGA":

                        if (data[6] != "")
                        {
                            switch (Convert.ToInt32(data[6]))
                            {
                                case 0:
                                    SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.None;
                                    break;
                                case 1:
                                    SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.GpsFix;
                                    break;
                                case 2:
                                    SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.DGpsFix;
                                    break;
                                case 3:
                                    SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.PpsFix;
                                    break;
                                case 4:
                                    SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.RealTimeKinematic;
                                    break;
                                case 5:
                                    SatellitesInfo.CurrentFixQuality = SatellitesInfoClass.FixQuality.FloatRTK;
                                    break;

                            }
                        }

                        SatellitesInfo.UsedSatelliteCount =
                            (data[7] != "") ? Convert.ToInt32(data[7]) : (int?)null;

                        PositionInfo.AltitudeAccuracy =
                            (data[8] != "") ? Convert.ToDouble(data[8]) : (double?)null;

                        PositionInfo.Altitude =
                            (data[9] != "") ? Convert.ToDouble(data[9]) : (double?)null;

                        break;

                    // detailed satellite data
                    case "GSV":
                        SatellitesInfo.TotalSatelliteCount = (data[3] != "") ? Convert.ToInt32(data[3]) : (int?)null;

                        var s = new List<SatelliteInfoClass>();

                        // 4,8,12,16 is id 5,9,13,17 is elevation and so on
                        for (int i = 4; i <= 16; i += 4)
                        {
                            s.Add(new SatelliteInfoClass()
                            {
                                Id = (data[i] != "") ? Convert.ToInt32(data[i]) : (int?)null,
                                Elevation = (data[i + 1] != "") ? Convert.ToInt32(data[i + 1]) : (int?)null,
                                Azimuth = (data[i + 2] != "") ? Convert.ToInt32(data[i + 2]) : (int?)null,
                                Snr = (data[i + 3] != "") ? Convert.ToInt32(data[i + 3]) : (int?)null,
                                // InUse = (data[i + 1] != "") ? true : false
                            });
                        }

                        // update into list, if dont have, add them
                        if (SatellitesInfo.SatelliteList.Count > 0)
                        {
                            foreach (SatelliteInfoClass new_s in s)
                            {
                                bool updated = false;

                                foreach (SatelliteInfoClass sl in SatellitesInfo.SatelliteList)
                                {
                                    if (sl.Id == new_s.Id)
                                    {
                                        // sl.Id = new_s.Id;
                                        sl.Elevation = new_s.Elevation;
                                        sl.Azimuth = new_s.Azimuth;
                                        sl.Snr = new_s.Snr;

                                        updated = true;
                                    }
                                    
                                }

                                if(!updated) // add new if not updated, after looped finish each sattelites
                                {
                                    SatellitesInfo.SatelliteList.Add(new SatelliteInfoClass
                                    {
                                        Id = new_s.Id,
                                        Elevation = new_s.Elevation,
                                        Azimuth = new_s.Azimuth,
                                        Snr = new_s.Snr
                                    });
                                }
                            }
                        }
                        else
                        {
                            // add all of them if the count is 0
                            for(int i = 0;i<s.Count;i++)
                            {
                                SatellitesInfo.SatelliteList.Add(s[i]);
                            }
                        }
                        break;

                    case "GSA":
                        switch (data[1])
                        {
                            case "A":
                                SatellitesInfo.IsFixTypeAutomatic = true;
                                break;
                            case "M":
                                SatellitesInfo.IsFixTypeAutomatic = false;
                                break;
                            default:
                                SatellitesInfo.IsFixTypeAutomatic = (bool?)null;
                                break;
                        }

                        if (data[2] != "")
                        {
                            switch (Convert.ToInt32(data[2]))
                            {
                                case 1:
                                    SatellitesInfo.CurrentFixType = SatellitesInfoClass.FixType.None;
                                    break;
                                case 2:
                                    SatellitesInfo.CurrentFixType = SatellitesInfoClass.FixType.TwoD;
                                    break;
                                case 3:
                                    SatellitesInfo.CurrentFixType = SatellitesInfoClass.FixType.ThreeD;
                                    break;
                            }
                        }

                        // 12 spaces for which satellite used for fix
                        foreach (SatelliteInfoClass satellites in SatellitesInfo.SatelliteList)
                        {
                            for (int i = 3; i < 15; i++)
                            {
                                if (data[i] != "")
                                {
                                    int satelliteId = Convert.ToInt32(data[i]);

                                    if (satellites.Id == satelliteId)
                                        satellites.InUse = true;

                                    // if the satellites is in used list then will find out and break
                                    break;
                                }
                                if (i == 15) // if till 15 still not in used list then it is not used
                                    satellites.InUse = false;
                            }
                        }

                        PositionInfo.Accuracy =
                            (data[15] != "") ? Convert.ToDouble(data[15]) : (double?)null;

                        PositionInfo.LatitudeAccuracy =
                            (data[16] != "") ? Convert.ToDouble(data[16]) : (double?)null;

                        PositionInfo.LongitudeAccuracy =
                            (data[17] != "") ? Convert.ToDouble(data[17]) : (double?)null;

                        break;
                }
            }catch(Exception ex)
            {
                LastErrorMessage = "NMEA parsing error" + ex.Message + ex.Data + ex.StackTrace + ex.Source;
            }
        }
    }
}
