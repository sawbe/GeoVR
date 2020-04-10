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

###   Transceiver List

Each transceiver is a radio mast, a list of transceiver information can be pulled from database (further down), update your transceiver list whenever it changes.  Whenever a transceiver is added you receive on it.

```
List<TransceiverDto> translist = new List<TransceiverDto>();
        
  translist.Add(new TransceiverDto()
                {
                    ID = 0,
                    Frequency = InjectCom1,
                    LatDeg = latDeg,
                    LonDeg = lonDeg,
                    HeightAglM = HeightAglM,
                    HeightMslM = HeightMslM
                });
                
GeoUserClient.UpdateTransceivers(translist);
```


### Transmitting Transceivers

An array of all Transceivers which are transmitting when PTT is pressed

```
txRadios.Add(new TxTransceiverDto() { ID = 0 });
GeoUserClient.TransmittingTransceivers(txRadios.ToArray());
```

### PTT

```GeoUserClient.PTT(active);```


### Cross Coupling

You can cross couple your transceivers as follows.

```
  List<ushort> crossCoupledTransceivers = new List<ushort>();
  
   crossCoupledTransceivers.Add((ushort)transNum);
   
    GeoUserClient.UpdateCrossCoupleGroups(new List<CrossCoupleGroupDto>() { new CrossCoupleGroupDto() { ID = 0, TransceiverIDs = crossCoupledTransceivers } });
    
    ```
   
   
