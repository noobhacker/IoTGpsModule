# IoTGpsModule
Windows 10 IoT Gps Module Library
Tested on uBlox neo-6M GPS Module
Should work on any GPS Module that output NMEA informations

1. Wiring
Connect 5V and GND from your GPS Module to RaspberryPi, then Rx to Tx and Tx to Rx. 
This is because Tx means Transmitter which sends the data and Rx for receiver so you can't match them.

2. Coding
Simply initialize the object to start transmitting the data from gps module to the raspberry pi.
GpsInformation g = new IoTGpsModule.GpsInformation();

3. Getting the informations
There is CurrentGpsStatus, 
None: GPS Module not connected
Active: THe GPS knows your location
Void: Settlelites not connected, unknown location but connected to GPS Module.

There are two classes which contains the information, 
PositionInfo: Contains the position info for most applications, longitude and latitude, accuracy and etc.
SattelitesInfo: This doesn't make sense on normal usage but I love this thing, you can get all sattelites info from here
