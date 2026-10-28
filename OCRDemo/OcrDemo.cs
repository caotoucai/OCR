using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using PaddleOCRSharp;

namespace OCRDemo
{
    public enum OCRLanguage
    {
        Chinese = 0,
        Korean = 1,
        Japanese = 2,
        Latin = 3,
        Arabic = 4,
        Cyrillic = 5,
        Devanagari = 6,
        Telugu = 7,
        Kannada = 8,
        Tamil = 9
    }

    public partial class OcrDemo : Form
    {

        private string ImgPath = Application.StartupPath + @"\OcrImg.bmp";
        string inipath = Application.StartupPath + @"\Result.ini";
        ConfigINI ini;
        int count = 0;

        public OCRResult ocrResult;
        OCRModelConfig config = null;

        public PaddleOCREngine engine;

        public OcrDemo()
        {
            InitializeComponent();
        }

        private static OCRLanguage GetLanguageArgument()
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 1; i < args.Length; i++)
            {
                if (!string.Equals(args[i], "--language", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                OCRLanguage language;
                if (i + 1 >= args.Length ||
                    !Enum.TryParse(args[i + 1], true, out language) ||
                    !Enum.IsDefined(typeof(OCRLanguage), language))
                {
                    throw new ArgumentException("--language 后面必须提供有效的 OCRLanguage 枚举名称。");
                }

                return language;
            }

            return OCRLanguage.Chinese;
        }

        private static OCRModelConfig GetModelConfig(OCRLanguage language)
        {
            switch (language)
            {
                case OCRLanguage.Chinese:
                    return CreateModelConfig("ch_PP-OCRv4_rec_infer", "ppocr_keys_v1.txt");
                case OCRLanguage.Korean:
                    return CreateModelConfig("korean_PP-OCRv4_rec_infer", "korean_dict.txt");
                case OCRLanguage.Japanese:
                    return CreateModelConfig("japan_PP-OCRv4_rec_infer", "japan_dict.txt");
                case OCRLanguage.Latin:
                    return CreateModelConfig("latin_PP-OCRv3_rec_infer", "latin_dict.txt");
                case OCRLanguage.Arabic:
                    return CreateModelConfig("arabic_PP-OCRv4_rec_infer", "arabic_dict.txt");
                case OCRLanguage.Cyrillic:
                    return CreateModelConfig("cyrillic_PP-OCRv3_rec_infer", "cyrillic_dict.txt");
                case OCRLanguage.Devanagari:
                    return CreateModelConfig("devanagari_PP-OCRv4_rec_infer", "devanagari_dict.txt");
                case OCRLanguage.Telugu:
                    return CreateModelConfig("te_PP-OCRv4_rec_infer", "te_dict.txt");
                case OCRLanguage.Kannada:
                    return CreateModelConfig("ka_PP-OCRv4_rec_infer", "ka_dict.txt");
                case OCRLanguage.Tamil:
                    return CreateModelConfig("ta_PP-OCRv4_rec_infer", "ta_dict.txt");
                default:
                    throw new ArgumentOutOfRangeException(nameof(language));
            }
        }

        private static OCRModelConfig CreateModelConfig(string recognitionModel, string dictionary)
        {
            // Models are copied beside the executable by the project file.
            string root = Path.Combine(Application.StartupPath, "模型");
            var modelConfig = new OCRModelConfig
            {
                det_infer = Path.Combine(root, "ch_PP-OCRv4_det_infer"),
                cls_infer = Path.Combine(root, "ch_ppocr_mobile_v2.0_cls_infer"),
                rec_infer = Path.Combine(root, recognitionModel),
                keys = Path.Combine(root, "keys", dictionary)
            };
            foreach (string directory in new[] { modelConfig.det_infer, modelConfig.cls_infer, modelConfig.rec_infer })
            {
                foreach (string filename in new[] { "inference.pdmodel", "inference.pdiparams" })
                {
                    string path = Path.Combine(directory, filename);
                    if (!File.Exists(path))
                        throw new FileNotFoundException("缺少推理模型文件：" + path, path);
                }
            }
            if (!File.Exists(modelConfig.keys))
                throw new FileNotFoundException("缺少语种字典：" + modelConfig.keys, modelConfig.keys);
            return modelConfig;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (engine != null) engine.Dispose();
            base.OnFormClosed(e);
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                config = GetModelConfig(GetLanguageArgument());
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "OCR 配置错误");
                this.Close();
                return;
            }

