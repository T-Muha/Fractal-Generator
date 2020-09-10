using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Interop;
using Microsoft.Win32;
using System.Diagnostics;

//References
//HSV->RGB: https://stackoverflow.com/questions/1335426/is-there-a-built-in-c-net-system-api-for-hsv-to-rgb
//Mandelbrot Renormalization: http://linas.org/art-gallery/escape/escape.html

//0.372138, 0.090397


namespace Mandelbrot {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window {

        public MainWindow()
        {
            InitializeComponent();
            fractalType.Items.Add("Mandelbrot");
            fractalType.Items.Add("Mandelbar");
            fractalType.Items.Add("Burning Ship");
            //Generate_Fractal(new object(), new RoutedEventArgs());
        }

        //Complex number with associated operations
        public struct Complex {
            public double i;
            public double r;
            public Complex(double real, double imaginary) {
                this.r = real;
                this.i = imaginary;
            }
            public static Complex operator +(Complex one, Complex two) {
                return new Complex(one.r + two.r, one.i + two.i);
            }
            public static Complex operator *(Complex one, Complex two) {
                return new Complex(one.r * two.r - one.i * two.i, one.r * two.i + one.i * two.r);
            }
            public override string ToString() {
                return (String.Format("{0} + {1}i", r, i));
            }
            public double Absolute() {
                return Math.Sqrt(r * r + i * i);
            }
            public double Normal()
            {
                return r * r + i * i;
            }
            public Complex NumAbsolute()
            {
                return new Complex(Math.Abs(r), Math.Abs(i));
            }
            public Complex Conjugate()
            {
                return new Complex(r, i * -1);
            }
        }

        public struct DebugTimer
        {
            public string processName;
            public Stopwatch timer;
            
            public DebugTimer(string name)
            {
                this.processName = name;
                this.timer = new Stopwatch();
            }
            public void Start()
            {
                timer.Start();
            }
            public void Stop()
            {
                timer.Stop();
            }
            public void PrintTime()
            {
                Console.WriteLine(processName + ":   " + timer.Elapsed);
            }
        }

        DebugTimer coloring = new DebugTimer("Total Coloring");
        DebugTimer generation = new DebugTimer("Total Generation");
        DebugTimer mainCalculations = new DebugTimer("Main Calculations");

