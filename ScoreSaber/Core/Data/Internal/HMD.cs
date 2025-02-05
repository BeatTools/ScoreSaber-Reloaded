﻿#pragma warning disable IDE1006 // Naming Styles

#region

using System;
using System.Collections.Generic;
using UnityEngine.XR;

#endregion

namespace ScoreSaber.Core.Data.Internal {
    internal static class HMD {
        internal static readonly int Unknown = 0;
        internal static readonly int CV1 = 1;
        internal static readonly int Vive = 2;
        internal static readonly int VivePro = 4;
        internal static readonly int Windows = 8;
        internal static readonly int RiftS = 16;
        internal static readonly int Quest = 32;
        internal static readonly int Index = 64;
        internal static readonly int Cosmos = 128;

        internal static readonly string[] WMRBrands = {
            "lenovo",
            "microsoft",
            "acer",
            "dell",
            "acer",
            "wmr",
            "samsung",
            "asus",
            "reverb"
        };

        internal static int Get() {
            try {
                List<InputDevice> inputDevices = new List<InputDevice>();
                InputDevices.GetDevices(inputDevices);
                foreach (InputDevice device in inputDevices) {
                    if (device.name.ToLower().Contains("knuckles")) {
                        return Index;
                    }
                }
#pragma warning disable CS0618 // Type or member is obsolete
                return Get(XRDevice.model);
#pragma warning restore CS0618 // Type or member is obsolete
            } catch (Exception) {
                return 0;
            }
        }

        private static int Get(string hmdName) {
            string hmd = hmdName.ToLower();
            if (hmd.Contains("vive")) {
                if (hmd.Contains("pro")) {
                    return VivePro;
                }

                if (hmd.Contains("cosmos")) {
                    return Cosmos;
                }

                return Vive;
            }

            if (hmd.Contains("quest")) {
                return Quest;
            }

            if (hmd.Contains("oculus")) {
                if (hmd.Contains("cv1")) {
                    return CV1;
                }

                if (hmd.Contains("quest")) {
                    return Quest;
                }

                return RiftS;
            }

            if (hmdName.ToLower().Contains("index")) {
                return Index;
            }

            foreach (string brand in WMRBrands) {
                if (hmdName.ToLower().Contains(brand)) {
                    return Windows;
                }
            }

            return Unknown;
        }

        internal static string GetFriendlyName(int hmd) {
            if (hmd == 0) { return "Unknown"; }

            if (hmd == 1) { return "Oculus Rift CV1"; }

            if (hmd == 2) { return "HTC VIVE"; }

            if (hmd == 4) { return "HTC VIVE Pro"; }

            if (hmd == 8) { return "Windows Mixed Reality"; }

            if (hmd == 16) { return "Oculus Rift S"; }

            if (hmd == 32) { return "Oculus Quest"; }

            if (hmd == 64) { return "Valve Index"; }

            if (hmd == 128) { return "HTC VIVE Cosmos"; }

            return "Unknown";
        }
    }
}