
### Changes

- HF random crackle added
- Fixed bug where HF noise would play when it shouldn't
- HF Uses Separate Equalizer
- AC Bus Noise fixed and tweaked on HF and VHF
- HF sound now loops properly and sound better (clear buffer on silence)
- Multiple Soundcard support
- Selcal Sending from controller client over voice, FSD is used to notify the pilot client of a call still.
- Allow changing of audio settings without stopping and restarting client
- WASAPI now used for Input as well as Output devices

### Implementation Notes
Use either `NAudioUserClient` or `UserClient` depending on if you want to reference naudio in your app and supply WASAPI MMDevice's directly or not (friendlynames otherwise).

Then for each radio (input/output device combination) you need to call UserClient.AddSoundcard with devices and transceiver ids (multiple soundcards may use the same transceivers) **before** starting the client with UserClient.Start. 

Each added Soundcard can be got from `UserClient.Soundcards` and you can call Soundcard.UpdateTransmittingTransceivers, OutputVolume etc. to config each soundcard independently. 

Once client is started / connected call UpdateTransceivers regularly the same way it was done previously for all the active transceivers across all soundcards 

UserClient.PTT still exists but takes a soundcard parameter to specify which is transmitting. A simple check is done for same transceiver ids if more than one transmit at a time, but it’s still untested and might blow something up if more than one transmit 🤷‍♂️
