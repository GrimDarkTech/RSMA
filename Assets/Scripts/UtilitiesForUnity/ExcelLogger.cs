using UnityEngine;
using System.IO;

public class ExcelLogger : MonoBehaviour
{
    [SerializeField] string filename = "";
    [SerializeField] string path;

    void Start()
    {
        path = Application.dataPath + "/" + filename + ".csv";
    }
    public void SetFilename(string newFilename)
    {
        filename = newFilename;
    }
    public void SetPath(string newPath)
    {
        path = newPath;
    }
    public void WriteHead(string head)
    {
        TextWriter textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(head);
        textWriter.Close();
    }
    public void WriteLine(string line)
    {
        TextWriter textWriter = new StreamWriter(path, true);
        textWriter.WriteLine(line);
        textWriter.Close();
    }
}
