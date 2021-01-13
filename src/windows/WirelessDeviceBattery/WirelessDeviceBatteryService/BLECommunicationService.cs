using HIDReader;
using NAudio.CoreAudioApi;
using NAudio.Mixer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;

namespace WirelessDeviceBatteryService
{
    public partial class BLECommunicationService : ServiceBase
    {
        G533 headset;
        SteelseriesRival650_Wireless mouse;

        BluetoothLEDevice bluetoothLEDevice;
        GattDeviceService service;
        GattCharacteristic mouseGattCharacteristic;
        GattCharacteristic headsetGattCharacteristic;
        GattCharacteristic muteGattCharacteristic;
        GattCharacteristic volumeGattCharacteristic;
        EventLog log;
        BluetoothLEAdvertisementWatcher watcher;
        Semaphore semaphore = new Semaphore(1, 1);

        BooleanMixerControl booleanMixerControl;
        UnsignedMixerControl unsignedMixerControl;
        bool muted = false;

        public BLECommunicationService()
        {
            try
            {
                InitializeComponent();
                log = new EventLog();
                if (!System.Diagnostics.EventLog.SourceExists("Monitor de Bateria"))
                {
                    System.Diagnostics.EventLog.CreateEventSource(
                        "Monitor de Bateria", "Monitor de Bateria");
                }

                log.Source = "Monitor de Bateria";
                //log.Log = "Monitor de Bateria";
            }
            catch(Exception ex)
            {
                log.WriteEntry(ex.StackTrace);
                log.WriteEntry(ex.Message);
            }

        }

        private System.Timers.Timer timer;
        MMDevice defaultDevice;

        protected override void OnStart(string[] args)
        {
            StartScanning();

            MMDeviceEnumerator devEnum = new MMDeviceEnumerator();
            defaultDevice = devEnum.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);


            this.timer = new System.Timers.Timer(1000D);  // 30000 milliseconds = 30 seconds
            this.timer.AutoReset = true;
            this.timer.Elapsed += new System.Timers.ElapsedEventHandler(this.timer_Elapsed);
            this.timer.Start();
        }

        private void timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                int waveInDeviceNumber = 0;
                var mixerLine = new MixerLine((IntPtr)waveInDeviceNumber,
                                               0, MixerFlags.WaveIn);

                booleanMixerControl = mixerLine.Controls.Single(f => f.ControlType == MixerControlType.Mute) as BooleanMixerControl;

