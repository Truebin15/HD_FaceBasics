using System;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using Microsoft.Kinect;
using Microsoft.Kinect.Face;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Windows.Controls;
using System.IO;

namespace Kinect2FaceHD_NET
{
    public partial class MainWindow : Window
    {
        private KinectSensor _sensor = null;

        private Body[] bodies = null;

        private BodyFrameSource _bodySource = null;

        private BodyFrameReader _bodyReader = null;

        private HighDefinitionFaceFrameSource[] _faceSource = null;

        private HighDefinitionFaceFrameReader[] _faceReader = null;

        private FaceAlignment[] _faceAlignment = null;

        private FaceModel[] _faceModel = null;

        private List<Ellipse>[] _points = null; //new List<Ellipse>();

        private int bodyCount;

        private int displayHeight;

        private int displayWidth;

        private void CreateLogFile()
        {
            double startTime = DateTime.Now.Subtract(DateTime.Parse("1970-1-1")).TotalMilliseconds;
            this.fileName = "HDFaceBasics-log"+ startTime.ToString() + ".csv";
            
            //Initialize log file*********************************************
            string entries = "UserID,Date,Time,";
            int num = 1347;

            for (int i = 0; i < num; i++)
            {
                if (i != num - 1)
                {
                    entries += "X" + i.ToString() + ",Y" + i.ToString() + ",";
                }
                else
                {
                    entries += "X" + i.ToString() + ",Y" + i.ToString() + "\n";
                }
            }

            using (StreamWriter sw = new StreamWriter(this.fileName))
            {
                sw.Write(entries);
            }
            //Initialize log file**********************************************
        }

        public MainWindow()
        {
            this._sensor = KinectSensor.GetDefault();

            //Create the log file
            this.CreateLogFile();

            FrameDescription framedis = this._sensor.ColorFrameSource.FrameDescription;
            this.displayHeight = framedis.Height;
            this.displayWidth = framedis.Width;

            this._bodySource = this._sensor.BodyFrameSource;
            this._bodyReader = this._bodySource.OpenReader();
            this._bodyReader.FrameArrived += this.BodyReader_FrameArrived;

            this.bodyCount = this._bodySource.BodyCount;

            this.bodies = new Body[this.bodyCount];

            this._faceSource = new HighDefinitionFaceFrameSource[this.bodyCount]; //(_sensor);
            this._faceReader = new HighDefinitionFaceFrameReader[this.bodyCount];
            this._points = new List<Ellipse>[this.bodyCount];

            for (int i = 0; i < this.bodyCount; i++)
            {
                this._faceSource[i] = new HighDefinitionFaceFrameSource(this._sensor);
                this._faceReader[i] = this._faceSource[i].OpenReader();
                this._points[i] = new List<Ellipse>();
            }

            this._faceAlignment = new FaceAlignment[this.bodyCount];
            this._faceModel = new FaceModel[this.bodyCount];
            
            for (int i = 0; i < this.bodyCount; i++)
            {
                this._faceModel[i] = new FaceModel();
                this._faceAlignment[i] = new FaceAlignment();
            }

            this._sensor.Open();

            InitializeComponent();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //_faceReader = _faceSource.OpenReader();
            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this._faceReader[i] != null)
                {
                    this._faceReader[i].FrameArrived += this.FaceReader_FrameArrived;
                }
            }