        //Generates the mandelbrot
        private void Generate_Fractal(object sender, RoutedEventArgs e)
        {
            generation.Start();
            //Define the properties of the mandelbrot
            double realRange = Convert.ToDouble(xRangeIn.Text);
            double complexRange = Convert.ToDouble(yRangeIn.Text);
            double realFocus = Convert.ToDouble(xFocusIn.Text);
            double complexFocus = Convert.ToDouble(yFocusIn.Text);
            double minReal = realFocus - realRange / 2;
            double minComplex = complexFocus - complexRange / 2;
            double maxComplex = complexFocus + complexRange / 2;
            double delta = Convert.ToDouble(deltaIn.Text);   //Distance between points
            int iterations = Convert.ToInt32(iterationsIn.Text);    //Attempts before declaring position bounded. Increases the contrast

            //Calculate size/density properties of the bitmap
            int numR = Convert.ToInt32(realRange / delta) + 1;
            int numC = Convert.ToInt32(complexRange / delta) + 1;
            int tempBitNumR = Convert.ToInt32(numR / 2);
            int tempBitNumC = Convert.ToInt32(numC / 2);
            int bitWidth = 499, bitHeight = 499;
            if (numR - numC > 0.5)
            {
                bitWidth = Convert.ToInt32(Convert.ToDouble(numR) / Convert.ToDouble(numC) * bitHeight);
                mImageView.Width = bitWidth;
            }
            else if (numC - numR > 0.5)
            {
                bitWidth = Convert.ToInt32(499 * numR / numC);
            }
            HwndSource windowHandleSource = PresentationSource.FromVisual(this) as HwndSource;
            double dpiX = numR * (96.0 * windowHandleSource.CompositionTarget.TransformToDevice.M11) / bitWidth;
            double dpiY = numC * (96.0 * windowHandleSource.CompositionTarget.TransformToDevice.M22) / bitHeight;

            //Create changeable bitmap and prepare app image
            WriteableBitmap bitImage = new WriteableBitmap(numR, numC, dpiX, dpiY, PixelFormats.Bgr32, null);

            //Iterate through the real axis
            double real = 0;
            double complex = 0;
            bool noMansLand = false;
            int noMansCounter = 0;
            int colorMemory = 0;
            int rgbColor = 0;
            for (int i = 0; i < numR; i++)
            {
                //Iterate through the complex axis
                real = minReal + i * delta;
                for (int j = 0; j < numC; j++)
                {
                    if (Convert.ToBoolean(doApproximate.IsChecked))
                    {
                        if (noMansLand && noMansCounter < 10)
                        {
                            noMansCounter++;
                            rgbColor = colorMemory;
                        }
                        else
                        {
                            if (noMansCounter == 10)
                            {
                                noMansLand = false;
                                noMansCounter = 0;
                            }
                            complex = maxComplex - delta - j * delta;
                            coloring.Start();
                            rgbColor = MandelbrotSeriesColor(new Complex(real, complex), iterations);
                            coloring.Stop();
                            if (rgbColor == colorMemory)
                            {
                                noMansLand = true;
                            }
                        }
                    }
                    else
                    {
                        complex = maxComplex - delta - j * delta;
                        coloring.Start();
                        rgbColor = MandelbrotSeriesColor(new Complex(real, complex), iterations);
                        coloring.Stop();
                    }

                    //Add the pixel to the writeablebitmap
                    try
                    {
                        bitImage.Lock();
                        unsafe
                        {
                            IntPtr pBitImage = bitImage.BackBuffer;
                            pBitImage += j * bitImage.BackBufferStride;
                            pBitImage += i * 4;
                            *((int*)pBitImage) = rgbColor;
                        }
                        bitImage.AddDirtyRect(new Int32Rect(i, j, 1, 1));
                    }
                    finally
                    {
                        bitImage.Unlock();
                    }
                }
            }
            //Assign the bitmap to the image; display the mandelbrot
            mImageView.Width = bitImage.Width;
            mImageView.Height = bitImage.Height;
            mImageView.Source = bitImage;
            generation.Stop();
            Console.WriteLine("------------------------------------------");
            mainCalculations.PrintTime();
            coloring.PrintTime();
            generation.PrintTime();
        }

        int MandelbrotSeriesColor(Complex coordinate, int iterations)
        {
            int counter = 0;
            Complex val = new Complex(0, 0);
            //Determine if the point is bounded
            mainCalculations.Start();
            switch (fractalType.Text)
            {
                case "Mandelbrot":
                    do
                    {
                        val = val * val + coordinate;
                        counter++;
                    } while (counter < iterations && val.Normal() < 4);
                    break;
                case "Burning Ship":
                    do
                    {
                        val = val.NumAbsolute() * val.NumAbsolute() + coordinate;
                        counter++;
                    } while (counter < iterations && val.Normal() < 4);
                    break;
                case "Mandelbar":
                    do
                    {
                        val = val.Conjugate();
                        val = val * val + coordinate;
                        counter++;
                    } while (counter < iterations && val.Normal() < 4);
                    break;
            }
            //Determine the pixel color based on the number of successful iterations
            if (Convert.ToBoolean(doRenormalize.IsChecked) && counter != iterations)
            {
                Renormalize(ref counter, val);
            }
            double hue = (Convert.ToDouble(iterations) - Convert.ToDouble(counter)) / Convert.ToDouble(iterations) * 360;
            double value = (counter == iterations) ? 0 : 100;
            int red, green, blue;
            HsvToRgb(hue, 100, value, out red, out green, out blue);
            int bitColor = red << 16;
            bitColor |= green << 8;
            bitColor |= blue << 0;
            return bitColor;
        }

        void Renormalize(ref int numIter, Complex finalVal)
        {
            switch (fractalType.Text)
            {
                case "Mandelbrot":
                    //double intermediateVal = Math.Log(finalVal.Absolute());
                    //intermediateVal = (intermediateVal < 0) ? 0.1 : intermediateVal;
                    //Console.WriteLine(numIter - Math.Log(Math.Abs(Math.Log(finalVal.Absolute())))/Math.Log(2.0));
                    numIter = Convert.ToInt32(numIter + 1 - Math.Log(Math.Log(finalVal.Absolute(),2)));
                    //numIter = Convert.ToInt32(numIter - Math.Log(intermediateVal)/Math.Log(2.0));
                    break;
                case "Burning Ship":
                    break;
            }
        }

