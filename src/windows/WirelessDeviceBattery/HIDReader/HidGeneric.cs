using HidLibrary;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HIDReader
{
    public class HidGeneric : IDisposable
    {
        HidDevice _device;

        public HidGeneric(int vendor, int product, int inArray)
        {
            var devices = HidDevices.Enumerate(vendor, product);

            if (devices.Count() > inArray)
            {
                _device = devices.ToArray()[inArray];

                _device.OpenDevice();
                _device.MonitorDeviceEvents = true;

            }
            else
            {
                throw new IndexOutOfRangeException();
            }
        }

        Func<HidReport, bool> _func;
        public void GetInfo(byte[] payload, Func<HidReport, bool> func)
        {
            _func = func;
            var report = new HidReport(payload.Length, new HidDeviceData(payload, HidDeviceData.ReadStatus.Success));

            _device.WriteReport(report, (success) =>
            {
                if (success)
                {
                    _device.ReadReport(OnResult);
                }
                else
                {
                    func(null);
                }
            });
        }

        private void OnResult(HidReport report)
        {
            if (_func(report))
                _device.ReadReport(OnResult);
        }

        public void Dispose()
        {
            if (_device != null)
            {
                _device.CloseDevice();
            }
        }
    }
}
