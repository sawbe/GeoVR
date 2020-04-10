# ATC Client Implementation Notes

### Instantiating Client

`UserClient GeoUserClient = new UserClient("https://voice1.vatsim.uk", UserClient_ReceivingCallsignsChanged);`
        
###  Connected Event

When server connection is established this can be used to update your UI
```
        GeoUserClient.Connected += UserClient_Connected;
            
        private void UserClient_Connected(object sender, ConnectedEventArgs e)

        {
        }
```
    
### Disconnected Event
`
          GeoUserClient.Disconnected += UserClient_Disconnected;
`

Called when client disconnected, used to update UI, note the E.AutoReconnect


```
        private void UserClient_Disconnected(object sender, DisconnectedEventArgs e)
        {
                if (!e.AutoReconnect)
                {
                        //Update UI to show a voice reconnect is happening
                }
                else
                { 
                        //Update UI to show disconnect complete
                }
```
            
### ReceivingCallsignsChanged

Will return all the callsigns currently being received and the transciver ID

```
  private void UserClient_ReceivingCallsignsChanged(object sender, TransceiverReceivingCallsignsChangedEventArgs e)
        {
        }
```


### Starting Client Audio System

Starts the Audio Subsystem.  List of Trans is a list of transcievers your client has numbered from 0 to however many transceivers you have.  You will have to start the client with however many you think you need, and then restart if more are added.  In standalone we start with 10 and then if it goes above that restart the subsystem with more trans.  With every transceiver created the CPU will increase as each transceiver is an naudio mixer channel.  

  ```GeoUserClient.Start(mConfig.InputDeviceName, mConfig.OutputDeviceName, listOfTrans);```

### Stopping Client Audio System

``` GeoUserClient.Stop();```

### Connecting Client

Make your connection string ClientName + Version... eg  VATSYS 0.5.5a

```await GeoUserClient.Connect(mConfig.NetworkLogin, mConfig.NetworkPassword, clientCallsign,"Standalone " + Application.ProductVersion);```


### Disconnecting Client

 ```GeoUserClient.Disconnect("Closing");  //Reason for disconnection, ie form closes, disconnect pressed```

###   Transceiver List

Each transceiver is a radio mast, a list of transceiver information can be pulled from database (further down), update your transceiver list whenever it changes.  Whenever a transceiver is added you receive on it.  You can have multiple transcievers per frequency. 

```
List<TransceiverDto> translist = new List<TransceiverDto>();
        
  translist.Add(new TransceiverDto()
                {
                    ID = 0,
                    Frequency = freq,
                    LatDeg = latDeg,
                    LonDeg = lonDeg,
                    HeightAglM = HeightAglM,
                    HeightMslM = HeightMslM
                });
                
GeoUserClient.UpdateTransceivers(translist);
```


### Transmitting Transceivers

An array of all Transceivers which are transmitting when PTT is pressed.

```
txRadios.Add(new TxTransceiverDto() { ID = 0 });
GeoUserClient.TransmittingTransceivers(txRadios.ToArray());
```

### Cross Coupling

You can cross couple your transceivers as follows.  You can cross couple transceivers on a single frequency or multiple transcivers on multiple frequencies

```
  List<ushort> crossCoupledTransceivers = new List<ushort>();
  
   crossCoupledTransceivers.Add((ushort)transNum);
   
    GeoUserClient.UpdateCrossCoupleGroups(new List<CrossCoupleGroupDto>() { new CrossCoupleGroupDto() { ID = 0, TransceiverIDs = crossCoupledTransceivers } });
    
```
   
### PTT

```GeoUserClient.PTT(active);```


### Get all Transceivers from Database for a station

```GeoUserClient.ApiServerConnection.GetStationTransceiversAllDistinctObeyExclusions(clientCallsign);```

### Populate VCCS Panel from Database 

``` var topDownStations = await GeoUserClient.ApiServerConnection.GetVccsStations(clientCallsign);```
 
 ### Get data for a single station
 
``` StationDto stationData = await GeoUserClient.ApiServerConnection.GetStation(stationName);```
 
 


   