        void Zoom(double x, double y, double direction)
        {
            double currentRangeX = Convert.ToDouble(xRangeIn.Text);
            double currentRangeY = Convert.ToDouble(yRangeIn.Text);
            double coordX = (x / mImageView.Width * currentRangeX - currentRangeX / 2) + Convert.ToDouble(xFocusIn.Text);
            double coordY = (currentRangeY - y / mImageView.Height * currentRangeY - currentRangeY / 2) + Convert.ToDouble(yFocusIn.Text);
            double zoomFactor = Math.Pow(zoomPowerIn.Value, direction); //Pretty proud of myself for this neat trick
            deltaIn.Text = Convert.ToBoolean(doParamScaling.IsChecked) ? Convert.ToString(Convert.ToDouble(deltaIn.Text) / zoomFactor) : deltaIn.Text;
            xFocusIn.Text = Convert.ToString(coordX);
            yFocusIn.Text = Convert.ToString(coordY);
            xRangeIn.Text = Convert.ToString(currentRangeX / zoomFactor);
            yRangeIn.Text = Convert.ToString(currentRangeY / zoomFactor);
            Generate_Fractal(new object(), new RoutedEventArgs());
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point position = e.GetPosition(mImageView);
            if (e.OriginalSource == mImageView && position.X > 0 && position.X < mImageView.Width && position.Y > 0 && position.Y < mImageView.Height)
            {
                Zoom(position.X, position.Y, 1);
            }
        }

        private void Window_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            Point position = e.GetPosition(mImageView);
            if (e.OriginalSource == mImageView && position.X > 0 && position.X < mImageView.Width && position.Y > 0 && position.Y < mImageView.Height)
            {
                Zoom(position.X, position.Y, -1);
            }
        }

        int PalleteTranslate(int rgbIn)
        {
            return 0;
        }

        //Not mine -- found at https://stackoverflow.com/questions/1335426/is-there-a-built-in-c-net-system-api-for-hsv-to-rgb
        //Didn't want to reinvent the HSV-RGB wheel
        void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {
                    // Red is the dominant color
                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    // Green is the dominant color
                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;
                    // Blue is the dominant color
                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;
                    // Red is the dominant color
                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;
                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.
                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;
                    // The color is not defined, we should throw an error.
                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            r = Clamp((int)(R * 255.0));
            g = Clamp((int)(G * 255.0));
            b = Clamp((int)(B * 255.0));
        }
        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }

        //File Functionalities
        public void Open_File(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openDialog = new OpenFileDialog();
            openDialog.DefaultExt = ".tom";
            Nullable<bool> dlgResult = openDialog.ShowDialog();
            if (dlgResult == true)
            {
                string[] fileData = System.IO.File.ReadAllLines(openDialog.FileName);
                //load preset data
                xRangeIn.Text = fileData[0];
                yRangeIn.Text = fileData[1];
                xFocusIn.Text = fileData[2];
                yFocusIn.Text = fileData[3];
                deltaIn.Text = fileData[4];
                iterationsIn.Text = fileData[5];
                Generate_Fractal(new object(), new RoutedEventArgs());
            }
        }

        public void Save_File(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            Nullable<bool> dlgResult = saveDialog.ShowDialog();
            if (dlgResult == true)
            {
                //prepare data into string for saving
                string[] fileSaveData = new string[6];
                fileSaveData[0] = Convert.ToString(xRangeIn.Text);
                fileSaveData[1] = Convert.ToString(yRangeIn.Text);
                fileSaveData[2] = Convert.ToString(xFocusIn.Text);
                fileSaveData[3] = Convert.ToString(yFocusIn.Text);
                fileSaveData[4] = Convert.ToString(deltaIn.Text);
                fileSaveData[5] = Convert.ToString(iterationsIn.Text);
                //save preset into a .tom structured text file
                System.IO.File.WriteAllLines(saveDialog.FileName + ".tom", fileSaveData);
            }
        }

        public void Exit_App(object sender, RoutedEventArgs e)
        {
            this.Close();
        }
    }
}