            if (this._bodyReader != null)
            {
                this._bodyReader.FrameArrived += this.BodyReader_FrameArrived;
            }
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this._faceModel[i] != null)
                {
                    this._faceModel[i].Dispose();
                    this._faceModel[i] = null;
                }

                //if (this._faceReader[i] != null)
                //{
                //    this._faceReader[i].Dispose();
                //    this._faceReader[i] = null;
                //}

                //if (this._faceSource[i] != null)
                //{
                //    //this._faceSource[i]
                //    this._faceSource[i] = null;
                //}
            }

            if (this._bodyReader != null)
            {
                this._bodyReader.Dispose();
                this._bodyReader = null;
            }

            if (this._sensor != null)
            {
                this._sensor.Close();
                this._sensor = null;
            }

            GC.SuppressFinalize(this);
        }

        private void BodyReader_FrameArrived(object sender, BodyFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null)
                {
                    //Body[] bodies = new Body[frame.BodyCount];
                    frame.GetAndRefreshBodyData(this.bodies);

                    //Body body = bodies.Where(b => b.IsTracked).FirstOrDefault();

                    for (int i = 0; i < this.bodyCount; i++)
                    {
                        if (!this._faceSource[i].IsTrackingIdValid)
                        {
                            if (this.bodies[i].IsTracked)
                            {
                                this._faceSource[i].TrackingId = this.bodies[i].TrackingId;
                            }
                        }
                    }
                }
            }
        }

        private int GetFaceSourceIndex(HighDefinitionFaceFrameSource faceFrameSource)
        {
            int index = -1;

            for (int i = 0; i < this.bodyCount; i++)
            {
                if (this._faceSource[i] == faceFrameSource)
                {
                    index = i;
                    break;
                }
            }
        
            return index;
        }

        private bool ValidateFace(HighDefinitionFaceFrame frame)
        {
            bool isFaceValid = this.temp != null && frame.FaceModel != null;
            if (!isFaceValid) return false;

            var vertices = frame.FaceModel.CalculateVerticesForAlignment(this.temp);

            if(vertices.Count <= 0) return false;

            for (int i = 0; i < vertices.Count; i++)
            {
                CameraSpacePoint vertice = vertices[i];
                DepthSpacePoint point = _sensor.CoordinateMapper.MapCameraPointToDepthSpace(vertice);
                if (point.X >= this.displayWidth || point.X <= 0 || point.Y >= this.displayHeight || point.Y <= 0) return false;
            }
            return true;
        }

        private FaceAlignment temp = null;

        private void FaceReader_FrameArrived(object sender, HighDefinitionFaceFrameArrivedEventArgs e)
        {
            using (var frame = e.FrameReference.AcquireFrame())
            {
                if (frame != null) // && frame.IsFaceTracked
                {
                    int index = this.GetFaceSourceIndex(frame.HighDefinitionFaceFrameSource);

                    //this.temp = new FaceAlignment();
                    //frame.GetAndRefreshFaceAlignmentResult(this.temp);

                    if (frame.IsFaceTracked)
                    {
                        frame.GetAndRefreshFaceAlignmentResult(this._faceAlignment[index]);
                        //this._faceModel[index] = frame.FaceModel;
                        //UpdateFacePoints(index);
                    }
                    else
                    {
                        //this._faceModel[index] = new FaceModel();
                        this._faceAlignment[index] = new FaceAlignment();
                       
                        //UpdateFacePoints(index);
                        
                    }
                    UpdateFacePoints(index);
                }
            }
        }

        private string fileName = null;

        private void UpdateFacePoints(int index)
        {
            this.temp = new FaceAlignment();
            if (_faceAlignment[index] == temp) return;

            var vertices = _faceModel[index].CalculateVerticesForAlignment(_faceAlignment[index]);

            if (vertices.Count > 0)
            {
                if (_points[index].Count == 0)
                {
                    for (int i = 0; i < vertices.Count; i++)
                    {
                        Ellipse ellipse = new Ellipse
                        {
                            Width = 2.0,
                            Height = 2.0,
                            Fill = new SolidColorBrush(Colors.Blue)
                        };

                        _points[index].Add(ellipse);
                    }

                    foreach (Ellipse ellipse in _points[index])
                    {
                        canvas.Children.Add(ellipse);
                    }
                }

                //Record UserID, Date and Time
                string fileText = index.ToString() + "," + 
                                  DateTime.Now.ToString("yyyy-MM-dd") + "," + 
                                  DateTime.Now.ToString("HH:mm:ss.fff") + ",";

                //Record points and render the face
                for (int i = 0; i < vertices.Count; i++)
                {
                    CameraSpacePoint vertice = vertices[i];
                    DepthSpacePoint point = _sensor.CoordinateMapper.MapCameraPointToDepthSpace(vertice);

                    if (float.IsInfinity(point.X) || float.IsInfinity(point.Y)) return;
                    //if (point.X >= this.displayWidth || point.X <= 0 || point.Y >= this.displayHeight || point.Y <= 0) return;
                    
                    //Record the face points
                    if (i != vertices.Count - 1)
                    {
                        fileText += point.X.ToString() + "," + point.Y.ToString() + ",";
                    }
                    else
                    {
                        fileText += point.X.ToString() + "," + point.Y.ToString() + "\n";
                    }

                    Ellipse ellipse = _points[index][i];

                    Canvas.SetLeft(ellipse, point.X);
                    Canvas.SetTop(ellipse, point.Y);
                }

                //Add the data into log file
                using (StreamWriter sw = new StreamWriter(this.fileName, true))
                {
                    sw.Write(fileText);
                }

            }
        }
    }
}