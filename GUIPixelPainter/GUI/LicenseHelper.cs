using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace GUIPixelPainter.GUI
{
    class LicenseHelper
    {
        private static string hwId = null;

        public static string GetHwId()
        {
            if (hwId != null)
                return hwId;

            string cpuid = GetCpuId();
            string diskid = GetDiskId();
            string videoid = GetVideoId();
            string mbid = GetMotherboardId();

            string result = String.Format("cpu: {0}\ndrive: {1}\ngpu: {2}\nmotherboard: {3}", cpuid, diskid, videoid, mbid);
            Logger.Info(result);

            byte[] buffer = Encoding.Default.GetBytes(result);
            string hexString;
            using (SHA256 hash = SHA256.Create())
            {
                hexString = BitConverter.ToString(hash.ComputeHash(buffer));
            }
            hexString = hexString.Replace("-", "");

            hwId = hexString;
            return hexString;
        }

        private static string GetMotherboardId()
        {
            return "model:" + GetProp("Win32_BaseBoard", "Model") + " manufacturer:" + GetProp("Win32_BaseBoard", "Manufacturer") + " name:" + GetProp("Win32_BaseBoard", "Name") + " serialnumber:" + GetProp("Win32_BaseBoard", "SerialNumber");
        }

        private static string GetVideoId()
        {
            return "name:" + GetProp("Win32_VideoController", "Name");
        }

        private static string GetDiskId()
        {
            return "model:" + GetProp("Win32_DiskDrive", "Model") + " manufacturer:" + GetProp("Win32_DiskDrive", "Manufacturer") + " totalheads:" + GetProp("Win32_DiskDrive", "TotalHeads");
        }

        private static string GetCpuId()
        {
            string retVal = GetProp("Win32_Processor", "UniqueId");
            if (retVal != "") return retVal;
            retVal = GetProp("Win32_Processor", "ProcessorId");
            if (retVal != "") return retVal;
            retVal = GetProp("Win32_Processor", "Name");
            if (retVal == "") //If no Name, use Manufacturer
            {
                retVal = GetProp("Win32_Processor", "Manufacturer");
            }
            return retVal;
        }

        private static string GetProp(string wmiClass, string wmiProperty)
        {
            ManagementObjectCollection mbsList = null;
            ManagementObjectSearcher mbs = new ManagementObjectSearcher(String.Format("Select {0} From {1}", wmiProperty, wmiClass));
            mbsList = mbs.Get();
            string prop = "";
            foreach (ManagementObject mo in mbsList)
            {
                try
                {
                    prop = mo[wmiProperty].ToString();
                    break;
                }
                catch
                {
                }
            }
            return prop;
        }

        private static byte[] StringToByteArray(String hex)
        {
            int NumberChars = hex.Length;
            byte[] bytes = new byte[NumberChars / 2];
            for (int i = 0; i < NumberChars; i += 2)
                bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
            return bytes;
        }

        public static void SaveKey(string key)
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter");
            string licensePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter/license");
            Directory.CreateDirectory(folderPath);
            File.WriteAllText(licensePath, key);
        }

        public static string LoadSavedKey()
        {
            string folderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PixelPainter/license");
            if (File.Exists(folderPath))
                return File.ReadAllText(folderPath);
            return "";
        }

        public static bool CheckKey(string hwId, string key)
        {
            if (hwId == null || key == null)
                return false;
            if (key.Length % 2 != 0 || key.Where((a) => !(char.IsDigit(a) || a == 'A' || a == 'B' || a == 'C' || a == 'D' || a == 'E' || a == 'F')).Count() > 0)
                return false;

            RSAParameters rsaKeyInfo = new RSAParameters();
            rsaKeyInfo.Modulus = Convert.FromBase64String("4/aCtSszqKU8IndYtgBhyNCojH2aFmCGy/W9TXzVweSyoGdCrhi0dTvv1h7XA6ye4gIl4BpAG0KgmE/0lYsYnPqKs9lHOu6PG5JNQz8eKVl5rQGhKvtGI+8sH3PnsRC5E5+Md/BrNf0ibBo2v9mo1/EkpKnlgcC4F3ZA6CDkHh0=");
            rsaKeyInfo.Exponent = Convert.FromBase64String("AQAB");

            RSACryptoServiceProvider rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(rsaKeyInfo);
            RSAPKCS1SignatureDeformatter rsaDeformatter = new RSAPKCS1SignatureDeformatter(rsa);
            rsaDeformatter.SetHashAlgorithm("SHA256");
            return rsaDeformatter.VerifySignature(StringToByteArray(hwId), StringToByteArray(key));
        }
    }
}
