using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Media;
using Windows.Media.Audio;
using Windows.Media.Capture;

namespace WirelessDeviceBatteryMonitorForm
{
    public class MuteStatusChecker
    {
        AudioStateMonitor audioStateMonitor;

        public bool Muted { set; get; }

        public MuteStatusChecker()
        {
            //string deviceId = Windows.Media.Devices.MediaDevice.GetDefaultAudioCaptureId(Windows.Media.Devices.AudioDeviceRole.Default);
            audioStateMonitor = AudioStateMonitor.CreateForCaptureMonitoring(MediaCategory.Communications);
            audioStateMonitor.SoundLevelChanged += MuteChanged;
        }

        private void MuteChanged(AudioStateMonitor sender, object args)
        {
            switch (sender.SoundLevel)
            {
                case SoundLevel.Full:
                    Muted = false;
                    break;
                case SoundLevel.Muted:
                    Muted = true;
                    break;
                case SoundLevel.Low:
                    break;
            }
        }
    }
}
