using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using OCRDemo;
using PaddleOCRSharp;

// Compile this console harness into the application's output directory (x64).
class VerifyModels
{
    static int Main()
    {
        try
        {
            var getConfig = typeof(OcrDemo).GetMethod("GetModelConfig", BindingFlags.NonPublic | BindingFlags.Static);
            using (var image = new Bitmap(500, 100))
            {
                using (var graphics = Graphics.FromImage(image))
                using (var font = new Font("Arial", 32))
                {
                    graphics.Clear(Color.White);
                    graphics.DrawString("HELLO 123", font, Brushes.Black, 10, 15);
                }
                foreach (OCRLanguage language in Enum.GetValues(typeof(OCRLanguage)))
                {
                    var config = (OCRModelConfig)getConfig.Invoke(null, new object[] { language });
                    var parameter = new OCRParameter { rec_img_h = 48, cpu_math_library_num_threads = 2, cls = false };
                    var engine = new PaddleOCREngine(config, parameter);
                    try
                    {
                        var result = engine.DetectText(image);
                        if (result == null || result.TextBlocks.Count == 0)
                            throw new Exception(language + ": no recognition result");
                        if ((language == OCRLanguage.Chinese || language == OCRLanguage.Latin) &&
                            !result.Text.Replace(" ", "").Contains("HELLO123"))
                            throw new Exception(language + ": unexpected Latin recognition: " + result.Text);
                        Console.WriteLine("PASS " + language + ": " + result.Text);
                    }
                    finally { engine.Dispose(); }
                }
            }
            string iniPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "verify-unicode.ini");
            string text = "中文 한국어 日本語 العربية Кириллица हिन्दी తెలుగు ಕನ್ನಡ தமிழ்";
            File.WriteAllText(iniPath, "[Existing]\r\nValue=preserved\r\n", Encoding.UTF8);
            var ini = new ConfigINI(iniPath);
            ini.IniWriteValue("Result", "Text", text);
            if (ini.IniReadValue("Result", "Text") != text || ini.IniReadValue("Existing", "Value") != "preserved")
                throw new Exception("Unicode INI roundtrip failed");
            Console.WriteLine("PASS Unicode INI roundtrip and migration");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
