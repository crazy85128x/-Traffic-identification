

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.CV.CvEnum;
using System.IO;
using System.Diagnostics;
using System.Data.SqlClient;
using System.Data;
using System.Net;

namespace MultiFaceRec
{
    public partial class FrmPrincipal : Form
    {
        //Declararation of all variables, vectors and haarcascades
        Image<Bgr, Byte> currentFrame;
        Image<Bgr, Byte> TestImage;
        Capture grabber;
        HaarCascade face;
        HaarCascade eye;
        MCvFont font = new MCvFont(FONT.CV_FONT_HERSHEY_TRIPLEX, 0.5d, 0.5d);
        Image<Gray, byte> result, TrainedFace = null;
        Image<Gray, byte> ii;
        Image<Gray, byte> gray = null;
        List<Image<Gray, byte>> trainingImages = new List<Image<Gray, byte>>();
        List<string> labels = new List<string>();
        List<string> NamePersons = new List<string>();
        int ContTrain, NumLabels, t;
        string name, names = null;
        string[] ini = new string[] { "", "" };


        public FrmPrincipal()
        {

            InitializeComponent();

            //Load haarcascades for face detection
            face = new HaarCascade("haarcascade_frontalface_default.xml");
            eye = new HaarCascade("haarcascade_eye.xml");
            try
            {
                //Load of previus trainned faces and labels for each image
                string Labelsinfo = File.ReadAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");
                string[] Labels = Labelsinfo.Split('%');
                NumLabels = Convert.ToInt16(Labels[0]);
                ContTrain = NumLabels;
                string LoadFaces;

                for (int tf = 1; tf < NumLabels + 1; tf++)
                {
                    LoadFaces = "face" + tf + ".bmp";
                    trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + "/TrainedFaces/" + LoadFaces));
                    labels.Add(Labels[tf]);
                }

            }
            catch (Exception e)
            {
                //MessageBox.Show(e.ToString());
                MessageBox.Show("Nothing in binary database, please add at least a face(Simply train the prototype with the Add Face Button).", "Triained faces load", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }

        }


        private void button1_Click(object sender, EventArgs e)
        {
            //Initialize the capture device
            //grabber = new Capture();
            // Capture cameraCapture
            // cameraCapture = new Capture("http://user:passwd@http://169.254.255.253");
            string line;

            // Read the file and display it line by line.  
            System.IO.StreamReader file =
                new System.IO.StreamReader(@"config.txt");
            int i = 0;

            while ((line = file.ReadLine()) != null)
            {
                ini[i++] = line;
            }

            file.Close();
            //Initialize the capture device
            if (ini[0] == "0") grabber = new Capture();
            else grabber = new Capture(ini[0]);
            grabber.QueryFrame();
            //Initialize the FrameGraber event
            Application.Idle += new EventHandler(FrameGrabber);
            button1.Enabled = false;
        }


