using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Drawing.Drawing2D;

namespace PlottingTools
{
    public partial class Plot3DMainForm : Form
    {
        Surface3DRenderer sr;
        
        public Plot3DMainForm()
        {
            InitializeComponent();
            sr = new Surface3DRenderer(10, 10, 5, 0, 0, ClientRectangle.Width, ClientRectangle.Height, 0.5, 0, 0);
            sr.ColorSchema = new ColorSchema(tbHue.Value);

            double[,] mesh = new double[30, 30];

            for (int x = 0; x < mesh.GetLength(0); x++)
            {
                for (int y = 0; y < mesh.GetLength(1); y++)
                {
                    mesh[x, y] = Math.Sin(x);
                }
            }

            sr.SetMesh(mesh);

            //sr.SetFunction("sin(x1)*cos(x2)/(sqrt(sqrt(x1*x1+x2*x2))+1)*10");
            Form1_Resize(null, null);
            ResizeRedraw = true;
            DoubleBuffered = true;
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(BackColor);
            sr.RenderSurface(e.Graphics);
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            sr.ReCalculateTransformationsCoeficients(70, 35, 40, 0, 0, ClientRectangle.Width, ClientRectangle.Height, 0.5, 0, 0);
        }

        private void tbHue_Scroll(object sender, EventArgs e)
        {
            sr.ColorSchema = new ColorSchema(tbHue.Value);
            Invalidate();
        }
    }
}