            //OCR参数
            OCRParameter oCRParameter = new OCRParameter();
            oCRParameter.cpu_math_library_num_threads = 6;//预测并发线程数
            oCRParameter.enable_mkldnn = true;//web部署该值建议设置为0,否则出错，内存如果使用很大，建议该值也设置为0.
            oCRParameter.cls = false; //是否执行文字方向分类；默认false
            oCRParameter.det = true;//执行文本区域检测
            oCRParameter.rec = true;//执行文本识别
            oCRParameter.rec_img_h = 48;//PP-OCRv3/v4 识别模型输入高度
            oCRParameter.det_db_score_mode = true;//是否使用多段线，即文字区域是用多段线还是用矩形，
            oCRParameter.det_db_unclip_ratio = 1.6f;
            oCRParameter.max_side_len = 960;
            try
            {
                engine = new PaddleOCREngine(config, oCRParameter);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "OCR 引擎初始化失败");
                this.Close();
                return;
            }
            //this.Hide();


            try
            {

                ini = new ConfigINI(Application.StartupPath + @"\Result.ini");

                ini.IniWriteValue("CanBeRead", "flag", "0");

                //OpenFileDialog ofd = new OpenFileDialog();
                //ofd.Filter = "*.*|*.bmp;*.jpg;*.jpeg;*.tiff;*.tiff;*.png";
                //if (ofd.ShowDialog() != DialogResult.OK) return;
                //var imagebyte = File.ReadAllBytes(ofd.FileName);
                //Bitmap bitmap = new Bitmap(new MemoryStream(imagebyte));

                Bitmap bitmap = new Bitmap(ImgPath);
                ocrResult = engine.DetectText(bitmap);
                if (ocrResult != null)
                {

                    try
                    {
                        string texts = "";
                        for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                        {
                            if (i == 0)
                            {
                                texts = ocrResult.TextBlocks[i].Text;
                            }
                            else
                            {
                                texts = texts + "&" + ocrResult.TextBlocks[i].Text;
                            }

                        }
                        ini.IniWriteValue("Result", "Text[]", texts);
                        ini.IniWriteValue("Result", "Text", ocrResult.Text);
                        ini.IniWriteValue("Score", "num", ocrResult.TextBlocks[0].Score.ToString());
                        ini.IniWriteValue("JsonText","value", ocrResult.JsonText);
                        ini.IniWriteValue("CanBeRead", "flag", "1");

                        bitmap.Dispose();
                    }
                    catch (Exception)
                    {
                        bitmap.Dispose();
                        this.Close();
                    }

                }
                else
                {
                    try
                    {
                        ini.IniWriteValue("CanBeRead", "flag", "0");
                    }
                    catch (Exception)
                    {
                        bitmap.Dispose();
                        this.Close();
                    }
                }
                bitmap.Dispose();
            }
            catch
            {
                ini.IniWriteValue("CanBeRead", "flag", "0");
                this.Close();
            }

            this.Close();

        }
        
        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //this.Hide();
                ini = new ConfigINI(Application.StartupPath + @"\Result.ini");

                ini.IniWriteValue("CanBeRead", "flag", "0");

                //OpenFileDialog ofd = new OpenFileDialog();
                //ofd.Filter = "*.*|*.bmp;*.jpg;*.jpeg;*.tiff;*.tiff;*.png";
                //if (ofd.ShowDialog() != DialogResult.OK) return;
                //var imagebyte = File.ReadAllBytes(ofd.FileName);
                //Bitmap bitmap = new Bitmap(new MemoryStream(imagebyte));

                Bitmap bitmap = new Bitmap(ImgPath);
                ocrResult = engine.DetectText(bitmap);
                if (ocrResult != null)
                {

                    try
                    {
                        string texts = "";
                        for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                        {
                            if (i == 0)
                            {
                                texts = ocrResult.TextBlocks[i].Text;
                            }
                            else
                            {
                                texts = texts+ "&"+ ocrResult.TextBlocks[i].Text ;
                            }
                            
                        }
                        ini.IniWriteValue("Result", "Text[]", texts);
                        ini.IniWriteValue("Result", "Text", ocrResult.Text);
                        ini.IniWriteValue("Score", "num", ocrResult.TextBlocks[0].Score.ToString());
                        ini.IniWriteValue("CanBeRead", "flag", "1");

                        bitmap.Dispose();
                    }
                    catch (Exception)
                    {
                        bitmap.Dispose();
                    }

                }
                else
                {
                    try
                    {
                        ini.IniWriteValue("CanBeRead", "flag", "0");
                    }
                    catch (Exception)
                    {
                        bitmap.Dispose();
                    }
                }
                bitmap.Dispose();
            }
            catch
            {
                ini.IniWriteValue("CanBeRead", "flag", "0");
            }

        }



        BinaryFormatter bf = new BinaryFormatter();
        string strMemName = "MemoryShareTest";
        Bitmap bmp;
        private void button2_Click(object sender, EventArgs e)
        {
            ScreenCapturer.ScreenCapturerTool screenCapturer = new ScreenCapturer.ScreenCapturerTool();
            if (screenCapturer.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                bmp = (Bitmap)screenCapturer.Image;
                try
                {
                    string texts = "";
                    ocrResult = engine.DetectText(bmp);
                    for (int i = 0; i < ocrResult.TextBlocks.Count; i++)
                    {
                        if (i == 0)
                        {
                            texts = ocrResult.TextBlocks[i].Text;
                        }
                        else
                        {
                            texts = texts + "&" + ocrResult.TextBlocks[i].Text;
                        }
                    }
                    string[] getvalue = texts.Split('&');
                    MessageBox.Show(texts, "图像分析结果");
                    bmp.Dispose();
                    screenCapturer.Dispose();
                }
                catch (Exception ex)
                {
                }

            }
            //this.Show();
        }


        public static string[][] Read(string fullname)
        {
            if (!File.Exists(fullname)) return new string[][] { };
            var lines = File.ReadAllLines(fullname).Skip(1);
            var list = new List<string[]>();
            var builder = new StringBuilder();
            foreach (var line in lines)
            {
                builder.Clear();
                var comma = false;
                var array = line.ToCharArray();
                var values = new List<string>();
                var length = array.Length - 1;
                var index = 0;
                while (index < length)
                {
                    var item = array[index++];
                    switch (item)
                    {
                        case ',':
                            if (comma)
                            {
                                builder.Append(item);
                            }
                            else
                            {
                                values.Add(builder.ToString());
                                builder.Clear();
                            }
                            break;
                        case '"':
                            comma = !comma;
                            break;
                        default:
                            builder.Append(item);
                            break;
                    }
                }
                var count = values.Count;
                if (count == 0) continue;
                list.Add(values.ToArray());
            }
            return list.ToArray();
        }

        public static void ObjToFile(Object o)
        {
            FileStream fs = null;
            BinaryFormatter bf = null;
            try
            {
                fs = new FileStream(Application.StartupPath  + @"\ocrdata.dat", FileMode.Create);
                bf = new BinaryFormatter();
                bf.Serialize(fs, o);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                // throw;
            }
            finally
            {
                if (fs != null)
                {
                    //关闭Io流
                    fs.Close();
                }
            }
        }



        public static Object fileToObj(string cmd)
        {
            String[] s = null;
            FileStream fs = null;
            BinaryFormatter bf = null;
            try
            {
                fs = new FileStream(cmd, FileMode.OpenOrCreate);
                bf = new BinaryFormatter();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                // throw;
            }
            finally
            {
                if (fs != null)
                {
                    s = (string[])bf.Deserialize(fs);
                    fs.Close();
                }
            }
            return s;
        }
    }



}

