using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Management;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;
using Timer = System.Windows.Forms.Timer;

namespace WindowsFormsApplication
{
    public partial class USBSecure : Form
    {
        public USBSecure()
        {
            InitializeComponent();
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr SecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
        );

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            IntPtr lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped
        );

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice,
            uint dwIoControlCode,
            byte[] lpInBuffer,
            uint nInBufferSize,
            IntPtr lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped
        );

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private IntPtr handle = IntPtr.Zero;

        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const int FILE_SHARE_READ = 0x1;
        const int FILE_SHARE_WRITE = 0x2;
        const int FSCTL_LOCK_VOLUME = 0x00090018;
        const int FSCTL_DISMOUNT_VOLUME = 0x00090020;
        const int IOCTL_STORAGE_EJECT_MEDIA = 0x2D4808;
        const int IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;

        /// <summary>
        /// Constructor for the USBEject class
        /// </summary>
        /// <param name="driveLetter">This should be the drive letter. Format: F:/, C:/..</param>

        public IntPtr USBEject(string driveLetter)
        {
            string filename = @"\\.\" + driveLetter[0] + ":";
            return CreateFile(filename, GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, 0x3, 0, IntPtr.Zero);
        }

        public bool Eject(IntPtr handle)
        {
            bool result = false;

            if (LockVolume(handle) && DismountVolume(handle))
            {
                PreventRemovalOfVolume(handle, false);
                result = AutoEjectVolume(handle);
            }
            CloseHandle(handle);
            return result;
        }

        private bool LockVolume(IntPtr handle)
        {
            uint byteReturned;

            for (int i = 0; i < 10; i++)
            {
                if (DeviceIoControl(handle, FSCTL_LOCK_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out byteReturned, IntPtr.Zero))
                {
                    System.Windows.Forms.MessageBox.Show("Lock success!");
                    return true;
                }
                Thread.Sleep(500);
            }
            return false;
        }

        private bool PreventRemovalOfVolume(IntPtr handle, bool prevent)
        {
            byte[] buf = new byte[1];
            uint retVal;

            buf[0] = (prevent) ? (byte)1 : (byte)0;
            return DeviceIoControl(handle, IOCTL_STORAGE_MEDIA_REMOVAL, buf, 1, IntPtr.Zero, 0, out retVal, IntPtr.Zero);
        }

        private bool DismountVolume(IntPtr handle)
        {
            uint byteReturned;
            return DeviceIoControl(handle, FSCTL_DISMOUNT_VOLUME, IntPtr.Zero, 0, IntPtr.Zero, 0, out byteReturned, IntPtr.Zero);
        }

        private bool AutoEjectVolume(IntPtr handle)
        {
            uint byteReturned;
            return DeviceIoControl(handle, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0, IntPtr.Zero, 0, out byteReturned, IntPtr.Zero);
        }

        private bool CloseVolume(IntPtr handle)
        {
            return CloseHandle(handle);
        }

        //-----------------------------------------------------------------------------------------------------------------------

        #region
        /// <summary>
        /// Регистрируем событие USB
        /// </summary>
        class USBConnectionsChecker
        {

            private int devicesCount;
            private Timer updatingInformationTimer;

            public event EventHandler DeviceConnected;
            public event EventHandler DeviceDisconnected;

            private int GetDevicesCount()
            {
                return ((new ManagementObjectSearcher(@"select * from Win32_USBHub")).Get()).Count;
            }

            private void UpdatingInformationTimer_Tick(object sender, EventArgs e)
            {
                int newDevicesCountValue = GetDevicesCount();

                if (newDevicesCountValue != devicesCount)
                {
                    if (newDevicesCountValue > devicesCount)
                    {
                        devicesCount = newDevicesCountValue;
                        DeviceConnected(this, null);
                    }
                    else
                    {
                        devicesCount = newDevicesCountValue;
                        DeviceDisconnected(this, null);
                    }
                }
            }

            public USBConnectionsChecker()
            {
                devicesCount = GetDevicesCount();
                updatingInformationTimer = new Timer();
                updatingInformationTimer.Tick += new EventHandler(this.UpdatingInformationTimer_Tick);
                updatingInformationTimer.Interval = 1000;
                updatingInformationTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Событие включение флешки
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UsbConnectionsChecker_DeviceConnected(object sender, System.EventArgs e)
        {
            // Отключаем все флешки
            if (checkBox1.Checked)
            {
                return;
            }

            var usbDevices = GetUSBDevices();
            foreach (var usbDevice in usbDevices)
            {
                var sn = usbDevice.PnpDeviceID.ToString().Split('&').First();
                if (sn == textBox2.Text)
                {
                    textBox1.AppendText($"PNP {sn} - SN {usbDevice.SerialNumber} ===== Есть в базе !");
                    textBox1.AppendText(Environment.NewLine);
                }
                else textBox1.AppendText($"PNP {sn} - SN " + usbDevice.SerialNumber);
                // $"Device ID: {usbDevice.DeviceID}, PNP Device ID: {usbDevice.PnpDeviceID}, Description: {usbDevice.Description}");
                textBox1.AppendText(Environment.NewLine);
            }
        }

        /// <summary>
        /// Событие отключение флешки
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void UsbConnectionsChecker_DeviceDisconnected(object sender, System.EventArgs e)
        {
            textBox1.AppendText("Один из USB-девайсов был отключен!"); 
            textBox1.AppendText(Environment.NewLine);
        }

        private void CheckUSBConnections_Click(object sender, EventArgs e)
        {
            UsbConnectionsChecker_DeviceConnected(sender, e);

            USBConnectionsChecker usbConnectionsChecker = new USBConnectionsChecker();
            usbConnectionsChecker.DeviceConnected += new EventHandler(UsbConnectionsChecker_DeviceConnected);
            usbConnectionsChecker.DeviceDisconnected += new EventHandler(UsbConnectionsChecker_DeviceDisconnected);
        }
        #endregion

        static List<USBDeviceInfo> GetUSBDevices()
        {
            List<USBDeviceInfo> devices = new List<USBDeviceInfo>();
            var searcher = new ManagementObjectSearcher("root\\CIMV2",
                @"SELECT * FROM Win32_DiskDrive WHERE InterfaceType='USB'");
            ManagementObjectCollection collection = searcher.Get();
            foreach (var device in collection)
            {
                devices.Add(new USBDeviceInfo(
                    (string)device.GetPropertyValue("DeviceID"),
                    (string)device.GetPropertyValue("SerialNumber"),
                    (string)device.GetPropertyValue("PNPDeviceID").ToString().Split('\\').Last(),
                    (string)device.GetPropertyValue("Description")
                    )); ;
            }
            return devices;
        }

        class USBDeviceInfo
        {
            public USBDeviceInfo(string deviceID, string serialNumber, string pnpDeviceID, string description)
            {
                this.DeviceID = deviceID;
                this.SerialNumber = serialNumber;
                this.PnpDeviceID = pnpDeviceID;
                this.Description = description;
            }

            public string DeviceID { get; private set; }
            public string SerialNumber { get; private set; }
            public string PnpDeviceID { get; private set; }
            public string Description { get; private set; }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //EjectDrive(tbNameDisk.Text);
        }

        /*
        const uint GENERIC_READ = 0x80000000;
        const uint GENERIC_WRITE = 0x40000000;
        const int FILE_SHARE_READ = 0x1;
        const int FILE_SHARE_WRITE = 0x2;
        const int FSCTL_LOCK_VOLUME = 0x00090018;
        const int FSCTL_DISMOUNT_VOLUME = 0x00090020;
        const int IOCTL_STORAGE_EJECT_MEDIA = 0x2D4808;
        const int IOCTL_STORAGE_MEDIA_REMOVAL = 0x002D4804;

        void EjectDrive(string driveLetter)
        {
            string path = @"\\.\" + driveLetter + @":";
            IntPtr handle = CreateFile(path, GENERIC_READ | GENERIC_WRITE,
            FILE_SHARE_READ | FILE_SHARE_WRITE, IntPtr.Zero, 0x3, 0, IntPtr.Zero);

            if ((long)handle == -1)
            {
                textBox1.AppendText("Unable to open drive " + driveLetter);
                textBox1.AppendText(Environment.NewLine);
                return;
            }

            int dummy = 0;

            DeviceIoControl(handle, IOCTL_STORAGE_EJECT_MEDIA, IntPtr.Zero, 0,
                IntPtr.Zero, 0, ref dummy, IntPtr.Zero);

            CloseHandle(handle);

            textBox1.AppendText("OK to remove drive.");
            textBox1.AppendText(Environment.NewLine);
        }
        [DllImport("kernel32", SetLastError = true)]
        private static extern IntPtr CreateFile
            (string filename, uint desiredAccess,
                uint shareMode, IntPtr securityAttributes,
                int creationDisposition, int flagsAndAttributes,
                IntPtr templateFile);
        [DllImport("kernel32")]
        private static extern int DeviceIoControl
            (IntPtr deviceHandle, uint ioControlCode,
                IntPtr inBuffer, int inBufferSize,
                IntPtr outBuffer, int outBufferSize,
                ref int bytesReturned, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);
        */
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if(checkBox1.Checked)
            {

            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            Eject(USBEject("G:"));
        }
    }
}
