
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

public class ConfigINI
{
    public string inipath;

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

    [DllImport("kernel32", CharSet = CharSet.Unicode)]
    private static extern int GetPrivateProfileString(string section, string key, string defVal, byte[] retVal, int size, string filePath);

    public ConfigINI(string INIPath)
    {
        inipath = INIPath;
        // The Windows profile API preserves all languages only for UTF-16 INI files.
        // Upgrade existing ANSI/UTF-8 files before the first profile API write.
        if (!File.Exists(inipath) || new FileInfo(inipath).Length == 0)
        {
            File.WriteAllText(inipath, "", Encoding.Unicode);
        }
        else
        {
            byte[] data = File.ReadAllBytes(inipath);
            if (data.Length < 2 || data[0] != 0xff || data[1] != 0xfe)
            {
                Encoding encoding = Encoding.Default;
                try
                {
                    new UTF8Encoding(false, true).GetString(data);
                    encoding = Encoding.UTF8;
                }
                catch (DecoderFallbackException) { }
                string text = File.ReadAllText(inipath, encoding);
                File.WriteAllText(inipath, text, Encoding.Unicode);
            }
        }
    }

    private static Encoding GetType(string FILE_NAME)
    {
        FileStream fileStream = new FileStream(FILE_NAME, FileMode.Open, FileAccess.Read);
        byte[] array = new byte[3] { 255, 254, 65 };
        byte[] array2 = new byte[3] { 254, 255, 0 };
        byte[] array3 = new byte[3] { 239, 187, 191 };
        Encoding result = Encoding.Default;
        BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.Default);
        int.TryParse(fileStream.Length.ToString(), out var result2);
        byte[] array4 = binaryReader.ReadBytes(result2);
        if (IsUTF8Bytes(array4) || (array4[0] == 239 && array4[1] == 187 && array4[2] == 191))
        {
            result = Encoding.UTF8;
        }
        else if (array4[0] == 254 && array4[1] == byte.MaxValue && array4[2] == 0)
        {
            result = Encoding.BigEndianUnicode;
        }
        else if (array4[0] == byte.MaxValue && array4[1] == 254 && array4[2] == 65)
        {
            result = Encoding.Unicode;
        }
        binaryReader.Close();
        return result;
    }

    private static bool IsUTF8Bytes(byte[] data)
    {
        int num = 1;
        for (int i = 0; i < data.Length; i++)
        {
            byte b = data[i];
            if (num == 1)
            {
                if (b >= 128)
                {
                    while (((b = (byte)(b << 1)) & 0x80u) != 0)
                    {
                        num++;
                    }
                    if (num == 1 || num > 6)
                    {
                        return false;
                    }
                }
            }
            else
            {
                if ((b & 0xC0) != 128)
                {
                    return false;
                }
                num--;
            }
        }
        if (num > 1)
        {
            throw new Exception("非预期的byte格式");
        }
        return true;
    }

    public void IniWriteValue(string Section, string Key, string Value)
    {
        WritePrivateProfileString(Section, Key, Value, inipath);
    }

    public string IniReadValue(string Section, string Key)
    {
        byte[] array = new byte[256];
        int privateProfileString = GetPrivateProfileString(Section, Key, "", array, 256, inipath);
        string[] array2 = Encoding.Unicode.GetString(array).Split(default(char));
        return array2[0];
    }

    public string[] IniReadValues(string section, string key)
    {
        byte[] array = new byte[1024];
        int privateProfileString = GetPrivateProfileString(section, key, "", array, 1024, inipath);
        return bytetostring(array);
    }

    public bool ExistINIFile()
    {
        return File.Exists(inipath);
    }

    private string[] bytetostring(byte[] b)
    {
        string @string = Encoding.Unicode.GetString(b);
        string[] array = @string.Split(default(char));
        List<string> list = array.ToList();
        string[] array2 = array;
        foreach (string text in array2)
        {
            if (text == "")
            {
                list.Remove(text);
            }
        }
        return list.ToArray();
    }
}
