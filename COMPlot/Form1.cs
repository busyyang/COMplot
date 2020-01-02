using System;
using System.Collections.Generic;
using System.Data;
using System.Windows.Forms;
using System.IO.Ports;
using System.Threading;
using System.IO;
using DSPLib;
using System.Windows.Forms.DataVisualization.Charting;
using System.Numerics;



namespace COMPlot
{

    public partial class mainForm : Form
    {
        public static object locker = new object();//add a object as locker
        // set the serial port
        private int mbaudRate = 115200;
        private int mDataBits = 8;
        private double mStopBits = 1;
        private int mParity = 0;
        private string[] ports;
        private bool portIsOpen = false;
        IList<string> comList = new List<string>();

        //prepare the Datatable for plot
        DataTable saveDataTable = new DataTable();
        DataTable PlotDataTable = new DataTable();

        private List<byte> buffer = new List<byte>(300);
        int packSize = 100;
        int Count = 0;
        int numPerScreen = 1000;
        int delayTime = 50;

        // bool for display
        bool IsDisplay = false;



        private void SearchAndAddSerialPort()
        {
            ports = SerialPort.GetPortNames();
            foreach (var item in ports)
            {
                comList.Add(item);
            }
            cmbSP.DataSource = comList;
            // for convenient usage
            try
            {
                cmbSP.SelectedIndex = 1;
            }
            catch (Exception)
            {

                cmbSP.SelectedIndex = 0;
            }
            toolStripStatusLabel1.Text = "Serial Port refreshed";
        }

        public mainForm()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            this.Size = new System.Drawing.Size(900,800);
            SearchAndAddSerialPort();
            SetChart();
            SetDataTable();
            // set a timer to plot
            System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
            timer.Tick += Timer_Tick;
            timer.Interval = delayTime;
            timer.Start();
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (IsDisplay == false)
            {
                displayFromPlotDatatable();
            }
            else
            {

                displayFromSaveDataTable();
            }
        }

        private void btnOpen_Click(object sender, EventArgs e)
        {
            this.Size = new System.Drawing.Size(900, 800);
            //
            // set the interval of X Axis
            //
            chart1.ChartAreas["ChartArea1"].AxisX.Interval = 100;
            chart2.ChartAreas["ChartArea1"].AxisX.Interval = 100;
            chart3.ChartAreas["ChartArea1"].AxisX.Interval = 100;
            chart4.ChartAreas["ChartArea1"].AxisX.Interval = 100;


            //
            // set the color of series
            //
            chart1.Series["Series1"].Color = System.Drawing.Color.Black;
            chart2.Series["Series1"].Color = System.Drawing.Color.Black;
            chart3.Series["Series1"].Color = System.Drawing.Color.Black;
            chart4.Series["Series1"].Color = System.Drawing.Color.Black;

            IsDisplay = false;
            if (portIsOpen == false)
            {
                try
                {
                    myserialPort.PortName = cmbSP.SelectedValue.ToString();
                    myserialPort.BaudRate = mbaudRate;
                    myserialPort.Parity = (Parity)mParity;
                    myserialPort.DataBits = mDataBits;
                    myserialPort.StopBits = (StopBits)mStopBits;
                    myserialPort.Open();
                    myserialPort.DiscardInBuffer();
                    toolStripStatusLabel1.Text = "Seriel port opened";
                    btnSearch.Enabled = false;
                    cmbSP.Enabled = false;
                }
                catch (Exception)
                {
                    MessageBox.Show("Can't Open SerialPort!");
                    return;
                }
                btnOpen.Text = "Close";
                startToolStripMenuItem.Text = "End";
                portIsOpen = true;

            }
            else
            {
                try
                {
                    Thread.Sleep(50);
                    myserialPort.Close();
                    toolStripStatusLabel1.Text = "Seriel port closed";
                    btnSearch.Enabled = true;
                    cmbSP.Enabled = true;
                }
                catch (Exception)
                {
                    MessageBox.Show("Close failed");
                    return;
                }
                finally
                {
                    btnOpen.Text = "Open";
                    startToolStripMenuItem.Text = "Start";
                    portIsOpen = false;
                }
            }
        }

