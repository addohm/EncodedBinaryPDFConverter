/// <summary>
/// Take an encoded pdf file and store it as some image types and set it as a blob in a db
/// </summary>
namespace WriteBinEncPDFtoDB
{
    using System;
    using System.Data;
    using System.Data.SqlClient;
    using System.Drawing;
    using System.IO;
    using BitmapEncoder;
    using ImageMagick;

    /// <summary>
    /// 
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Gets the connection string
        /// </summary>
        private string ConnStr
        {
            get
            {
                Properties.Settings.Default.ConnStr = $"Data Source={ Properties.Settings.Default.sqlServer}" +
                    $";Initial Catalog={ Properties.Settings.Default.sqlDBName}" +
                    $";user id={ Properties.Settings.Default.sqlUsername}" +
                    $";password={ Properties.Settings.Default.sqlPassword}";
                Properties.Settings.Default.ConnStr.Replace(" ", "");
                Properties.Settings.Default.Save();
                return Properties.Settings.Default.ConnStr;
            }
        }

        /// <summary>
        /// Main method
        /// </summary>
        /// <param name="args">string list of arguments</param>
        private static void Main(string[] args)
        {
            Program program = new Program();
            ImageData imgData = new ImageData();

            // Specify the base64 text file location
            imgData.BinaryFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "Test Optic Labels", "Binary.txt");
            imgData.ToStr = File.ReadAllText(imgData.BinaryFilePath);
            WriteToTempFile(imgData);
            ConvertPdfToBmp(imgData);
            CompressBitmap(imgData);
            imgData.ToByte = File.ReadAllBytes(imgData.PngFilePath);
            WriteToDB(imgData, program.ConnStr);

            program = null;
            imgData = null;
        }

        /// <summary>
        /// Write blob to db
        /// </summary>
        /// <param name="imgData">Image Data</param>
        /// <param name="connStr">Connection string</param>
        private static void WriteToDB(ImageData imgData, string connStr)
        {
            using (SqlConnection conn = new SqlConnection(connStr))
            {
                using (SqlCommand cmd = conn.CreateCommand())
                {
                    conn.Open();
                    // Set the correct correct sql command string with parameter
                    // e.g. update TABLE_NAME Set FIELD_NAME = @labelImage where IDENTIFYING_FIELD = something
                    // FIELD_NAME should be a varbinary(max) field
                    cmd.CommandText = "update [AOF_LABELS] Set [LABEL_IMAGE] = @labelImage where [LABEL_TYPE] = 'O'";
                    SqlParameter par = new SqlParameter("@labelImage",
                                                        SqlDbType.VarBinary,
                                                        imgData.Length,
                                                        ParameterDirection.Input,
                                                        false,
                                                        0,
                                                        0,
                                                        null,
                                                        DataRowVersion.Current,
                                                        imgData.ToByte);
                    cmd.Parameters.Add(par);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        /// <summary>
        /// Write image data from base64 text file to a pdf file
        /// </summary>
        /// <param name="imgData">Image Data</param>
        private static void WriteToTempFile(ImageData imgData)
        {
            // Delete any old file
            if (File.Exists(imgData.PdfFilePath))
            {
                File.Delete(imgData.PdfFilePath);
            }

            // Read in the string from the file
            string base64string = imgData.ToStr;

            // Convert the string to a byte array
            byte[] pdf = Convert.FromBase64String(base64string);

            // Write the byte array to a file
            File.WriteAllBytes(imgData.PdfFilePath, pdf);
        }

        /// <summary>
        /// Write image data from a pdf file to a bitmap file
        /// </summary>
        /// <param name="imgData">Image Data</param>
        private static void ConvertPdfToBmp(ImageData imgData)
        {
            MagickReadSettings settings = new MagickReadSettings();

            // Settings the density to 600 dpi will create an image with a better quality
            settings.Density = new Density(600);

            using (MagickImageCollection images = new MagickImageCollection())
            {
                // Add all the pages of the pdf file to the collection
                images.Read(imgData.PdfFilePath, settings);

                // Create new image that appends all the pages horizontally
                using (IMagickImage image = images.AppendVertically())
                {
                    // Remove the transparency layers and color the background white
                    image.Alpha(AlphaOption.Remove);
                    image.Settings.BackgroundColor.A = 65535;
                    image.Settings.BackgroundColor.R = 65535;
                    image.Settings.BackgroundColor.G = 65535;
                    image.Settings.BackgroundColor.B = 65535;

                    // Convert the image to a bitmap
                    image.Format = MagickFormat.Bmp;

                    // Delete any old file
                    if (File.Exists(imgData.BmpFilePath))
                    {
                        File.Delete(imgData.BmpFilePath);
                    }

                    // Save result as a bmp
                    image.Write(imgData.BmpFilePath);
                }
            }
        }

        /// <summary>
        /// Encode the bitmap into 1, 4, or 8 bits per pixel
        /// </summary>
        /// <param name="imgData">Image Data</param>
        private static void CompressBitmap(ImageData imgData)
        {
            using (Bitmap imageRaw = new Bitmap(imgData.BmpFilePath))
            {
                using (BitmapEncoder imageCompressed = new BitmapEncoder())
                {
                    Bitmap bmpFile = imageCompressed.Encode(imageRaw, 4);

                    // Delete any old file
                    if (File.Exists(imgData.PngFilePath))
                    {
                        File.Delete(imgData.PngFilePath);
                    }

                    // Save the file in the new path
                    bmpFile.Save(imgData.PngFilePath);
                }
            }
        }
    }

    /// <summary>
    /// Image file and data information
    /// </summary>
    public class ImageData
    {
        /// <summary>
        /// Sets the PDF destination file path
        /// </summary>
        public string PdfFilePath = _pdfFilePath;
        private static string _pdfFilePath = Path.Combine(Path.GetTempPath(), "IntegraOpticLabel.pdf");

        /// <summary>
        /// Sets the BMP destination file path
        /// </summary>
        public string BmpFilePath = _bmpFilePath;
        private static string _bmpFilePath = Path.Combine(Path.GetTempPath(), "IntegraOpticLabel.bmp");

        /// <summary>
        /// Sets the BMP destination file path
        /// </summary>
        public string PngFilePath = _pngFilePath;
        private static string _pngFilePath = Path.Combine(Path.GetTempPath(), "IntegraOpticLabel.png");

        /// <summary>
        /// Gets or sets the binary encoded pdf destination file path
        /// </summary>
        public string BinaryFilePath { get; set; }


        /// <summary>
        /// Gets or sets the length of the ImageData object
        /// </summary>
        private int _length;
        public int Length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = this.ToByte.Length;
            }
        }

        /// <summary>
        /// Gets the image file as an array of bytes
        /// </summary>
        public byte[] ToByte { get; set; }

        /// <summary>
        /// Gets the encoded text file as a string
        /// </summary>
        public string ToStr { get; set; }
    }
}