        private void button2_Click(object sender, System.EventArgs e)
        {
            try
            {
                //Trained face counter
                ContTrain = ContTrain + 1;

                //Get a gray frame from capture device
                gray = grabber.QueryGrayFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

                //Face Detector
                MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
                face,
                1.2,
                10,
                Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                new Size(20, 20));

                //Action for each element detected
                foreach (MCvAvgComp f in facesDetected[0])
                {
                    TrainedFace = currentFrame.Copy(f.rect).Convert<Gray, byte>();
                    break;
                }

                //resize face detected image for force to compare the same size with the 
                //test image with cubic interpolation type method
                TrainedFace = result.Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                trainingImages.Add(TrainedFace);
                labels.Add(textBox1.Text + ",Age.Id" + textBox2.Text);

                //Show face added in gray scale
                imageBox1.Image = TrainedFace;

                //Write the number of triained faces in a file text for further load
                File.WriteAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt", trainingImages.ToArray().Length.ToString() + "%");

                //Write the labels of triained faces in a file text for further load
                for (int i = 1; i < trainingImages.ToArray().Length + 1; i++)
                {
                    trainingImages.ToArray()[i - 1].Save(Application.StartupPath + "/TrainedFaces/face" + i + ".bmp");
                    File.AppendAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt", labels.ToArray()[i - 1] + "%");
                }

                MessageBox.Show(textBox1.Text + "(" + textBox2.Text + ")" + "´s face detected and added :)", "Training OK", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch
            {
                MessageBox.Show("Enable the face detection first", "Training Fail", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }


        void FrameGrabber(object sender, EventArgs e)
        {
            label3.Text = "0";
            //label4.Text = "";
            NamePersons.Add("");


            //Get the current frame form capture device
            currentFrame = grabber.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);

            //Convert it to Grayscale
            gray = currentFrame.Convert<Gray, Byte>();

            //Face Detector
            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
          face,
          1.2,
          10,
          Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
          new Size(20, 20));

            //Action for each element detected
            foreach (MCvAvgComp f in facesDetected[0])
            {
                t = t + 1;
                result = currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                //draw the face detected in the 0th (gray) channel with blue color
                currentFrame.Draw(f.rect, new Bgr(Color.Cyan), 2);


                if (trainingImages.ToArray().Length != 0)
                {
                    //TermCriteria for face recognition with numbers of trained images like maxIteration
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);



                    //Eigen face recognizer
                    EigenObjectRecognizer recognizer = new EigenObjectRecognizer(
                       trainingImages.ToArray(),
                       labels.ToArray(),
                       0,
                       ref termCrit);
                    /*
                    float[] distance;                 
                    distance = recognizer.GetEigenDistances(result);
                    label3.Text = distance[0].ToString();*/

                    name = recognizer.Recognize(result);
                    int i = labels.IndexOf(name);
                    /*
                    Bitmap img1 = trainingImages[i].ToBitmap();
                    Bitmap img2 = result.ToBitmap();
                    float diff = 0;

                    for (int y = 0; y < img1.Height; y++)
                    {
                        for (int x = 0; x < img1.Width; x++)
                        {
                            diff += (float)Math.Abs(img1.GetPixel(x, y).R - img2.GetPixel(x, y).R) / 255;
                            diff += (float)Math.Abs(img1.GetPixel(x, y).G - img2.GetPixel(x, y).G) / 255;
                            diff += (float)Math.Abs(img1.GetPixel(x, y).B - img2.GetPixel(x, y).B) / 255;
                        }
                    }
                    if (diff > Convert.ToInt64(textBox5.Text)) name = "?";
                    */
//------------------------------------------------------------------------------------------------------------------------------------
                    int matchf = 0;
                    Image<Gray, byte> source = trainingImages[i]; // Image B
                    Image<Gray, byte> template = result;// Image A
                    Image<Gray, byte> imageToShow = source.Copy();

                    using (Image<Gray, float> result = source.MatchTemplate(template, Emgu.CV.CvEnum.TM_TYPE.CV_TM_CCOEFF_NORMED))
                    {
                        double[] minValues, maxValues;
                        Point[] minLocations, maxLocations;
                        result.MinMax(out minValues, out maxValues, out minLocations, out maxLocations);
                        double maxValue = maxValues[0];
                        // You can try different values of the threshold. I guess somewhere between 0.75 and 0.95 would be good.
                        float th = Convert.ToSingle(textBox5.Text);
                        if (maxValues[0] > th )
                        {
                            matchf = 1;
                            // This is a match. Do something with it, for example draw a rectangle around it.
                            //Rectangle match = new Rectangle(maxLocations[0], template.Size);
                            //currentFrame.Draw(match, new Bgr(Color.Red), 3);
                        }
                    }
                    if (matchf == 1) {
                        //Draw the label for each face detected and recognized
                        currentFrame.Draw(name, ref font, new Point(f.rect.X - 2, f.rect.Y - 2), new Bgr(Color.LightGreen));
                    }
                    if (matchf != 1) name = "?";
                }
//-----------------------------------------------------------------------------------------------------------------------------------------
                NamePersons[t - 1] = name;
                NamePersons.Add("");


                //Set the number of faces detected on the scene
                label3.Text = facesDetected[0].Length.ToString();
                
                //Set the region of interest on the faces

                gray.ROI = f.rect;
                MCvAvgComp[][] eyesDetected = gray.DetectHaarCascade(
                   eye,
                   1.1,
                   10,
                   Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                   new Size(20, 20));
                gray.ROI = Rectangle.Empty;

                foreach (MCvAvgComp ey in eyesDetected[0])
                {
                    Rectangle eyeRect = ey.rect;
                    eyeRect.Offset(f.rect.X, f.rect.Y);
                    currentFrame.Draw(eyeRect, new Bgr(Color.Blue), 2);
                }


            }
            t = 0;
            if (name != "?")// 認識的人
            {
                //Names concatenation of persons recognized
                for (int nnn = 0; nnn < facesDetected[0].Length; nnn++)
                {
                    names = names + NamePersons[nnn] + ", ";
                    if (NamePersons[nnn] != null && NamePersons[nnn] != "")
                    {
                        string[] args = NamePersons[nnn].Split(',');
                        if (ini[1] != "0")
                        {
                            
                            string html = string.Empty;

                            //string url = @"http://www.weema.com.tw:8000/api/V1/faceDetect.cgi?id="+args[1]+"&name=" + args[0];
                            string url = ini[1] + "?id=" + args[1] + "&name=" + args[0];

                            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                            request.AutomaticDecompression = DecompressionMethods.GZip;

                            using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                            using (Stream stream = response.GetResponseStream())
                            using (StreamReader reader = new StreamReader(stream))
                            {
                                html = reader.ReadToEnd();
                            }
                        }
                        listBox2.Items.Add(args[0]);
                    }

                }
            }
            else
            {
                // 不認識的人
                listBox2.Items.Add("unknown");
                name = "";
            }

            //Show the faces procesed and recognized
            imageBoxFrameGrabber.Image = currentFrame;
            label4.Text = names;
            names = "";
            //Clear the list(vector) of names
            NamePersons.Clear();

        }


