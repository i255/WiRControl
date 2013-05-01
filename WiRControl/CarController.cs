using SharpDX.DirectInput;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WiRControl
{
    class CarController
    {
        Viewer viewer = new Viewer();
        //Viewer viewer2 = new Viewer();
        WiRC wirc;

        public void Run()
        {
            viewer.Show();
            //viewer2.Show();
            wirc = new WiRC();

            viewer.FormClosed += (s, e) => wirc.Disconnect();

            wirc.Connect();
            wirc.PictureRecieved += wirc_PictureRecieved;
            Task.Run(() => PollJoystick());

            Application.Run(viewer);

        }

        private void PollJoystick()
        {
            var gamePad = true;
            var directInput = new DirectInput();

            // Find a Joystick Guid
            var joystickGuid = Guid.Empty;

            foreach (var deviceInstance in directInput.GetDevices(DeviceType.Gamepad,
                        DeviceEnumerationFlags.AllDevices))
                joystickGuid = deviceInstance.InstanceGuid;

            // If Gamepad not found, look for a Joystick
            if (joystickGuid == Guid.Empty)
            {
                foreach (var deviceInstance in directInput.GetDevices(DeviceType.Joystick,
                        DeviceEnumerationFlags.AllDevices))
                    joystickGuid = deviceInstance.InstanceGuid;
                //gamePad = false;
            }

            // If Joystick not found, throws an error
            if (joystickGuid == Guid.Empty)
            {
                Console.WriteLine("No joystick/Gamepad found.");
                return;
            }

            // Instantiate the joystick
            var joystick = new Joystick(directInput, joystickGuid);

            Console.WriteLine("Found Joystick/Gamepad with GUID: {0}", joystickGuid);

            // Query all suported ForceFeedback effects
            var allEffects = joystick.GetEffects();
            foreach (var effectInfo in allEffects)
                Console.WriteLine("Effect available {0}", effectInfo.Name);

            // Set BufferSize in order to use buffered data.
            joystick.Properties.BufferSize = 128;

            // Acquire the joystick
            joystick.Acquire();

            // Poll events from joystick
            while (true)
            {
                joystick.Poll();
                var datas = joystick.GetBufferedData();
                foreach (var state in datas)
                {
                    if (gamePad)
                    {
                        if (state.Offset == JoystickOffset.Y) // gas
                            wirc.SetChannel(1, GetValue(state.Value, false, .18f, .16f));
                        if (state.Offset == JoystickOffset.Z) // steering
                            wirc.SetChannel(0, GetValue(state.Value, true, .6f, .6f));
                    }
                    else
                    {
                        if (state.Offset == JoystickOffset.Y) // gas
                            wirc.SetChannel(1, GetValue(state.Value, false, .16f, .15f));
                        if (state.Offset == JoystickOffset.X) // steering
                            wirc.SetChannel(0, GetValue(state.Value - trim, true, .6f, .6f));
                        if (state.Offset == JoystickOffset.Z) // steering
                            trim = state.Value;
                    }
                    Console.WriteLine(state);
                }
            }
        }
        int trim = 0;

        private float GetValue(int value, bool invert, float pos, float neg)
        {
            var val = (float)(value - (ushort.MaxValue / 2)) / ushort.MaxValue * 2;
            if (invert)
                val = -val;

            if (val >= 0)
                val = val * pos;
            else
                val = val * neg;
            return val;
        }

        void wirc_PictureRecieved(int id, System.Drawing.Image b)
        {
            b = new System.Drawing.Bitmap(b, new System.Drawing.Size(b.Width * 2, b.Height * 2));
            if (id == 0 && !viewer.IsDisposed)
                viewer.SetImage(b);
            //else
            //    viewer2.Invoke((Action)(() => viewer2.SetImage(b)));

        }
    }
}