        private void btnSearch_Click(object sender, EventArgs e)
        {
            SearchAndAddSerialPort();
        }

        private void myserialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int n = myserialPort.BytesToRead;
                byte[] recvBuffer = new byte[n];
                if (n < 10)
                {
                    return;
                }
                myserialPort.Read(recvBuffer, 0, n);
                buffer.AddRange(recvBuffer);
                if (buffer.Count >= 2)
                {
                    if (!(buffer[0] == 0x7F && buffer[1] == 0xF7))
                    {
                        buffer.Clear();
                    }
                }
                while (buffer.Count >= packSize)
                {
                    byte[] recvBytes = new byte[300];
                    buffer.CopyTo(0, recvBytes, 0, packSize);
                    buffer.RemoveRange(0, packSize);
                    //double[,] save = new double[packSize / 10, 4];
                    lock (locker)
                    {
                        double a1, a2, a3, a4;

                        for (int i = 0; i < packSize / 10; i++)
                        {
                            if (PlotDataTable.Rows.Count > numPerScreen)
                            {
                                PlotDataTable.Rows.RemoveAt(0);
                            }
                            if (recvBytes[i * 10] == 0x7F && recvBytes[i * 10 + 1] == 0xF7)
                            {
                                DataRow dr = PlotDataTable.NewRow();
                                a1 = recvBytes[i * 10 + 2] * 256 + recvBytes[i * 10 + 3];
                                a2 = recvBytes[i * 10 + 4] * 256 + recvBytes[i * 10 + 5];
                                a3 = recvBytes[i * 10 + 6] * 256 + recvBytes[i * 10 + 7];
                                a4 = recvBytes[i * 10 + 8] * 256 + recvBytes[i * 10 + 9];
                                Count++;
                                dr[1] = a1 / 1023 * 5;
                                dr[2] = a2 / 1023 * 5;
                                dr[3] = a3 / 1023 * 5;
                                dr[4] = a4 / 1023 * 5;
                                dr[0] = Count / 1000.0;
                                // add newest row into dt for charting
                                PlotDataTable.Rows.Add(dr);
                                // add newest row into dtToTXT for storing
                                saveDataTable.ImportRow(PlotDataTable.Rows[PlotDataTable.Rows.Count - 1]);
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {

                throw;
            }

        }

        private void SetDataTable()
        {
            // add columns into PlotDataTable
            PlotDataTable.Columns.Add("XLabel");
            PlotDataTable.Columns.Add("XAxis");
            PlotDataTable.Columns.Add("YAxis");
            PlotDataTable.Columns.Add("ZAxis");
            PlotDataTable.Columns.Add("EAxis");
            // add columns into saveDataTable
            for (int i = 0; i < PlotDataTable.Columns.Count; i++)
            {
                saveDataTable.Columns.Add(PlotDataTable.Columns[i].ColumnName);
            }
            for (int i = 0; i < numPerScreen; i++)
            {
                DataRow dr = PlotDataTable.NewRow();
                dr[0] = i - numPerScreen + 1;
                dr[1] = 0;
                dr[2] = 0;
                dr[3] = 0;
                dr[4] = 0;
                PlotDataTable.Rows.Add(dr);
            }
        }
        private void SetChart()
        {
            //
            // set the interval of X Axis
            //
            chart1.ChartAreas["ChartArea1"].AxisX.Interval = 100;
            chart2.ChartAreas["ChartArea1"].AxisX.Interval = 100;
            chart3.ChartAreas["ChartArea1"].AxisX.Interval = 100;
            chart4.ChartAreas["ChartArea1"].AxisX.Interval = 100;
            //
            // add Y label for charts
            //
            Title title1 = new Title();
            Title title2 = new Title();
            Title title3 = new Title();
            Title title4 = new Title();
            // chart1
            title1.Name = "XAxis";
            title1.Text = "X Axis / V";
            title1.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chart1.Titles.Add(title1);
            chart1.Titles["XAxis"].Docking = Docking.Left;
            // chart2
            title2.Name = "YAxis";
            title2.Text = "Y Axis / V";
            title2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chart2.Titles.Add(title2);
            chart2.Titles["YAxis"].Docking = Docking.Left;
            // chart3
            title3.Name = "ZAxis";
            title3.Text = "Z Axis / V";
            title3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chart3.Titles.Add(title3);
            chart3.Titles["ZAxis"].Docking = Docking.Left;
            // chart4
            title4.Name = "EMGSignal";
            title4.Text = "EMG Signal / V";
            title4.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            chart4.Titles.Add(title4);
            chart4.Titles["EMGSignal"].Docking = Docking.Left;

            //
            // add X label for charts
            //
            Title titlex = new Title();            
            titlex.Name = "Axisx";
            titlex.Text = "Time / s";
            titlex.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            // chart1
            chart1.Titles.Add(titlex);
            chart1.Titles["Axisx"].Docking = Docking.Bottom;
            // chart2
            chart2.Titles.Add(titlex);
            chart2.Titles["Axisx"].Docking = Docking.Bottom;
            // chart3
            chart3.Titles.Add(titlex);
            chart3.Titles["Axisx"].Docking = Docking.Bottom;
            // chart4
            chart4.Titles.Add(titlex);
            chart4.Titles["Axisx"].Docking = Docking.Bottom;

            // set the Maximum and Minimum for Y Axis
            chart1.ChartAreas["ChartArea1"].AxisY.Maximum = 3.3;
            chart1.ChartAreas["ChartArea1"].AxisY.Minimum = 0;
            chart2.ChartAreas["ChartArea1"].AxisY.Maximum = 3.3;
            chart2.ChartAreas["ChartArea1"].AxisY.Minimum = 0;
            chart3.ChartAreas["ChartArea1"].AxisY.Maximum = 3.3;
            chart3.ChartAreas["ChartArea1"].AxisY.Minimum = 0;
            chart4.ChartAreas["ChartArea1"].AxisY.Maximum = 3.3;
            chart4.ChartAreas["ChartArea1"].AxisY.Minimum = 0;
        }

        private void displayFromPlotDatatable()
        {
            lock (locker)
            {
                chart1.Series["Series1"].Points.DataBind(PlotDataTable.AsEnumerable(), "XLabel", "XAxis", "");
                chart2.Series["Series1"].Points.DataBind(PlotDataTable.AsEnumerable(), "XLabel", "YAxis", "");
                chart3.Series["Series1"].Points.DataBind(PlotDataTable.AsEnumerable(), "XLabel", "ZAxis", "");
                chart4.Series["Series1"].Points.DataBind(PlotDataTable.AsEnumerable(), "XLabel", "EAxis", "");
            }
        }

        private void displayFromSaveDataTable()
        {
            lock (locker)
            {
                chart1.Series["Series1"].Points.DataBind(saveDataTable.AsEnumerable(), "XLabel", "XAxis", "");
                chart2.Series["Series1"].Points.DataBind(saveDataTable.AsEnumerable(), "XLabel", "YAxis", "");
                chart3.Series["Series1"].Points.DataBind(saveDataTable.AsEnumerable(), "XLabel", "ZAxis", "");
                chart4.Series["Series1"].Points.DataBind(saveDataTable.AsEnumerable(), "XLabel", "EAxis", "");
            }
        }

        private void btnClear_Click(object sender, EventArgs e)
        {
            lock (locker)
            {
                PlotDataTable.Rows.Clear();
                saveDataTable.Rows.Clear();
                Count = 0;
                for (int i = 0; i < numPerScreen; i++)
                {
                    DataRow dr = PlotDataTable.NewRow();
                    dr[0] = i - numPerScreen + 1;
                    dr[1] = 0;
                    dr[2] = 0;
                    dr[3] = 0;
                    dr[4] = 0;
                    PlotDataTable.Rows.Add(dr);
                }
                SpectrumClear();
                toolStripStatusLabel1.Text = "Data cleared";
            }
        }

        private void SpectrumClear()
        {
            chart5.Series["Series1"].Points.Clear();
            chart6.Series["Series1"].Points.Clear();
            chart7.Series["Series1"].Points.Clear();
            chart8.Series["Series1"].Points.Clear();
        }

        private void btnSave_Click(object sender, EventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog();
            dialog.Filter = "txt file|*.txt";
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                string filename = dialog.FileName;
                FileStream fs = File.Open(filename, FileMode.Create, FileAccess.Write);
                StreamWriter wr = new StreamWriter(fs);
                wr.WriteLine("Count"+'\t'+"X axis"+'\t'+"Y axis"+'\t'+"Z axis"+'\t'+"EMG");
                foreach (DataRow dr in saveDataTable.Rows)
                {
                    wr.WriteLine(dr[0].ToString() + '\t' + dr[1].ToString() + '\t' + dr[2].ToString() + '\t' + dr[3].ToString() + '\t' + dr[4].ToString());
                }
                wr.Flush();
                wr.Close();
                fs.Close();
            }
            toolStripStatusLabel1.Text = "data saved";
        }
        private void BaudRateChecked(ToolStripMenuItem sender)
        {
            toolStripMenuItem115200.Checked = false;
            toolStripMenuItem57600.Checked = false;
            toolStripMenuItem56000.Checked = false;
            toolStripMenuItem43000.Checked = false;
            toolStripMenuItem38400.Checked = false;
            toolStripMenuItem19200.Checked = false;
            toolStripMenuItem9600.Checked = false;
            switch (sender.Name)
            {
                case "toolStripMenuItem115200":
                    toolStripMenuItem115200.Checked = true;
                    mbaudRate = 115200;
                    break;
                case "toolStripMenuItem57600":
                    toolStripMenuItem57600.Checked = true;
                    mbaudRate = 57600;
                    break;
                case "toolStripMenuItem56000":
                    toolStripMenuItem56000.Checked = true;
                    mbaudRate = 56000;
                    break;
                case "toolStripMenuItem43000":
                    toolStripMenuItem43000.Checked = true;
                    mbaudRate = 43000;
                    break;
                case "toolStripMenuItem38400":
                    toolStripMenuItem38400.Checked = true;
                    mbaudRate = 38400;
                    break;
                case "toolStripMenuItem19200":
                    toolStripMenuItem19200.Checked = true;
                    mbaudRate = 19200;
                    break;
                case "toolStripMenuItem9600":
                    toolStripMenuItem9600.Checked = true;
                    mbaudRate = 9600;
                    break;
                default:
                    break;
            }

        }
        private void toolStripMenuItembaudRate_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem thisSender = sender as ToolStripMenuItem;
            BaudRateChecked(thisSender);
        }

        private void dataBitsChecked(ToolStripMenuItem sender)
        {
            toolStripMenuItem5bits.Checked = false;
            toolStripMenuItem6bits.Checked = false;
            toolStripMenuItem7bits.Checked = false;
            toolStripMenuItem8bits.Checked = false;
            switch (sender.Name)
            {
                case "toolStripMenuItem5bits":
                    toolStripMenuItem5bits.Checked = true;
                    mDataBits = 5;
                    break;
                case "toolStripMenuItem6bits":
                    toolStripMenuItem6bits.Checked = true;
                    mDataBits = 6;
                    break;
                case "toolStripMenuItem7bits":
                    toolStripMenuItem7bits.Checked = true;
                    mDataBits = 7;
                    break;
                case "toolStripMenuItem8bits":
                    toolStripMenuItem8bits.Checked = true;
                    mDataBits = 8;
                    break;                
                default:
                    break;
            }
        }

        private void toolStripMenuItemDataBits_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem thisSender = sender as ToolStripMenuItem;
            dataBitsChecked(thisSender);
        }



