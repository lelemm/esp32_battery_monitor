
# esp32_battery_monitor
esp32 BLE server that receives data from PC. Battery information from headset (Logitech G533) and mouse (Steelseries Rival 650). 

*THIS IS A PROTOTYPE. NOT STABLE AT ALL*

PC code: C# Windows Service.
The Service uses the USB to retrieve battery information from headset and mouse.
The mouse part I used Wireshark to reverse engineering the protocol. I'm not 100% it's all it needs to work.
The headset part I found some code online (don't remember where to give credit, sorry), It works most of time.
I had to use some timers to retrieve information from devices, pretty sure this part could be improved.


ESP32 code: Arduino IDE to code. It is basically an BLE server with 4 characteristics (Mouse battery, Headset battery, Mute/Unmute, PC Volume Level)

Below you can see the interface created with TFT_espi and lvgl.io:
![Image](https://github.com/lelemm/esp32_battery_monitor/blob/main/Images/monitor.jpeg?raw=true)

The first progress bar is the mouse battery

The second progress bar is the headset battery

The third is the PC Volume level

When MIC is muted, the mute icon will be visible.

The bluetooth icon is to show that some device is connected to the BLE server.
