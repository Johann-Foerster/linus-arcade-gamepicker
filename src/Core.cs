using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;
using System.Runtime.InteropServices;

namespace Arcade {
    public class Exit {
        public string Title = "", Image = "", Target = "", Arguments = "", Folder = "";
    }
    public class ControllerExitButtons {
        public uint JoystickId;
        public uint Buttons;

        internal static bool IsButtonPair(uint buttons) {
            if (buttons == 0) return false;
            uint remaining = buttons & (buttons - 1);
            return remaining != 0 && (remaining & (remaining - 1)) == 0;
        }
    }
    public class Library {
        public static readonly string[] Positions = { "Upper left", "Upper right", "Lower left", "Lower right" };
        public Exit[] Exits = { new Exit(), new Exit(), new Exit(), new Exit() };
        public ControllerExitButtons[] GameExitButtons = new ControllerExitButtons[0];
        public static readonly string FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Linus Arcade", "games.xml");
        public static Library Read() {
            if (!File.Exists(FileName)) return new Library();
            using (var file = File.OpenRead(FileName)) {
                var library = (Library)new XmlSerializer(typeof(Library)).Deserialize(file);
                if (library.Exits == null || library.Exits.Length != 4 || Array.Exists(library.Exits, e => e == null))
                    throw new InvalidDataException("Four exit entries are required.");
                return library;
            }
        }
        public void Save() {
            Directory.CreateDirectory(Path.GetDirectoryName(FileName));
            string temp = FileName + "." + Guid.NewGuid().ToString("N");
            try {
                using (var file = File.Create(temp)) new XmlSerializer(typeof(Library)).Serialize(file, this);
                if (File.Exists(FileName)) File.Replace(temp, FileName, null);
                else File.Move(temp, FileName);
            } finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public static Process Start(Exit game) {
            string target = game.Target, args = game.Arguments, folder = game.Folder;
            if (String.IsNullOrWhiteSpace(target)) throw new InvalidOperationException("Assign a game in Arcade Setup.");
            if (!File.Exists(target)) throw new FileNotFoundException("Game file is missing.");
            if (Path.GetExtension(target).Equals(".lnk", StringComparison.OrdinalIgnoreCase)) {
                dynamic shell = Activator.CreateInstance(Type.GetTypeFromProgID("WScript.Shell"));
                try {
                    dynamic link = shell.CreateShortcut(target);
                    try {
                        target = Environment.ExpandEnvironmentVariables((string)link.TargetPath);
                        args = ((string)link.Arguments + " " + args).Trim();
                        if (String.IsNullOrWhiteSpace(folder)) folder = Environment.ExpandEnvironmentVariables((string)link.WorkingDirectory);
                    } finally { Marshal.FinalReleaseComObject(link); }
                } finally { Marshal.FinalReleaseComObject(shell); }
            }
            if (!File.Exists(target) || !Path.GetExtension(target).Equals(".exe", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Select an executable or a shortcut to one.");
            return Process.Start(new ProcessStartInfo(target, args) {
                WorkingDirectory = String.IsNullOrWhiteSpace(folder) ? Path.GetDirectoryName(target) : folder,
                UseShellExecute = false
            });
        }
    }
    static class Native {
        [DllImport("user32.dll")] internal static extern bool SetProcessDPIAware();
        [DllImport("user32.dll")] internal static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("kernel32.dll")] internal static extern uint SetThreadExecutionState(uint flags);
        [StructLayout(LayoutKind.Sequential)]
        struct Joystick {
            public uint size, flags, x, y, z, r, u, v, buttons, button, pov, reserved1, reserved2;
        }
        [StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
        struct JoyCaps {
            public ushort mid, pid;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string name;
            public uint xmin,xmax,ymin,ymax,zmin,zmax,buttons,periodMin,periodMax,rmin,rmax,umin,umax,vmin,vmax,caps,maxAxes,axes,maxButtons;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=32)] public string reg;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst=260)] public string oem;
        }
        [DllImport("winmm.dll")] static extern uint joyGetPosEx(uint id, ref Joystick state);
        [DllImport("winmm.dll", CharSet=CharSet.Unicode)] static extern uint joyGetDevCaps(UIntPtr id,out JoyCaps caps,uint size);
        internal const int JoystickSlots = 16;
        static readonly JoyCaps[] ranges = new JoyCaps[JoystickSlots];
        static readonly bool[] connected = new bool[JoystickSlots];
        static readonly int[] previous = new int[JoystickSlots];
        static int active;
        static int nextScan;
        internal static uint ReadButtons(uint id) {
            const uint returnButtons = 0x80;
            var state = new Joystick { size = 52, flags = returnButtons };
            return joyGetPosEx(id, ref state) == 0 ? state.buttons : 0;
        }
        internal static bool ExitButtonsHeld(ControllerExitButtons[] bindings) {
            if (bindings == null || bindings.Length != 2 || bindings[0] == null || bindings[1] == null ||
                bindings[0].JoystickId == bindings[1].JoystickId) return false;
            foreach (var binding in bindings) {
                if (binding.JoystickId >= ranges.Length || !ControllerExitButtons.IsButtonPair(binding.Buttons) ||
                    (ReadButtons(binding.JoystickId) & binding.Buttons) != binding.Buttons) return false;
            }
            return true;
        }
        internal static int Stick() {
            int first = 0, now = Environment.TickCount;
            bool scan=nextScan==0 || unchecked(now-nextScan)>=0;
            if(scan) nextScan=unchecked(now+1000);
            for (uint id=0; id<JoystickSlots; id++) {
                if (!connected[id] && !scan) continue;
                var s = new Joystick { size=52, flags=0xff };
                if (joyGetPosEx(id, ref s)!=0) { connected[id]=false; previous[id]=0; continue; }
                if (!connected[id]) connected[id]=joyGetDevCaps((UIntPtr)id,out ranges[id],(uint)Marshal.SizeOf(typeof(JoyCaps)))==0;
                var c=ranges[id];
                double x=(s.x-(c.xmax+(double)c.xmin)/2)/Math.Max(1,(c.xmax-(double)c.xmin)/2);
                double y=(s.y-(c.ymax+(double)c.ymin)/2)/Math.Max(1,(c.ymax-(double)c.ymin)/2);
                int d=0;
                if ((c.caps & 16)!=0 && s.pov<=35999) d=1+(int)((s.pov+4500)/9000)%4;
                else if (Math.Max(Math.Abs(x),Math.Abs(y))>.35) d=Math.Abs(x)>Math.Abs(y)?(x>0?2:4):(y>0?3:1);
                if (d!=0 && d!=previous[id]) active=(int)id;
                previous[id]=d;
                if (first==0) first=d;
            }
            return previous[active]!=0?previous[active]:first;
        }
    }
}