                if (muteGattCharacteristic != null && bluetoothLEDevice != null && bluetoothLEDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                {
                    Task.Run(async () =>
                    {
                        await muteGattCharacteristic.WriteValueAsync(CryptographicBuffer.ConvertStringToBinary(booleanMixerControl.Value ? "1" : "0", BinaryStringEncoding.Utf8));
                    });
                }


                if(volumeGattCharacteristic != null && bluetoothLEDevice != null && bluetoothLEDevice.ConnectionStatus == BluetoothConnectionStatus.Connected && defaultDevice != null)
                {
                    Task.Run(async () =>
                    {
                        await volumeGattCharacteristic.WriteValueAsync(CryptographicBuffer.ConvertStringToBinary(Math.Truncate(defaultDevice.AudioEndpointVolume.MasterVolumeLevelScalar * 100).ToString(), BinaryStringEncoding.Utf8));
                    });
                }
            }
            catch (Exception ex)
            {
                log.WriteEntry(ex.StackTrace);
                log.WriteEntry(ex.Message);
            }
        }

        protected override void OnStop()
        {
            if (headset != null)
                headset.Dispose();

            if (mouse != null)
                mouse.Dispose();

            this.timer.Stop();
            this.timer = null;
        }



        public void StartScanning()
        {
            watcher = new BluetoothLEAdvertisementWatcher();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;

            // Only activate the watcher when we're recieving values >= -80
            watcher.SignalStrengthFilter.InRangeThresholdInDBm = -90;

            // Stop watching if the value drops below -90 (user walked away)
            watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -95;

            // Register callback for when we see an advertisements
            watcher.Received += OnAdvertisementReceived;

            // Wait 5 seconds to make sure the device is really out of range
            watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
            watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(5000);

            watcher.AdvertisementFilter.Advertisement.ServiceUuids.Clear();
            watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(Guid.Parse("4dd08908-637c-4d4e-95ca-90b84fe2fcea"));

            //watcher.AdvertisementFilter = ;


            // Starting watching for advertisements
            watcher.Start();
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            Thread.Sleep(3000);
            semaphore.WaitOne();

            if (bluetoothLEDevice == null || bluetoothLEDevice.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                log.WriteEntry(String.Format("Advertisement:" + "\r\n" +
                "  BT_ADDR: {0}" + "\r\n" + "  FR_NAME: {1}", eventArgs.BluetoothAddress, eventArgs.Advertisement.LocalName), EventLogEntryType.Information);
            }

            var mydelegate = new Action<object>(delegate (object param)
            {
                Task.WaitAll(ConnectToDevice(watcher, eventArgs));

            });
            mydelegate.Invoke("test");
        }

        private async Task ConnectToDevice(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            try
            {
                bluetoothLEDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(eventArgs.BluetoothAddress);

                if (service == null)
                {
                    bluetoothLEDevice.ConnectionStatusChanged += BluetoothLEDevice_ConnectionStatusChanged;

                    var services = await bluetoothLEDevice.GetGattServicesAsync();
                    //var services = await bluetoothLEDevice.GetGattServicesForUuidAsync(Guid.Parse("4dd08908-637c-4d4e-95ca-90b84fe2fcea"));

                    if (services.Status == GattCommunicationStatus.Success)
                    {
                        service = services.Services.FirstOrDefault(f => f.Uuid == Guid.Parse("4dd08908-637c-4d4e-95ca-90b84fe2fcea"));
                        log.WriteEntry("Connected with service");
                    }
                }

                await SetCharacteristics();

            }
            catch (Exception ex)
            {
                log.WriteEntry(ex.StackTrace, EventLogEntryType.Error);
                log.WriteEntry(ex.Message, EventLogEntryType.Error);
            }
            finally
            {
                semaphore.Release();
            }
        }

        private async Task SetCharacteristics()
        {
            if (service != null)
            {
                if (mouseGattCharacteristic == null)
                {
                    var characteristics = await service.GetCharacteristicsForUuidAsync(Guid.Parse("8f43ee07-60d0-4f43-a350-39c8fdf6dc5d"));

                    if (characteristics.Status == GattCommunicationStatus.Success)
                    {
                        mouseGattCharacteristic = characteristics.Characteristics.Single();
                        log.WriteEntry("Mouse Gatt Characteristic found");
                    }
                }

                if (headsetGattCharacteristic == null)
                {
                    var characteristics = await service.GetCharacteristicsForUuidAsync(Guid.Parse("e54807c3-f811-4a63-8c0a-983f7ccea869"));

                    if (characteristics.Status == GattCommunicationStatus.Success)
                    {
                        headsetGattCharacteristic = characteristics.Characteristics.Single();
                        log.WriteEntry("Headset Gatt Characteristic found");
                    }
                }

                if(muteGattCharacteristic == null)
                {
                    var characteristics = await service.GetCharacteristicsForUuidAsync(Guid.Parse("5170b989-5e82-49c7-8000-15f88f9ff17e"));

                    if (characteristics.Status == GattCommunicationStatus.Success)
                    {
                        muteGattCharacteristic = characteristics.Characteristics.Single();
                        log.WriteEntry("Mute Gatt Characteristic found");
                    }
                }

                if(volumeGattCharacteristic == null)
                {
                    var characteristics = await service.GetCharacteristicsForUuidAsync(Guid.Parse("2f0a825a-b3d2-49d0-a2a1-884e1cf8b975"));

                    if (characteristics.Status == GattCommunicationStatus.Success)
                    {
                        volumeGattCharacteristic = characteristics.Characteristics.Single();
                        log.WriteEntry("Volume Gatt Characteristic found");
                    }
                }

                if (headset == null && headsetGattCharacteristic != null)
                {
                    headset = new G533(200);
                    headset.OnBatteryNotification += Headset_OnBatteryNotification;
                }

                if (mouse == null && mouseGattCharacteristic != null)
                {
                    mouse = new SteelseriesRival650_Wireless(200);
                    mouse.OnBatteryNotification += Mouse_OnBatteryNotification;
                }
            }
        }

        private void BluetoothLEDevice_ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if(sender != null && sender.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                service = null;
                mouseGattCharacteristic = null;
                headsetGattCharacteristic = null;
                muteGattCharacteristic = null;
                volumeGattCharacteristic = null;

                watcher.Stop();
                if (headset != null)
                {
                    headset.Dispose();
                    headset = null;
                }

                if (mouse != null)
                {
                    mouse.Dispose();
                    mouse = null;
                }

                bluetoothLEDevice.ConnectionStatusChanged -= BluetoothLEDevice_ConnectionStatusChanged;

                log.WriteEntry("Disconnected from BLE server", EventLogEntryType.Information);

                StartScanning();
            }
        }

        private void Mouse_OnBatteryNotification(object sender, BatteryEventArgs e)
        {
            try
            {
                var obj = sender as WirelessDevice;
                if (obj != null)
                {
                    if (mouseGattCharacteristic != null && bluetoothLEDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                    {
                        Task.Run(async () =>
                        {
                            await mouseGattCharacteristic.WriteValueAsync(CryptographicBuffer.ConvertStringToBinary(obj.BatteryLevel.ToString(), BinaryStringEncoding.Utf8));
                        });
                    }

                }
            }
            catch (Exception ex)
            {
                log.WriteEntry(ex.StackTrace, EventLogEntryType.Error);
                log.WriteEntry(ex.Message, EventLogEntryType.Error);
                service = null;
                mouseGattCharacteristic = null;
                headsetGattCharacteristic = null;

                watcher.Stop();
                watcher.Start();
            }
        }

        private void Headset_OnBatteryNotification(object sender, BatteryEventArgs e)
        {
            try
            {
                var obj = sender as WirelessDevice;
                if (obj != null)
                {
                    if (headsetGattCharacteristic != null && bluetoothLEDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                    {
                        Task.Run(async () =>
                        {
                            await headsetGattCharacteristic.WriteValueAsync(CryptographicBuffer.ConvertStringToBinary(obj.BatteryLevel.ToString(), BinaryStringEncoding.Utf8));
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                log.WriteEntry(ex.StackTrace, EventLogEntryType.Error);
                log.WriteEntry(ex.Message, EventLogEntryType.Error);
                service = null;
                mouseGattCharacteristic = null;
                headsetGattCharacteristic = null;

                watcher.Stop();
                watcher.Start();

            }
        }
    }
}
