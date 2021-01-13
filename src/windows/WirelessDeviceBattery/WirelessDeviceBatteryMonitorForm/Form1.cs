using HIDReader;
using NAudio.CoreAudioApi;
using NAudio.Mixer;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Security.Cryptography;

namespace WirelessDeviceBatteryMonitorForm
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            instance = this;
        }

        G533 headset;
        SteelseriesRival650_Wireless mouse;
        static Form1 instance;
        BluetoothLEDevice bluetoothLEDevice;
        GattDeviceService service;
        GattCharacteristic mouseGattCharacteristic;
        GattCharacteristic headsetGattCharacteristic;

        MuteStatusChecker muteStatusChecker;

        private void Form1_Load(object sender, EventArgs e)
        {
            //go();
            new G533(10);
            muteStatusChecker = new MuteStatusChecker();
        }


        public void go()
        {
            var watcher = new BluetoothLEAdvertisementWatcher();

            watcher.ScanningMode = BluetoothLEScanningMode.Active;

            // Only activate the watcher when we're recieving values >= -80
            watcher.SignalStrengthFilter.InRangeThresholdInDBm = -90;

            // Stop watching if the value drops below -90 (user walked away)
            watcher.SignalStrengthFilter.OutOfRangeThresholdInDBm = -95;

            // Register callback for when we see an advertisements
            watcher.Received += OnAdvertisementReceived;

            // Wait 5 seconds to make sure the device is really out of range
            watcher.SignalStrengthFilter.OutOfRangeTimeout = TimeSpan.FromMilliseconds(5000);
            watcher.SignalStrengthFilter.SamplingInterval = TimeSpan.FromMilliseconds(15000);

            watcher.AdvertisementFilter.Advertisement.ServiceUuids.Clear();
            watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(Guid.Parse("4dd08908-637c-4d4e-95ca-90b84fe2fcea"));

            //watcher.AdvertisementFilter = ;


            // Starting watching for advertisements
            watcher.Start();
        }

        private void OnAdvertisementReceived(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            MethodInvoker action = delegate
            {

                instance.richTextBox1.AppendText(String.Format("Advertisement:") + "\r\n");
                instance.richTextBox1.AppendText(String.Format("  BT_ADDR: {0}", eventArgs.BluetoothAddress) + "\r\n");
                instance.richTextBox1.AppendText(String.Format("  FR_NAME: {0}", eventArgs.Advertisement.LocalName) + "\r\n");
                instance.richTextBox1.AppendText("\r\n");
            };
            instance.richTextBox1.BeginInvoke(action);

            if (eventArgs.BluetoothAddress == 154664818668418)
            {
                Task.WaitAll(ConnectToDevice(watcher, eventArgs));

                if (watcher.Status == BluetoothLEAdvertisementWatcherStatus.Stopped)
                {
                    headset = new G533(10);
                    headset.OnBatteryNotification += Headset_OnBatteryNotification;

                    mouse = new SteelseriesRival650_Wireless(10);
                    mouse.OnBatteryNotification += Mouse_OnBatteryNotification;
                }
            }
        }

        private async Task ConnectToDevice(BluetoothLEAdvertisementWatcher watcher, BluetoothLEAdvertisementReceivedEventArgs eventArgs)
        {
            watcher.Stop();

            Thread.Sleep(1000);

            MethodInvoker action = async delegate
            {
                try
                {

                    bluetoothLEDevice = await BluetoothLEDevice.FromBluetoothAddressAsync(eventArgs.BluetoothAddress);

                    //var services = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
                    Thread.Sleep(1000);

                    service = (await bluetoothLEDevice.GetGattServicesForUuidAsync(Guid.Parse("4dd08908-637c-4d4e-95ca-90b84fe2fcea".ToUpper())))?.Services?.Single();

                    mouseGattCharacteristic = (await service.GetCharacteristicsForUuidAsync(Guid.Parse("8f43ee07-60d0-4f43-a350-39c8fdf6dc5d")))?.Characteristics?.Single();
                    headsetGattCharacteristic = (await service.GetCharacteristicsForUuidAsync(Guid.Parse("e54807c3-f811-4a63-8c0a-983f7ccea869")))?.Characteristics?.Single();
                }
                catch (Exception ex)
                {
                    instance.richTextBox1.AppendText(ex.Message + "\r\n");
                    watcher.Start();
                }

            };

            instance.richTextBox1.BeginInvoke(action);
        }

        private void Mouse_OnBatteryNotification(object sender, BatteryEventArgs e)
        {
            var obj = sender as WirelessDevice;
            if (obj != null)
            {
                MethodInvoker action = null;

                if (obj.BatteryLevel != 9999)
                    action = delegate { instance.textBox1.Text = $"{obj.BatteryLevel.ToString()}%"; };
                else
                    action = delegate { instance.textBox1.Text = "Carregando"; };

                instance.textBox1.BeginInvoke(action);

                if (instance.mouseGattCharacteristic != null)
                {
                    instance.mouseGattCharacteristic.WriteValueAsync(CryptographicBuffer.ConvertStringToBinary(obj.BatteryLevel.ToString(), BinaryStringEncoding.Utf8));
                }

            }
        }

        private void Headset_OnBatteryNotification(object sender, BatteryEventArgs e)
        {
            var obj = sender as WirelessDevice;
            if (obj != null)
            {
                MethodInvoker action = null;

                if (obj.BatteryLevel != 9999)
                    action = delegate { instance.textBox2.Text = $"{obj.BatteryLevel.ToString()}%"; };
                else
                    action = delegate { instance.textBox2.Text = "Carregando"; };

                instance.textBox2.BeginInvoke(action);

                if (instance.mouseGattCharacteristic != null)
                {
                    instance.headsetGattCharacteristic.WriteValueAsync(CryptographicBuffer.ConvertStringToBinary(obj.BatteryLevel.ToString(), BinaryStringEncoding.Utf8));
                }
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            
        }
    }
}