        private void imageBoxFrameGrabber_Click(object sender, EventArgs e)
        {

        }

        private void label4_Click(object sender, EventArgs e)
        {

        }

        private void button3_Click_1(object sender, EventArgs e)
        {

            int result = 0;
            string connection = @"data source=LENOVO\RDX;database=Attendance; integrated security=true;";
            // Data Source=LENOVO\RDX;Initial Catalog=Attendance;Integrated Security=True
            SqlConnection conn = new SqlConnection(connection);
            try
            {
                if (conn.State == ConnectionState.Closed)
                {
                    conn.Open();
                }

                for (int i = 0; i < labels.ToArray().Length; i++)
                {
                    SqlCommand cmd_Insert = new SqlCommand("SP_EmpAttendance_Insert", conn);
                    cmd_Insert.CommandType = CommandType.StoredProcedure;
                    cmd_Insert.Parameters.AddWithValue("@EmpName", labels[i]);
                    cmd_Insert.Parameters.AddWithValue("@EmpAttendedTime", DateTime.Now);
                    cmd_Insert.Parameters.AddWithValue("@EmpImagePath", "~/TrainedFaces/face" + (i + 1));
                    cmd_Insert.ExecuteNonQuery();
                }

            }
            catch (SqlException ex)
            {

                throw;
            }
            finally
            {
                conn.Close();
            }
        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }

