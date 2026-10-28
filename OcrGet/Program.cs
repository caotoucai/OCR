using PaddleOCRSharp;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OcrGet
{
    public class Program
    {
        static void Main(string[] args)
        {
            string ImgPath = "OcrImg.PNG";
            string IniPath = "Result.INI";
            OCRResult ocrResult;
            OCRModelConfig config = null;
            OCRParameter oCRParameter = new OCRParameter();
            PaddleOCREngine engine;

            engine = new PaddleOCREngine(config, oCRParameter);
            //WriteIniData("CanBeRead", "flag", "0",IniPath);
            Bitmap bitmap = new Bitmap(ImgPath);
            ocrResult = engine.DetectText(bitmap);
            if (ocrResult != null)
            {
                Console.WriteLine(ocrResult.Text);
                Console.ReadKey();
                //WriteIniData("Text", "Text", ocrResult.Text, IniPath);
                //WriteIniData("CanBeRead", "flag", "1", IniPath);
            }
            else
            {

            }
        }
            

    }
}
