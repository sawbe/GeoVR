#ATC Client Implementation Notes

### Instantiating Client

UserClient GeoUserClient = new UserClient("https://voice1.vatsim.uk", UserClient_ReceivingCallsignsChanged);
        
###  Connected Event

When server connection is established this can be used to update your UI

            GeoUserClient.Connected += UserClient_Connected;
            
              private void UserClient_Connected(object sender, ConnectedEventArgs e)

        {
        }
    
### Disconnected Event
          
          GeoUserClient.Disconnected += UserClient_Disconnected;
          
Called when client disconnected, used to update UI, note the E.AutoReconnect

            
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
           
            
            
###ReceivingCallsignsChanged

Will return all the callsigns currently being received and the transciver ID

  private void UserClient_ReceivingCallsignsChanged(object sender, TransceiverReceivingCallsignsChangedEventArgs e)
        {
        }