        private void FrmPrincipal_Load(object sender, EventArgs e)
        {
            if (System.Diagnostics.Process.GetProcessesByName(System.Diagnostics.Process.GetCurrentProcess().ProcessName).Length > 1)
            {
                this.Close();
            }
        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void imageBox1_Click(object sender, EventArgs e)
        {

        }

        //Connection String  
        string cs = @"data source=LENOVO\RDX;database=Attendance; integrated security=true;";
        SqlConnection con;
        SqlDataAdapter adapt;
        DataTable dt;
        private Image<Gray, byte> currentframe;

        private void button4_Click(object sender, EventArgs e)
        {
            /*  con = new SqlConnection(cs);
              con.Open();
              adapt = new SqlDataAdapter("select * from EmpAttendance", con);
              dt = new DataTable();
              adapt.Fill(dt);
              dataGridView1.DataSource = dt;
              con.Close();*/
        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            /*   con = new SqlConnection(cs);
               con.Open();
               adapt = new SqlDataAdapter("select * from EmpAttendance where EmpName like '" + textBox3.Text + "%'", con);
               dt = new DataTable();
               adapt.Fill(dt);
               dataGridView1.DataSource = dt;
               con.Close();*/
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            string line;

            // Read the file and display it line by line.  
            System.IO.StreamReader file =
                new System.IO.StreamReader(@"config.txt");
            int i = 0;

            while ((line = file.ReadLine()) != null)
            {
                ini[i++] = line;
            }

            file.Close();
            //Initialize the capture device
            if (ini[0] == "0") grabber = new Capture();
            else grabber = new Capture(ini[0]);
            grabber.QueryFrame();
            //Initialize the FrameGraber event
            Application.Idle += new EventHandler(FrameGrabber);
            button1.Enabled = false;

            timer1.Enabled = false;
        }

        //delete records
        private void button3_Click(object sender, EventArgs e)
        {
            try
            {
                //Load of previus trainned faces and labels for each image
                string Labelsinfo = File.ReadAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");
                string[] Labels = Labelsinfo.Split('%');
                NumLabels = Convert.ToInt16(Labels[0]);
                ContTrain = NumLabels;
                string LoadFaces;
                trainingImages.Clear();
                listBox1.Items.Clear();
                labels.Clear();
                for (int tf = 1; tf < NumLabels + 1; tf++)
                {
                    string v = textBox4.Text + ",Emp.Id" + textBox3.Text;
                    if (Labels[tf] == v)
                    {
                        File.Delete(Application.StartupPath + "/TrainedFaces/" + "face" + tf + ".bmp");
                        continue;
                    }
                    LoadFaces = "face" + tf + ".bmp";
                    trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + "/TrainedFaces/" + LoadFaces));
                    labels.Add(Labels[tf]);
                    listBox1.Items.Add(Labels[tf]);
                }
                File.Delete(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");

                //Write the number of triained faces in a file text for further load
                File.WriteAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt", trainingImages.ToArray().Length.ToString() + "%");

                //Write the labels of triained faces in a file text for further load
                for (int i = 1; i < trainingImages.ToArray().Length + 1; i++)
                {
                    //trainingImages.ToArray()[i - 1].Save(Application.StartupPath + "/TrainedFaces/face" + i + ".bmp");
                    File.AppendAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt", labels.ToArray()[i - 1] + "%");
                }

            }
            catch (Exception ex)
            {
                //MessageBox.Show(e.ToString());
                MessageBox.Show("Nothing in binary database, please add at least a face(Simply train the prototype with the Add Face Button).", "Triained faces load", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        //  query records
        private void button4_Click_1(object sender, EventArgs e)
        {
            listBox1.Items.Clear();
            try
            {
                //Load of previus trainned faces and labels for each image
                string Labelsinfo = File.ReadAllText(Application.StartupPath + "/TrainedFaces/TrainedLabels.txt");
                string[] Labels = Labelsinfo.Split('%');
                NumLabels = Convert.ToInt16(Labels[0]);
                ContTrain = NumLabels;
                string LoadFaces;

                for (int tf = 1; tf < NumLabels + 1; tf++)
                {
                    // if (tf == 2) continue;
                    LoadFaces = "face" + tf + ".bmp";
                    //    trainingImages.Add(new Image<Gray, byte>(Application.StartupPath + "/TrainedFaces/" + LoadFaces));
                    labels.Add(Labels[tf]);


                    listBox1.Items.Add(Labels[tf]);
                }

            }
            catch (Exception ex)
            {
                //MessageBox.Show(e.ToString());
                MessageBox.Show("Nothing in binary database, please add at least a face(Simply train the prototype with the Add Face Button).", "Triained faces load", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void listBox1_Click(object sender, EventArgs e)
        {
            string file = Application.StartupPath + "/TrainedFaces/" + "face" + (int)(listBox1.SelectedIndex + 1) + ".bmp";
            pictureBox2.Image = Image.FromFile(file);
        }

        private void button5_Click_1(object sender, EventArgs e)
        {
            listBox2.Items.Clear();
        }

		private void listBox2_SelectedIndexChanged(object sender, EventArgs e)
		{

		}

		private void button5_Click(object sender, EventArgs e)
        {
            /*        foreach (DataGridViewRow item in this.dataGridView1.SelectedRows)
                    {
                        dataGridView1.Rows.RemoveAt(item.Index);
                    }*/
        }

        private void button6_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofp = new OpenFileDialog();

            //chose the images type
            ofp.Filter = "Choose Image(*.jpg; *.jpeg; *.gif; *png; *.bmp)|*.jpg; *.jpeg; *.gif; *png; *.bmp";

            if (ofp.ShowDialog() == DialogResult.OK)
            {
                //get the image returned by OpenFileDialog 
                pictureBox1.Image = Image.FromFile(ofp.FileName);

            }





            //1.0
        }

        private void button7_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                Image InputImg = Image.FromFile(openFileDialog1.FileName);
                TestImage = new Image<Bgr, byte>(new Bitmap(InputImg));
                imageBoxFrameGrabber.Image = TestImage;
                Application.Idle += new EventHandler(FrameGrabbers);
                //button7.Enabled = true;
                //  FrmPrincipal();
            }

        }
        void FrameGrabbers(object sender, EventArgs e)
        {
            label3.Text = "0";
            //label4.Text = "";
            NamePersons.Add("");


            //Get the current frame form capture device
            //   currentFrame = grabber.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
            currentFrame = TestImage;
            //Convert it to Grayscale
            gray = currentFrame.Convert<Gray, Byte>();

            //Face Detector
            MCvAvgComp[][] facesDetected = gray.DetectHaarCascade(
          face,
          1.2,
          10,
          Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
          new Size(20, 20));

            //Action for each element detected
            foreach (MCvAvgComp f in facesDetected[0])
            {
                t = t + 1;
                result = currentFrame.Copy(f.rect).Convert<Gray, byte>().Resize(100, 100, Emgu.CV.CvEnum.INTER.CV_INTER_CUBIC);
                //draw the face detected in the 0th (gray) channel with blue color
                currentFrame.Draw(f.rect, new Bgr(Color.Cyan), 2);


                if (trainingImages.ToArray().Length != 0)
                {
                    //TermCriteria for face recognition with numbers of trained images like maxIteration
                    MCvTermCriteria termCrit = new MCvTermCriteria(ContTrain, 0.001);

                    //Eigen face recognizer
                    EigenObjectRecognizer recognizer = new EigenObjectRecognizer(
                       trainingImages.ToArray(),
                       labels.ToArray(),
                       3000,
                       ref termCrit);

                    name = recognizer.Recognize(result);

                    //Draw the label for each face detected and recognized
                    currentFrame.Draw(name, ref font, new Point(f.rect.X - 2, f.rect.Y - 2), new Bgr(Color.LightGreen));

                }

                NamePersons[t - 1] = name;
                NamePersons.Add("");


                //Set the number of faces detected on the scene
                label3.Text = facesDetected[0].Length.ToString();


                //Set the region of interest on the faces

                gray.ROI = f.rect;
                MCvAvgComp[][] eyesDetected = gray.DetectHaarCascade(
                   eye,
                   1.1,
                   10,
                   Emgu.CV.CvEnum.HAAR_DETECTION_TYPE.DO_CANNY_PRUNING,
                   new Size(20, 20));
                gray.ROI = Rectangle.Empty;

                foreach (MCvAvgComp ey in eyesDetected[0])
                {
                    Rectangle eyeRect = ey.rect;
                    eyeRect.Offset(f.rect.X, f.rect.Y);
                    currentFrame.Draw(eyeRect, new Bgr(Color.Blue), 2);
                }


            }
            t = 0;

            //Names concatenation of persons recognized
            for (int nnn = 0; nnn < facesDetected[0].Length; nnn++)
            {
                names = names + NamePersons[nnn] + ", ";
            }
            //Show the faces procesed and recognized
            imageBoxFrameGrabber.Image = currentFrame;
            label4.Text = names;
            names = "";
            //Clear the list(vector) of names
            NamePersons.Clear();

        }
    }
}