        private void StopBitsChecked(ToolStripMenuItem sender)
        {
            toolStripMenuItem1bit.Checked = false;
            toolStripMenuItem1halfBits.Checked = false;
            toolStripMenuItem2bits.Checked = false;
            switch (sender.Name)
            {
                case "toolStripMenuItem1bit":
                    toolStripMenuItem1bit.Checked = true;
                    mStopBits = 1;
                    break;
                case "toolStripMenuItem1halfBits":
                    toolStripMenuItem1halfBits.Checked = true;
                    mStopBits = 1.5;
                    break;
                case "toolStripMenuItem2bits":
                    toolStripMenuItem2bits.Checked = true;
                    mStopBits = 2;
                    break;
                default:
                    break;
            }

        }

        private void toolStripMenuItemStopBitsChecked_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem thisSender = sender as ToolStripMenuItem;
            StopBitsChecked(thisSender);
        }

        private void ParityChecked(ToolStripMenuItem sender)
        {
            noneToolStripMenuItem.Checked = false;
            evenToolStripMenuItem.Checked = false;
            oddToolStripMenuItem.Checked = false;
            markToolStripMenuItem.Checked = false;
            spaceToolStripMenuItem.Checked = false;
            switch (sender.Name)
            {
                case "noneToolStripMenuItem":
                    noneToolStripMenuItem.Checked = true;
                    mParity = 0;
                    break;
                case "evenToolStripMenuItem":
                    evenToolStripMenuItem.Checked = true;
                    mParity = 1;
                    break;
                case "oddToolStripMenuItem":
                    oddToolStripMenuItem.Checked = true;
                    mParity = 2;
                    break;
                case "markToolStripMenuItem":
                    markToolStripMenuItem.Checked = true;
                    mParity = 3;
                    break;
                case "spaceToolStripMenuItem":
                    spaceToolStripMenuItem.Checked = true;
                    mParity = 4;
                    break;
                default:
                    break;
            }
        }

        private void ToolStripMenuItemParityChecked_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem thisSender = sender as ToolStripMenuItem;
            ParityChecked(thisSender);
        }

        private void btnDisplay_Click(object sender, EventArgs e)
        {
            // prepare the chartarea for display
            chart1.ChartAreas["ChartArea1"].AxisX.Interval = Count / 10;
            chart2.ChartAreas["ChartArea1"].AxisX.Interval = Count / 10;
            chart3.ChartAreas["ChartArea1"].AxisX.Interval = Count / 10;
            chart4.ChartAreas["ChartArea1"].AxisX.Interval = Count / 10;

            IsDisplay = true;
            toolStripStatusLabel1.Text = "Finish";
        }

        private void btnSpectrum_Click(object sender, EventArgs e)
        {
            this.Size = new System.Drawing.Size(1220, 800);
            // prepare the charts........
            SepctrumChartsPrepare();
            // prepare the Spectrum......
            SpectrumPrepare((int)Chanels.XAxis, chart5);
            SpectrumPrepare((int)Chanels.YAxis, chart6);
            SpectrumPrepare((int)Chanels.ZAxis, chart7);
            SpectrumPrepare((int)Chanels.EMG, chart8);
            toolStripStatusLabel1.Text = "Finish";
        }


        private void SepctrumChartsPrepare()
        {
            //
            // set the interval of X Axis
            //
            chart5.Series["Series1"].Color = System.Drawing.Color.Black;
            chart6.Series["Series1"].Color = System.Drawing.Color.Black;
            chart7.Series["Series1"].Color = System.Drawing.Color.Black;
            chart8.Series["Series1"].Color = System.Drawing.Color.Black;

            //
            // set the interval of X Axis
            //
            // chart5
            chart5.ChartAreas["ChartArea1"].AxisX.Minimum = 0;
            chart5.ChartAreas["ChartArea1"].AxisX.Maximum = 200;
            chart5.ChartAreas["ChartArea1"].AxisX.Interval = 40;
            // chart6
            chart6.ChartAreas["ChartArea1"].AxisX.Minimum = 0;
            chart6.ChartAreas["ChartArea1"].AxisX.Maximum = 200;
            chart6.ChartAreas["ChartArea1"].AxisX.Interval = 40;
            // chart7
            chart7.ChartAreas["ChartArea1"].AxisX.Minimum = 0;
            chart7.ChartAreas["ChartArea1"].AxisX.Maximum = 200;
            chart7.ChartAreas["ChartArea1"].AxisX.Interval = 40;
            // chart8
            chart8.ChartAreas["ChartArea1"].AxisX.Minimum = 0;
            chart8.ChartAreas["ChartArea1"].AxisX.Maximum = 200;
            chart8.ChartAreas["ChartArea1"].AxisX.Interval = 40;

            //
            // add Y label for charts
            //
            chart5.Titles.Clear();
            chart6.Titles.Clear();
            chart7.Titles.Clear();
            chart8.Titles.Clear();
            Title titleSpectrumY = new Title();
            titleSpectrumY.Name = "titleSpectrumY";
            titleSpectrumY.Text = "Ampitude / V";
            titleSpectrumY.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            // chart5
            chart5.Titles.Add(titleSpectrumY);
            chart5.Titles["titleSpectrumY"].Docking = Docking.Left;
            // chart6
            chart6.Titles.Add(titleSpectrumY);
            chart6.Titles["titleSpectrumY"].Docking = Docking.Left;
            // chart7
            chart7.Titles.Add(titleSpectrumY);
            chart7.Titles["titleSpectrumY"].Docking = Docking.Left;
            // chart8
            chart8.Titles.Add(titleSpectrumY);
            chart8.Titles["titleSpectrumY"].Docking = Docking.Left;
            //
            // add X label for charts
            //
            Title titleSpectrumX = new Title();
            titleSpectrumX.Name = "titleSpectrumX";
            titleSpectrumX.Text = "Frequency / Hz";
            titleSpectrumX.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            // chart5
            chart5.Titles.Add(titleSpectrumX);
            chart5.Titles["titleSpectrumX"].Docking = Docking.Bottom;
            // chart6
            chart6.Titles.Add(titleSpectrumX);
            chart6.Titles["titleSpectrumX"].Docking = Docking.Bottom;
            // chart7
            chart7.Titles.Add(titleSpectrumX);
            chart7.Titles["titleSpectrumX"].Docking = Docking.Bottom;
            // chart8
            chart8.Titles.Add(titleSpectrumX);
            chart8.Titles["titleSpectrumX"].Docking = Docking.Bottom;
        }

        private void SpectrumPrepare(int chanel,Chart PlotChart)
        {
            uint N = Convert.ToUInt32(saveDataTable.Rows.Count);
            double[] orignalData = new double[N];
            double samplingRateHz = 1000;
            string windowFFT = "Hamming";
            DSP.Window.Type windowToApply = (DSP.Window.Type)Enum.Parse(typeof(DSP.Window.Type), windowFFT);
            double[] wc = DSP.Window.Coefficients(windowToApply, N);
            double windowScaleFactor = DSP.Window.ScaleFactor.Signal(wc);
            // the sum of N and zeros must be power of 2.
            int pow2 = 1;
            while (N > Math.Pow(2, pow2))
            {
                pow2++;
            }
            uint zeros = Convert.ToUInt32(Math.Pow(2, pow2)) - N;
            // Instantiate & Initialize the FFT class
            FFT fft = new FFT();
            fft.Initialize(N, zeros);
            foreach (DataRow dr in saveDataTable.Rows)
            {
                // copy saveDataTable to array "orignailData"
                orignalData[saveDataTable.Rows.IndexOf(dr)] = Convert.ToDouble(dr[chanel]);
            }
            // remove the mean
            orignalData = DSP.Math.RemoveMean(orignalData);
            // Calculate the frequency span
            double[] fSpan = fft.FrequencySpan(samplingRateHz);
            // Convert and Plot Log Magnitude
            Complex[] cpxResult = fft.Execute(orignalData);

            double[] magResult = DSP.ConvertComplex.ToMagnitude(cpxResult);
            magResult = DSP.Math.Multiply(magResult, windowScaleFactor);
            double[] magLog = DSP.ConvertMagnitude.ToMagnitudeDBV(magResult);

            PlotChart.Series["Series1"].Points.Clear();
            PlotChart.Series["Series1"].Points.DataBindXY(fSpan,magResult);
        }

        private enum Chanels
        {
            count = 0,
            XAxis = 1,
            YAxis = 2,
            ZAxis = 3,
            EMG = 4
        } 

    }
}
