﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace WiRControl
{
    public partial class Viewer : Form
    {
        public Viewer()
        {
            InitializeComponent();
        }

        public void SetImage(Image img)
        {
            try
            {
                Invoke((Action)(() => pictureBox1.Image = img));
            }
            catch { }
        }
    }
}
