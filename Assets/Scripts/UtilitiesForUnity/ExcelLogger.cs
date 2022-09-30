using UnityEngine;
using System.IO;

public class ExcelLogger : MonoBehaviour
{
    string filename = "";
    string path;

    public void SetFilename(string newFilename)
    {
        filename = newFilename;
    }
    public void WriteLine(string line, string head)
    {
        TextWriter textWriter = new StreamWriter(path, false);
        textWriter.WriteLine(head);
        textWriter.Close();

        textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(line);
        textWriter.Close();
    }
    public void WriteLine(string line)
    {
        TextWriter textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(line);
        textWriter.Close();

        textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(line);
        textWriter.Close();
    }

    void Start()
    {
        path = Application.dataPath + "/" + filename + ".csv";
    }
}
